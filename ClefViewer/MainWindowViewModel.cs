using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ClefViewer.Properties;
using DevExpress.Mvvm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClefViewer
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly ObservableCollection<string> logRecords;
        private string logFilePath;
        private int selectedIndex;
        private string leftPane;

        public MainWindowViewModel()
        {
            logRecords = new ObservableCollection<string>();
            SelectedIndex = 0;
            OpenFileDialogCommand = new DelegateCommand(OpenFileDialog);
            ClearCommand = new DelegateCommand(Clear, () => !string.IsNullOrEmpty(LogFilePath));
            LogFilePath = Settings.Default.LogFilePath;
        }

        private void Clear()
        {
            LeftPane = string.Empty;
            logRecords.Clear();
            LogFilePath = string.Empty;
        }

        public ICommand OpenFileDialogCommand { get; }

        public ICommand ClearCommand { get; }

        public IEnumerable<string> LogRecords => logRecords;

        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (SetValue(ref selectedIndex, value))
                {
                    Settings.Default.LogFilePath = LogFilePath;
                    if (0 <= selectedIndex)
                    {
                        LeftPane = FormatJson(logRecords[selectedIndex]);
                    }
                }
            }
        }

        public string LogFilePath
        {
            get => logFilePath;
            set
            {
                if (SetValue(ref logFilePath, value))
                {
                    logRecords.Clear();
                    if (string.IsNullOrEmpty(logFilePath))
                    {
                        return;
                    }

                    if (File.Exists(logFilePath))
                    {
                        LoadLogFile();
                        Settings.Default.LogFilePath = LogFilePath;
                    }
                    else
                    {
                        LeftPane = "Error: file does not exist.";
                    }
                }
            }
        }

        public string LeftPane
        {
            get => leftPane;
            set => SetValue(ref leftPane, value);
        }

        private string FormatJson(string logRecord)
        {
            try
            {
                return JObject.Parse(logRecord).ToString(Formatting.Indented);
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

        private async void LoadLogFile()
        {
            using (var reader = File.OpenText(LogFilePath))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    logRecords.Add(line);
                }
            }

            if (0 < logRecords.Count)
            {
                SelectedIndex = logRecords.Count - 1;
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