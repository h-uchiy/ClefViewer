using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ClefViewer.Properties;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Events;

namespace ClefViewer
{
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly LogFile _logFile = new LogFile();
        private readonly DispatcherTimer _timer;

        private readonly WeakEvent<PropertyChangedEventHandler, PropertyChangedEventArgs> _weakEvent =
            new WeakEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>();

        private CollectionView _logRecordsView;
        private int _selectedFilterMethods;

        private int _selectedIndex;
        private int _selectedLevelIndex;
        private LogRecord _selectedLogRecord;
        private IList<LogRecord> _selectedLogRecords;

        public MainWindowViewModel()
        {
            PropertyChanged += (sender, args) => _weakEvent.Raise(sender, args);
            _timer = new DispatcherTimer {Interval = new TimeSpan(0, 0, 1), IsEnabled = false};
            _timer.Tick += (sender, args) => AutoReloadFile();
            OpenFileDialogCommand = new DelegateCommand(OpenFileDialog);
            ClearCommand =
                new DelegateCommand(() => LogFilePath = string.Empty, () => !string.IsNullOrEmpty(LogFilePath));
            ReloadCommand = new DelegateCommand(ReloadFile, () => !string.IsNullOrEmpty(LogFilePath));
            CopyCommand = new DelegateCommand<string>(Copy, CanCopy);

            SelectedIndex = -1;
        }

        public ICommand OpenFileDialogCommand { get; }

        public ICommand ClearCommand { get; }

        public ICommand ReloadCommand { get; }

        public ICommand CopyCommand { get; }

        public string RightPane =>
            SelectedLogRecord != null ? FormatRightPane(SelectedLogRecord.RowText) : string.Empty;

        public IEnumerable<string> Levels => Enum.GetNames(typeof(LogEventLevel));

