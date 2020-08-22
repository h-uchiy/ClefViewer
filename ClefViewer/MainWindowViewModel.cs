using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClefViewer.Properties;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClefViewer
{
    public class MainWindowViewModel : ViewModelBase
    {
        private string _logFilePath;
        private int _selectedIndex;
        private bool _render;
        private bool _unescape;
        private CancellationTokenSource _ctsLoadLogFile;

        public MainWindowViewModel()
        {
            LogRecords = new ObservableCollection<LogRecord>();
            OpenFileDialogCommand = new DelegateCommand(OpenFileDialog);
            ClearCommand = new DelegateCommand(() => LogFilePath = string.Empty, () => !string.IsNullOrEmpty(LogFilePath));
            CopyCommand = new DelegateCommand<string>(Copy, CanCopy);

            SelectedIndex = -1;
            Render = Settings.Default.Render;
            Unescape = Settings.Default.Unescape;
            LogFilePath = Settings.Default.LogFilePath;
        }

        public ICommand OpenFileDialogCommand { get; }

        public ICommand ClearCommand { get; }

        public ICommand CopyCommand { get; }

        public ObservableCollection<LogRecord> LogRecords { get; }

        public string RightPane => 0 <= SelectedIndex ? IndentJson(LogRecords[SelectedIndex].RowText) : string.Empty;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetValue(ref _selectedIndex, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public bool Render
        {
            get => _render;
            set => SetValue(ref _render, value, () => LogRecords.ForEach(x => x.Render = Render));
        }

        public bool Unescape
        {
            get => _unescape;
            set => SetValue(ref _unescape, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public string LogFilePath
        {
            get => _logFilePath;
            set
            {
                async void ChangedCallback()
                {
                    _ctsLoadLogFile?.Cancel();
                    var newCts = new CancellationTokenSource();
                    _ctsLoadLogFile = newCts;

                    if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath))
                    {
                        LogRecords.Clear();
                        return;
                    }

                    Settings.Default.LogFilePath = _logFilePath;
                    try
                    {
                        await LoadLogFile(_ctsLoadLogFile.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        LogRecords.Clear();
                    }
                    finally
                    {
                        if (newCts == _ctsLoadLogFile)
                        {
                            _ctsLoadLogFile = null;
                        }
                    }
                }

                SetValue(ref _logFilePath, value, ChangedCallback);
            }
        }

        private void Copy(string obj)
        {
            if (obj == "LeftPane")
            {
                Clipboard.SetText(LogRecords[SelectedIndex].DisplayText);
            }
        }

        private bool CanCopy(string arg)
        {
            return arg == "LeftPane" && 0 < SelectedIndex;
        }

        private string IndentJson(string logRecord)
        {
            try
            {
                var formatJson = JObject.Parse(logRecord).ToString(Formatting.Indented);
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

        private async Task LoadLogFile(CancellationToken token)
        {
            using (var reader = File.OpenText(LogFilePath))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    token.ThrowIfCancellationRequested();
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        LogRecords.Add(new LogRecord(line, Render));
                    }
                }
            }

            if (0 < LogRecords.Count)
            {
                SelectedIndex = LogRecords.Count - 1;
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