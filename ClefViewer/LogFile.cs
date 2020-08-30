using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Serilog;

namespace ClefViewer
{
    public class LogFile : IDisposable
    {
        private readonly FileSystemWatcher _logFileWatcher;
        private string _filePath;

        public LogFile()
        {
            _logFileWatcher = new FileSystemWatcher();
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnFilePathChanged();
                }
            }
        }

        public void Dispose()
        {
            _logFileWatcher?.Dispose();
        }

        public event EventHandler FileChangedEvent;

        public IEnumerable<LogRecord> IterateLogRecords(MainWindowViewModel viewModel, CancellationToken token, Action onIterationCompleted)
        {
            static bool IsJsonString(string line)
            {
                return !string.IsNullOrWhiteSpace(line) && line.StartsWith("{") && line.EndsWith("}");
            }

            var parallelQuery = ReadLines(FilePath, token, onIterationCompleted)
                .AsParallel()
                .AsOrdered()
                .WithCancellation(token)
                .Where(IsJsonString)
                .Select((line, idx) => new LogRecord(viewModel, line, idx + 1));
            return parallelQuery;
        }

        /// <summary>
        /// Same as <see cref="File.ReadLines(string)" />, but can open file that log writer process still opens it.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="token"></param>
        /// <param name="onLoadCompleted"></param>
        /// <returns></returns>
        private static IEnumerable<string> ReadLines(string path, CancellationToken token, Action onLoadCompleted)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"file {path} does not exist.");
            }

            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
            {
                string line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (token.IsCancellationRequested)
                    {
                        yield break;
                    }

                    yield return line.Trim();
                }

                // TODO: save current stream position for 'Tail'
                // _position = fileStream.Position;
            }

            onLoadCompleted?.Invoke();
        }

        private void OnFilePathChanged()
        {
            if (File.Exists(FilePath))
            {
                _logFileWatcher.Path = Path.GetDirectoryName(FilePath);
                _logFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                _logFileWatcher.Changed += LogFileWatcherOnChanged;
                _logFileWatcher.EnableRaisingEvents = true;
            }
            else
            {
                _logFileWatcher.EnableRaisingEvents = false;
            }

            void LogFileWatcherOnChanged(object sender, FileSystemEventArgs e)
            {
                if (e.ChangeType == WatcherChangeTypes.Changed && e.Name == Path.GetFileName(FilePath))
                {
                    Log.Information("{@FileSystemEvent}", e);
                    FileChangedEvent?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}