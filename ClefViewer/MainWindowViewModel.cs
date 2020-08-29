using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
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
        private readonly LogFile _logFile;

        private int _selectedIndex;
        private bool _unescape;
        private bool _unwrap;
        private bool _showUTC;
        private bool _render;
        private string _logFilePath;
        private bool _autoReload;
        private int _selectedLevelIndex = 0;
        private LogRecord _selectedItem;

        public MainWindowViewModel()
        {
            LogRecords = new ObservableCollection<LogRecord>();
            LogRecordsView = (CollectionView)CollectionViewSource.GetDefaultView(LogRecords);
            _logFile = new LogFile(this, () =>
            {
                if (AutoReload)
                {
                    ReloadFile();
                }
            });

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

        public string RightPane => SelectedItem != null ? IndentJson(SelectedItem.RowText) : string.Empty;

        public IEnumerable<string> Levels => Enum.GetNames(typeof(LogEventLevel));

        public CollectionView LogRecordsView { get; }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetValue(ref _selectedIndex, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public LogRecord SelectedItem
        {
            get => _selectedItem;
            set => SetValue(ref _selectedItem, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public bool Render
        {
            get => _render;
            set => SetValue(ref _render, value);
        }

        public bool AutoReload
        {
            get => _autoReload;
            set => SetValue(ref _autoReload, value);
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
            get => _logFilePath;
            set => SetValue(ref _logFilePath, value, ReloadFile);
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
            set => SetValue(ref _selectedLevelIndex, value, () =>
            {
                var selectedLevel = Enum.GetValues(typeof(LogEventLevel)).OfType<LogEventLevel>().ElementAt(value);
                LogRecordsView.Filter = item => selectedLevel <= ((LogRecord)item).LogEvent.Level;
            });
        }

        private ObservableCollection<LogRecord> LogRecords { get; }

        void IDisposable.Dispose()
        {
            _logFile.Dispose();
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

        private async void ReloadFile()
        {
            var dispatcherService = GetService<IDispatcherService>();
            try
            {
                await _logFile.LoadLogFile(this, dispatcherService, LogRecords);
            }
            catch (IOException e)
            {
                // cannot open file
                MessageBox.Show(e.GetBaseException().Message);
                return;
            }

            if (0 < LogRecords.Count)
            {
                SelectedIndex = LogRecords.Count - 1;
            }
        }

        private void Copy(string obj)
        {
            if (obj == "LeftPane")
            {
                if (SelectedItem != null)
                {
                    Clipboard.SetText(SelectedItem.DisplayText);
                }
            }
        }

        private bool CanCopy(string arg)
        {
            return arg == "LeftPane" && SelectedItem != null;
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