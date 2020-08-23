using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly FileSystemWatcher _logFileWatcher;
        private string _logFilePath;
        private int _selectedIndex;
        private bool _render;
        private bool _unescape;
        private CancellationTokenSource _ctsLoadLogFile;

        public MainWindowViewModel()
        {
            _logFileWatcher = new FileSystemWatcher();
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

        public string RightPane => 0 <= SelectedIndex ? IndentJson(LogRecords.Skip(SelectedIndex).First().RowText) : string.Empty;

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
            set => SetValue(ref _logFilePath, value, LoadLogFile);
        }

        void IDisposable.Dispose()
        {
            _ctsLoadLogFile?.Cancel();
            _ctsLoadLogFile?.Dispose();
            _logFileWatcher?.Dispose();
        }

        private async void LoadLogFile()
        {
            _ctsLoadLogFile?.Cancel();
            var newCts = new CancellationTokenSource();
            _ctsLoadLogFile = newCts;

            if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath))
            {
                _logFileWatcher.EnableRaisingEvents = false;
                LogRecords.Clear();
                return;
            }

            _logFileWatcher.Path = Path.GetDirectoryName(_logFilePath);
            _logFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _logFileWatcher.Changed += LogFileWatcherOnChanged;
            _logFileWatcher.EnableRaisingEvents = true;
            Settings.Default.LogFilePath = _logFilePath;
            try
            {
                var token = _ctsLoadLogFile.Token;
                var parallelQuery = File.ReadLines(LogFilePath)
                    .AsParallel()
                    .AsOrdered()
                    .WithCancellation(token)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => new LogRecord(line, Render));
                var dispatcher = GetService<IDispatcherService>();
                if(dispatcher == null)
                {
                    // called from constructor
                    foreach (var logRecord in parallelQuery)
                    {
                        token.ThrowIfCancellationRequested();
                        LogRecords.Add(logRecord);
                    }
                }
                else
                {
                    await Task.Run(async () =>
                    {
                        foreach (var logRecord in parallelQuery)
                        {
                            token.ThrowIfCancellationRequested();
                            await dispatcher.BeginInvoke(() =>
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    LogRecords.Add(logRecord);
                                }
                            });
                        }
                    }, token);
                }

                if (0 < LogRecords.Count)
                {
                    SelectedIndex = LogRecords.Count - 1;
                }
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

        private void LogFileWatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && e.Name == Path.GetFileName(LogFilePath))
            {
                GetService<IDispatcherService>()
                    ?.BeginInvoke(() =>
                    {
                        LogRecords.Clear();
                        LoadLogFile();
                    });
            }
        }

        private void Copy(string obj)
        {
            if (obj == "LeftPane")
            {
                var logRecord = LogRecords.Skip(SelectedIndex).FirstOrDefault();
                if (logRecord != null)
                {
                    Clipboard.SetText(logRecord.DisplayText);
                }
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