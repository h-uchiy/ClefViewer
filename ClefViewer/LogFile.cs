using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Mvvm;

namespace ClefViewer
{
    public class LogFile : IDisposable
    {
        private readonly Action _onFileChanged;
        private readonly FileSystemWatcher _logFileWatcher;
        private CancellationTokenSource _ctsLoadLogFile;

        public LogFile(Action onFileChanged)
        {
            _logFileWatcher = new FileSystemWatcher();
            _onFileChanged = onFileChanged;
        }

        public string FilePath { get; set; }

        public bool Render { get; set; }

        public void Dispose()
        {
            _ctsLoadLogFile?.Cancel();
            _ctsLoadLogFile?.Dispose();
            _logFileWatcher?.Dispose();
        }

        public async Task LoadLogFile(IDispatcherService dispatcher, ICollection<LogRecord> logRecords)
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
                var parallelQuery = File.ReadLines(FilePath)
                    .AsParallel()
                    .AsOrdered()
                    .WithCancellation(token)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => new LogRecord(line, Render));
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

        private void LogFileWatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && e.Name == Path.GetFileName(FilePath))
            {
                _onFileChanged();
            }
        }
    }
}