        public CollectionView LogRecordsView
        {
            get => _logRecordsView;
            set => SetValue(ref _logRecordsView, value);
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetValue(ref _selectedIndex, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public LogRecord SelectedLogRecord
        {
            get => _selectedLogRecord;
            set => SetValue(ref _selectedLogRecord, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }
        
        public IList<LogRecord> SelectedLogRecords
        {
            get => _selectedLogRecords;
            set => SetValue(ref _selectedLogRecords, value, () => RaisePropertiesChanged(nameof(TimeSpan), nameof(IsDiffVisible)));
        }

        public bool Render
        {
            get => Settings.Default.Render;
            set => SetValue(value);
        }

        public bool AutoReload
        {
            get => Settings.Default.AutoReload;
            set => SetValue(value, () => _timer.IsEnabled = value);
        }

        public bool ShowUTC
        {
            get => Settings.Default.ShowUTC;
            set => SetValue(value);
        }

        public bool Indent
        {
            get => Settings.Default.Indent;
            set => SetValue(value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public bool Unescape
        {
            get => Settings.Default.Unescape;
            set => SetValue(value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public bool Unwrap
        {
            get => Settings.Default.Unwrap;
            set => SetValue(value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public bool UrlDecode
        {
            get => Settings.Default.UrlDecode;
            set => SetValue(value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public string LogFilePath
        {
            get => Settings.Default.LogFilePath;
            set => SetValue(value, ReloadFile);
        }

        public bool Tail
        {
            get => Settings.Default.Tail;
            set => SetValue(value, ReloadFile);
        }

        public double TailSize
        {
            get => Settings.Default.TailSize;
            set => SetValue(value, ReloadFile);
        }

        public string FilterText
        {
            get => Settings.Default.FilterText;
            set => SetValue(value, () =>
            {
                Settings.Default.FilterText = value;
                ApplyFilter();
            });
        }

        public IEnumerable<string> FilterMethods => Enum.GetNames(typeof(FilterMethods));

        public int SelectedFilterMethods
        {
            get => _selectedFilterMethods;
            set => SetValue(ref _selectedFilterMethods, value, ApplyFilter);
        }

        public int SelectedLevelIndex
        {
            get => _selectedLevelIndex;
            set => SetValue(ref _selectedLevelIndex, value, ApplyFilter);
        }

        public string TimeSpan
        {
            get
            {
                var first = SelectedLogRecords[0].LogEvent.Timestamp;
                var second = SelectedLogRecords[1].LogEvent.Timestamp;
                var diff = second - first;
                return diff.ToString("g");
            }
        }

        public bool IsDiffVisible => 2 == SelectedLogRecords.Count;

        public void Dispose()
        {
            _timer.Stop();
        }

        protected override bool SetPropertyCore<T>(string propertyName, T value, out T oldValue)
        {
            var propInfo = Settings.Default.GetType().GetProperty(propertyName);
            if (propInfo == null)
            {
                return base.SetPropertyCore(propertyName, value, out oldValue);
            }

            oldValue = (T)propInfo.GetValue(Settings.Default);
            if (Equals(oldValue, value))
            {
                return false;
            }

            propInfo.SetValue(Settings.Default, value);
            RaisePropertiesChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler WeakPropertyChanged
        {
            add => _weakEvent.Add(value);
            remove => _weakEvent.Remove(value);
        }

        /// <summary>
        ///     Retrieve stringified JSON property values.
        /// </summary>
        /// <param name="jDocument"></param>
        private static void UnwrapJValue(JObject jDocument)
        {
            foreach (var jProperty in jDocument.Flatten<JToken>(x => x.Children()).OfType<JProperty>())
            {
                var jValue = jProperty.Value;
                if (jValue.Type == JTokenType.String)
                {
                    var value = jValue.ToString();
                    if (value.StartsWith("{") && value.EndsWith("}"))
                    {
                        jProperty.Value = JObject.Parse(value);
                    }
                }
            }
        }

        private void ApplyFilter()
        {
            var filterExpressions = new List<Func<LogRecord, bool>>();

            var selectedLevel = Enum.GetValues(typeof(LogEventLevel)).OfType<LogEventLevel>()
                .ElementAt(SelectedLevelIndex);
            if (LogEventLevel.Verbose < selectedLevel)
            {
                filterExpressions.Add(item => selectedLevel <= item.LogEvent.Level);
            }

            switch ((FilterMethods)SelectedFilterMethods)
            {
                case ClefViewer.FilterMethods.None:
                    break;
                case ClefViewer.FilterMethods.RegExp:
                    filterExpressions.Add(item => Regex.IsMatch(item.RowText, FilterText));
                    break;
                case ClefViewer.FilterMethods.JSONPath:
                    filterExpressions.Add(item =>
                    {
                        try
                        {
                            return JObject.Parse(item.RowText).SelectToken(FilterText) != null;
                        }
                        catch (JsonException)
                        {
                            // TODO: feedback filter text parse error
                            return false;
                        }
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var logRecordsView = LogRecordsView;
            if (logRecordsView != null)
            {
                logRecordsView.Filter = item => filterExpressions.All(x => x((LogRecord)item));
            }
        }

        private void AutoReloadFile()
        {
            if (File.Exists(LogFilePath) && _logFile.LoadedFileLength != new FileInfo(LogFilePath).Length)
            {
                ReloadFile();
            }
        }

        private void ReloadFile()
        {
            if (string.IsNullOrWhiteSpace(LogFilePath) || !File.Exists(LogFilePath))
            {
                LogRecordsView = null;
            }

            try
            {
                var showLastRecord = SelectedIndex == (LogRecordsView?.Count ?? 0) - 1;

                var tailSize = Tail ? TailSize : 0;
                var logRecords = _logFile
                    .IterateLines(LogFilePath, tailSize, 0, CancellationToken.None, null)
                    .Select((line, idx) => new LogRecord(this, line, idx + 1));
                LogRecordsView = (CollectionView)CollectionViewSource.GetDefaultView(logRecords);
                ApplyFilter();

                if (showLastRecord)
                {
                    SelectedIndex = LogRecordsView.Count - 1;
                }
            }
            catch (IOException e)
            {
                // cannot open file
                MessageBox.Show(e.GetBaseException().Message);
            }
        }

        private void Copy(string arg)
        {
            switch (arg)
            {
                case "LeftPane":
                    Clipboard.SetText(SelectedLogRecords
                        .Select(x => Render ? $"{x.Timestamp} [{x.DisplayLevel}] {x.DisplayText}" : x.RowText)
                        .Aggregate(new StringBuilder(), (x,y) => x.Append(Environment.NewLine).Append(y))
                        .ToString());
                    break;
                default:
                    break;
            }
        }

        private bool CanCopy(string arg)
        {
            switch (arg)
            {
                case "LeftPane":
                    return 0 < SelectedLogRecords.Count;
                default:
                    return false;
            }
        }

        private string FormatRightPane(string logRecord)
        {
            try
            {
                var jDocument = JObject.Parse(logRecord);
                if (Unwrap)
                {
                    UnwrapJValue(jDocument);
                }

                return jDocument.ToString(Indent ? Formatting.Indented : Formatting.None)
                    .Do(Unescape, Regex.Unescape)
                    .Do(UrlDecode, WebUtility.UrlDecode);
            }
            catch (JsonReaderException)
            {
                return logRecord;
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        private void OpenFileDialog()
        {
            var service = GetService<IOpenFileDialogService>();
            service.Filter =
                "Compact Log Event Format File (*.clef)|*.clef|Log File (*.log)|*.log|Text File (*.txt)|*.txt|All File (*.*)|*.*";
            service.Title = Application.Current.MainWindow?.Title ?? string.Empty;
            if (service.ShowDialog())
            {
                LogFilePath = service.File.GetFullName();
            }
        }
    }

    public enum FilterMethods
    {
        None,
        RegExp,
        JSONPath
    }
}