using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Mvvm;

namespace ClefViewer
{
    public class LogFile : IDisposable
    {
        private readonly MainWindowViewModel _outer;
        private readonly Action _onFileChanged;
        private readonly FileSystemWatcher _logFileWatcher;
        private CancellationTokenSource _ctsLoadLogFile;

        public LogFile(MainWindowViewModel outer, Action onFileChanged)
        {
            _outer = outer;
            _onFileChanged = onFileChanged;
            _logFileWatcher = new FileSystemWatcher();
        }

        private string FilePath => _outer.LogFilePath;

        public void Dispose()
        {
            _ctsLoadLogFile?.Cancel();
            _ctsLoadLogFile?.Dispose();
            _logFileWatcher?.Dispose();
        }

        public async Task LoadLogFile(MainWindowViewModel viewModel, IDispatcherService dispatcher, ICollection<LogRecord> logRecords)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            _ctsLoadLogFile?.Cancel();
            var newCts = new CancellationTokenSource();
            _ctsLoadLogFile = newCts;

            await dispatcher.BeginInvoke(logRecords.Clear);

            if (string.IsNullOrEmpty(FilePath))
            {
                _logFileWatcher.EnableRaisingEvents = false;
                return;
            }

            if (!File.Exists(FilePath))
            {
                _logFileWatcher.EnableRaisingEvents = false;
                return;
            }

            _logFileWatcher.Path = Path.GetDirectoryName(FilePath);
            _logFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _logFileWatcher.Changed += LogFileWatcherOnChanged;
            _logFileWatcher.EnableRaisingEvents = true;
            try
            {
                var token = _ctsLoadLogFile.Token;
                var parallelQuery = ReadLines(FilePath)
                    .AsParallel()
                    .AsOrdered()
                    .WithCancellation(token)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select((line, idx) => new LogRecord(viewModel, line, idx + 1));
                await Task.Run(async () =>
                {
                    foreach (var logRecord in parallelQuery)
                    {
                        token.ThrowIfCancellationRequested();
                        await dispatcher.BeginInvoke(() =>
                        {
                            if (!token.IsCancellationRequested)
                            {
                                logRecords.Add(logRecord);
                            }
                        });
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                logRecords.Clear();
            }
            finally
            {
                if (newCts == _ctsLoadLogFile)
                {
                    _ctsLoadLogFile = null;
                }
            }
        }

        /// <summary>
        /// Same as <see cref="File.ReadLines(string)" />, but can open file that log writer process still opens it.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static IEnumerable<string> ReadLines(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"file {path} does not exist.");
            }

            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream, Encoding.UTF8);
            string line;
            while ((line = streamReader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        private void LogFileWatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && e.Name == Path.GetFileName(FilePath))
            {
                _onFileChanged();
            }
        }
    }
}