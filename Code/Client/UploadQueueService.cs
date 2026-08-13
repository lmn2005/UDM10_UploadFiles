using System.IO;

namespace UDM10.Client
{
    internal sealed class UploadQueueService
    {
        private readonly object _syncRoot = new();
        private readonly Queue<QueuedUpload> _queue = new();
        private readonly HashSet<string> _trackedPaths = new(StringComparer.OrdinalIgnoreCase);

        public bool TryEnqueue(string filePath, IProgress<UploadProgress> progress)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string normalizedPath;
            try
            {
                normalizedPath = NormalizePath(filePath);
            }
            catch (ArgumentException)
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (!_trackedPaths.Add(normalizedPath))
                {
                    return false;
                }

                _queue.Enqueue(new QueuedUpload(normalizedPath, progress));
                return true;
            }
        }

        public bool TryDequeue(out QueuedUpload? upload)
        {
            lock (_syncRoot)
            {
                if (_queue.Count == 0)
                {
                    upload = null;
                    return false;
                }

                upload = _queue.Dequeue();
                return true;
            }
        }

        public void MarkCompleted(string filePath)
        {
            string normalizedPath;
            try
            {
                normalizedPath = NormalizePath(filePath);
            }
            catch (ArgumentException)
            {
                return;
            }

            lock (_syncRoot)
            {
                _trackedPaths.Remove(normalizedPath);
            }
        }

        private static string NormalizePath(string filePath)
            => Path.GetFullPath(filePath.Trim());

        internal sealed class QueuedUpload
        {
            public QueuedUpload(string filePath, IProgress<UploadProgress> progress)
            {
                FilePath = filePath;
                Progress = progress;
            }

            public string FilePath { get; }
            public IProgress<UploadProgress> Progress { get; }
        }
    }
}
