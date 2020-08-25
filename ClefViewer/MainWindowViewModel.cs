using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClefViewer
{
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly ObservableCollection<LogRecord> _logRecords;
        private readonly LogFile _logFile;
        private int _selectedIndex;
        private bool _unescape;
        private bool _unwrap;

        public MainWindowViewModel()
        {
            _logFile = new LogFile(ReloadFile);
            _logRecords = new ObservableCollection<LogRecord>();
            LogRecords = CollectionViewSource.GetDefaultView(_logRecords);

            OpenFileDialogCommand = new DelegateCommand(OpenFileDialog);
            ClearCommand = new DelegateCommand(() => LogFilePath = string.Empty, () => !string.IsNullOrEmpty(LogFilePath));
            CopyCommand = new DelegateCommand<string>(Copy, CanCopy);

            SelectedIndex = -1;
        }

        public ICommand OpenFileDialogCommand { get; }

        public ICommand ClearCommand { get; }

        public ICommand CopyCommand { get; }

        public ICollectionView LogRecords { get; }

        public string RightPane => 0 <= SelectedIndex ? IndentJson(_logRecords.Skip(SelectedIndex).First().RowText) : string.Empty;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetValue(ref _selectedIndex, value, () => RaisePropertiesChanged(nameof(RightPane)));
        }

        public bool Render
        {
            get => _logFile.Render;
            set
            {
                if (!Equals(_logFile.Render, value))
                {
                    _logFile.Render = value;
                    RaisePropertiesChanged(nameof(Render));
                    LinqExtensions.ForEach(_logRecords, x => x.Render = Render);
                }
            }
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
                if (!Equals(_logFile.FilePath, value))
                {
                    _logFile.FilePath = value;
                    RaisePropertiesChanged(nameof(LogFilePath));
                    ReloadFile();
                }
            }
        }

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
                    if (value.StartsWith('{') && value.EndsWith('}'))
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
                await _logFile.LoadLogFile(dispatcherService, _logRecords);
            }
            catch (IOException e)
            {
                // cannot open file
                MessageBox.Show(e.GetBaseException().Message);
                return;
            }

            if (0 < _logRecords.Count)
            {
                SelectedIndex = _logRecords.Count - 1;
            }
        }

        private void Copy(string obj)
        {
            if (obj == "LeftPane")
            {
                var logRecord = _logRecords.Skip(SelectedIndex).FirstOrDefault();
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