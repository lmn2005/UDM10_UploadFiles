using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace UDM10.Client
{
    public sealed class UploadStatistics : INotifyPropertyChanged
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, FileMetric> _files =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Stopwatch _stopwatch = new();

        private int _totalFiles;
        private int _completedFiles;
        private int _errorFiles;
        private int _cancelledFiles;
        private long _totalBytes;
        private long _transferredBytes;
        private TimeSpan _elapsed;
        private double _averageSpeedKBps;

        public int TotalFiles { get { lock (_syncRoot) return _totalFiles; } }
        public int CompletedFiles { get { lock (_syncRoot) return _completedFiles; } }
        public int ErrorFiles { get { lock (_syncRoot) return _errorFiles; } }
        public int CancelledFiles { get { lock (_syncRoot) return _cancelledFiles; } }
        public long TotalBytes { get { lock (_syncRoot) return _totalBytes; } }
        public long TransferredBytes { get { lock (_syncRoot) return _transferredBytes; } }
        public TimeSpan Elapsed { get { lock (_syncRoot) return _elapsed; } }
        public double AverageSpeedKBps { get { lock (_syncRoot) return _averageSpeedKBps; } }

        public string SummaryText
        {
            get
            {
                lock (_syncRoot)
                {
                    return $"Tổng file: {_totalFiles} | Completed: {_completedFiles} | "
                        + $"Error: {_errorFiles} | Cancelled: {_cancelledFiles} | "
                        + $"Đã truyền: {FormatBytes(_transferredBytes)}/{FormatBytes(_totalBytes)} | "
                        + $"Thời gian: {_elapsed:hh\\:mm\\:ss} | "
                        + $"Tốc độ TB: {_averageSpeedKBps:F1} KB/s";
                }
            }
        }

        public void RegisterFile(string filePath, long fileSize)
        {
            string normalizedPath = NormalizePath(filePath);
            bool changed;

            lock (_syncRoot)
            {
                changed = _files.TryAdd(
                    normalizedPath,
                    new FileMetric(Math.Max(0, fileSize), UploadItemStatus.Waiting));

                if (changed)
                {
                    EnsureStopwatchState();
                    Recalculate();
                }
            }

            if (changed)
            {
                NotifyAllProperties();
            }
        }

        public void UpdateFile(
            string filePath,
            UploadItemStatus status,
            long? bytesTransferred = null)
        {
            string normalizedPath = NormalizePath(filePath);
            bool changed = false;

            lock (_syncRoot)
            {
                if (_files.TryGetValue(normalizedPath, out FileMetric? metric))
                {
                    metric.Status = status;

                    if (bytesTransferred.HasValue)
                    {
                        metric.TransferredBytes = Math.Clamp(bytesTransferred.Value, 0, metric.FileSize);
                    }

                    if (status == UploadItemStatus.Completed)
                    {
                        metric.TransferredBytes = metric.FileSize;
                    }

                    EnsureStopwatchState();
                    Recalculate();
                    changed = true;
                }
            }

            if (changed)
            {
                NotifyAllProperties();
            }
        }

        public void ResetForRetry(string filePath)
        {
            string normalizedPath = NormalizePath(filePath);
            bool changed = false;

            lock (_syncRoot)
            {
                if (_files.TryGetValue(normalizedPath, out FileMetric? metric))
                {
                    metric.Status = UploadItemStatus.Waiting;
                    metric.TransferredBytes = 0;
                    EnsureStopwatchState();
                    Recalculate();
                    changed = true;
                }
            }

            if (changed)
            {
                NotifyAllProperties();
            }
        }

        public void RefreshElapsed()
        {
            lock (_syncRoot)
            {
                Recalculate();
            }

            OnPropertyChanged(nameof(Elapsed));
            OnPropertyChanged(nameof(AverageSpeedKBps));
            OnPropertyChanged(nameof(SummaryText));
        }

        private void EnsureStopwatchState()
        {
            bool hasPendingFiles = _files.Values.Any(metric => !IsTerminal(metric.Status));

            if (hasPendingFiles && !_stopwatch.IsRunning)
            {
                _stopwatch.Start();
            }
            else if (!hasPendingFiles && _stopwatch.IsRunning)
            {
                _stopwatch.Stop();
            }
        }

        private void Recalculate()
        {
            _totalFiles = _files.Count;
            _completedFiles = _files.Values.Count(metric => metric.Status == UploadItemStatus.Completed);
            _errorFiles = _files.Values.Count(metric => metric.Status == UploadItemStatus.Error);
            _cancelledFiles = _files.Values.Count(metric => metric.Status == UploadItemStatus.Cancelled);
            _totalBytes = _files.Values.Sum(metric => metric.FileSize);
            _transferredBytes = _files.Values.Sum(metric => metric.TransferredBytes);
            _elapsed = _stopwatch.Elapsed;
            _averageSpeedKBps = _elapsed.TotalSeconds > 0
                ? _transferredBytes / 1024d / _elapsed.TotalSeconds
                : 0;
        }

        private void NotifyAllProperties()
        {
            OnPropertyChanged(nameof(TotalFiles));
            OnPropertyChanged(nameof(CompletedFiles));
            OnPropertyChanged(nameof(ErrorFiles));
            OnPropertyChanged(nameof(CancelledFiles));
            OnPropertyChanged(nameof(TotalBytes));
            OnPropertyChanged(nameof(TransferredBytes));
            OnPropertyChanged(nameof(Elapsed));
            OnPropertyChanged(nameof(AverageSpeedKBps));
            OnPropertyChanged(nameof(SummaryText));
        }

        private static bool IsTerminal(UploadItemStatus status)
            => status is UploadItemStatus.Completed or UploadItemStatus.Error or UploadItemStatus.Cancelled;

        private static string NormalizePath(string filePath)
            => Path.GetFullPath(filePath.Trim());

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024 * 1024):F2} GB";
            if (bytes >= 1024L * 1024) return $"{bytes / (1024d * 1024):F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024d:F1} KB";
            return $"{bytes} B";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private sealed class FileMetric
        {
            public FileMetric(long fileSize, UploadItemStatus status)
            {
                FileSize = fileSize;
                Status = status;
            }

            public long FileSize { get; }
            public long TransferredBytes { get; set; }
            public UploadItemStatus Status { get; set; }
        }
    }
}
