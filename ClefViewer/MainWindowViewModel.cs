using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private readonly WeakEvent<PropertyChangedEventHandler, PropertyChangedEventArgs> _weakEvent = new WeakEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>();

        private int _selectedIndex;
        private bool _unescape;
        private bool _unwrap;
        private bool _showUTC;
        private bool _render;
        private bool _autoReload;
        private int _selectedLevelIndex = 0;
        private LogRecord _selectedLogRecord;
        private CollectionView _logRecordsView;
        private bool _tail;
        private int _tailSize;
        private string _filterText;
        private bool _useFilterText;

        public MainWindowViewModel()
        {
            PropertyChanged += (sender, args) => _weakEvent.Raise(sender, args);
            _timer = new DispatcherTimer {Interval = new TimeSpan(0, 0, 1), IsEnabled = false};
            _timer.Tick += (sender, args) => AutoReloadFile();
            OpenFileDialogCommand = new DelegateCommand(OpenFileDialog);
            ClearCommand = new DelegateCommand(() => LogFilePath = string.Empty, () => !string.IsNullOrEmpty(LogFilePath));
            ReloadCommand = new DelegateCommand(ReloadFile, () => !string.IsNullOrEmpty(LogFilePath));
            CopyCommand = new DelegateCommand<string>(Copy, CanCopy);

            SelectedIndex = -1;
        }

        public ICommand OpenFileDialogCommand { get; }

        public ICommand ClearCommand { get; }

        public ICommand ReloadCommand { get; }

        public ICommand CopyCommand { get; }

        public string RightPane => SelectedLogRecord != null ? IndentJson(SelectedLogRecord.RowText) : string.Empty;

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

        public bool Render
        {
            get => _render;
            set => SetValue(ref _render, value);
        }

        public bool AutoReload
        {
            get => _autoReload;
            set => SetValue(ref _autoReload, value, () => _timer.IsEnabled = value);
        }

        public bool ShowUTC
        {
            get => _showUTC;
            set => SetValue(ref _showUTC, value);
        }

        public bool Unescape
        {
            get => _unescape;
            set => SetValue(ref _unescape, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public bool Unwrap
        {
            get => _unwrap;
            set => SetValue(ref _unwrap, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public string LogFilePath
        {
            get => _logFile.FilePath;
            set
            {
                if (_logFile.FilePath != value)
                {
                    _logFile.FilePath = value;
                    RaisePropertiesChanged();
                    ReloadFile();
                }
            }
        }

        public bool Tail
        {
            get => _tail;
            set => SetValue(ref _tail, value, () => _logFile.TailSize = _tail && 0 < _tailSize ? _tailSize : -1);
        }

        public int TailSize
        {
            get => _tailSize;
            set => SetValue(ref _tailSize, value, () => _logFile.TailSize = _tail && 0 < _tailSize ? _tailSize : -1);
        }

        public string FilterText
        {
            get => _filterText;
            set => SetValue(ref _filterText, value, ReloadFile);
        }

        public bool UseFilterText
        {
            get => _useFilterText;
            set => SetValue(ref _useFilterText, value, ApplyFilter);
        }

        public double LineNumberWidth
        {
            get => Settings.Default.NumberWidth;
            set => Settings.Default.NumberWidth = value;
        }

        public double TimestampWidth
        {
            get => Settings.Default.TimestampWidth;
            set => Settings.Default.TimestampWidth = value;
        }

        public double ErrorLevelWidth
        {
            get => Settings.Default.ErrorLevelWidth;
            set => Settings.Default.ErrorLevelWidth = value;
        }

        public int SelectedLevelIndex
        {
            get => _selectedLevelIndex;
            set => SetValue(ref _selectedLevelIndex, value, ApplyFilter);
        }

        public void Dispose()
        {
            _timer.Stop();
        }

        public event PropertyChangedEventHandler WeakPropertyChanged
        {
            add => _weakEvent.Add(value);
            remove => _weakEvent.Remove(value);
        }

        /// <summary>
        /// Retrieve stringified JSON property values.
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
            var filterExpressions = new List<Func<LogRecord,bool>>();
            
            var selectedLevel = Enum.GetValues(typeof(LogEventLevel)).OfType<LogEventLevel>().ElementAt(SelectedLevelIndex);
            if(LogEventLevel.Verbose < selectedLevel)
            {
                filterExpressions.Add(item => selectedLevel <= item.LogEvent.Level);
            }

            if (UseFilterText)
            {
                filterExpressions.Add(item =>
                {
                    try
                    {
                        return JObject.Parse(item.RowText).SelectToken(FilterText) != null;
                    }
                    catch (JsonException e)
                    {
                        // TODO: feedback filter text parse error
                        return false;
                    }
                });
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
            if (string.IsNullOrWhiteSpace(LogFilePath))
            {
                LogRecordsView = null;
            }

            if (!File.Exists(LogFilePath))
            {
                return;
            }

            try
            {
                var showLastRecord = SelectedIndex == (LogRecordsView?.Count ?? 0) - 1;

                var logRecords = _logFile.IterateLogRecords(this, 0, CancellationToken.None, null);
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

        private void Copy(string obj)
        {
            if (obj == "LeftPane")
            {
                if (SelectedLogRecord != null)
                {
                    Clipboard.SetText(SelectedLogRecord.DisplayText);
                }
            }
        }

        private bool CanCopy(string arg)
        {
            return arg == "LeftPane" && SelectedLogRecord != null;
        }

        private string IndentJson(string logRecord)
        {
            try
            {
                var jDocument = JObject.Parse(logRecord);
                if (Unwrap)
                {
                    UnwrapJValue(jDocument);
                }

                var formatJson = jDocument.ToString(Formatting.Indented);
                return Unescape ? Regex.Unescape(formatJson) : formatJson;
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
            service.Filter = "Compact Log Event Format File (*.clef)|*.clef|Log File (*.log)|*.log|Text File (*.txt)|*.txt|All File (*.*)|*.*";
            service.Title = Application.Current.MainWindow?.Title ?? string.Empty;
            if (service.ShowDialog())
            {
                LogFilePath = service.File.GetFullName();
            }
        }
    }
}