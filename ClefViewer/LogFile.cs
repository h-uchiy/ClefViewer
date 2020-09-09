using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Serilog;

namespace ClefViewer
{
    internal class LogFile
    {
        public string FilePath { get; set; }

        public int TailSize { get; set; }

        public IEnumerable<LogRecord> IterateLogRecords(MainWindowViewModel viewModel, int skip, CancellationToken token, Action onIterationCompleted)
        {
            static bool IsJsonString(string line)
            {
                return !string.IsNullOrWhiteSpace(line) && line.StartsWith("{") && line.EndsWith("}");
            }

            return ReadLines(skip, token, onIterationCompleted)
                .AsParallel()
                .AsOrdered()
                .WithCancellation(token)
                .Where(IsJsonString)
                .Select((line, idx) => new LogRecord(viewModel, line, skip + idx + 1));
        }

        private static IEnumerable<long> Sequence(long start, long stop, long skip)
        {
            for (var idx = start; idx < stop; idx += skip)
            {
                yield return idx;
            }
        }

#if false
        public unsafe Task<int> CountLogRecords()
        {
            const long maxViewSize = 64 * 1024 * 1024;

            if (_count != -1)
            {
                return Task.FromResult(_count);
            }

            Log.Verbose("CountLogRecords() begin");
            var fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var memoryMappedFile = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, false);

            var task = Task.Run(() => Sequence(0, fileStream.Length, maxViewSize)
                .AsParallel()
                .Select(offset =>
                {
                    var viewSize = Math.Min(maxViewSize, fileStream.Length - offset);
                    using var view = memoryMappedFile.CreateViewAccessor(offset, viewSize, MemoryMappedFileAccess.Read);
                    var handle = view.SafeMemoryMappedViewHandle;
                    byte* pointer = null;
                    try
                    {
                        handle.AcquirePointer(ref pointer);
                        var count = 0;
                        for (var idx = 0; idx < viewSize; idx++)
                        {
                            if (pointer[idx] == '\n')
                            {
                                count++;
                            }
                        }

                        return count;
                    }
                    finally
                    {
                        if (pointer != null)
                        {
                            handle.ReleasePointer();
                        }
                    }
                })
                .Aggregate((x, y) => x + y));
            task.ContinueWith(antecedent =>
            {
                Log.Verbose("CountLogRecords() = {Count} end", _count);
                Interlocked.Exchange(ref _count, antecedent.Result);
                memoryMappedFile?.Dispose();
                memoryMappedFile = null;
                fileStream?.Dispose();
                fileStream = null;
            });
            return task;
        }
#endif

        /// <summary>
        /// Same as <see cref="File.ReadLines(string)" />, but can open file that log writer process still opens it.
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="token"></param>
        /// <param name="onLoadCompleted"></param>
        /// <returns></returns>
        private IEnumerable<string> ReadLines(long skip, CancellationToken token, Action onLoadCompleted)
        {
            if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
            {
                yield break;
            }

            var count = 0;
            using (var fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Log.Verbose("ClefViewer.LogFile.ReadLines opened {FilePath}", FilePath);
                LoadedFileLength = fileStream.Length;
                if (0 < TailSize)
                {
                    fileStream.Position = Math.Max(0, fileStream.Length - TailSize * 1024 * 1024);
                }

                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        if (token.IsCancellationRequested)
                        {
                            yield break;
                        }

                        count++;
                        if (count <= skip)
                        {
                            continue;
                        }

                        yield return line;
                    }
                }
            }

            Log.Verbose("ClefViewer.LogFile.ReadLines end file reading: closed {FilePath}, it was {Line} lines.", FilePath, count);
            onLoadCompleted?.Invoke();
        }

        public long LoadedFileLength { get; private set; }
    }
}