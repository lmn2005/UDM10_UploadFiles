using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UDM10.Client
{
    public class UploadItemViewModel : INotifyPropertyChanged, IDisposable
    {
        public string FileName { get; }
        public string FilePath { get; }
        public long FileSizeBytes { get; }
        public string FileSizeText => FormatSize(FileSizeBytes);

        // Rút gọn tên file dài trên giao diện, tránh vỡ layout cột "Tên file"
        public string FileNameDisplay => FileName.Length > 30
            ? FileName.Substring(0, 27) + "..."
            : FileName;

        // Mỗi file giữ riêng 1 CancellationTokenSource để hủy độc lập,
        // không ảnh hưởng đến các file khác đang chạy song song
        public CancellationTokenSource CancellationTokenSource { get; private set; } = new();
        public CancellationToken UploadCancellationToken => CancellationTokenSource.Token;

        private double _percentComplete;
        public double PercentComplete
        {
            get => _percentComplete;
            set { _percentComplete = value; OnPropertyChanged(); }
        }

        private double _speedKBps;
        public double SpeedKBps
        {
            get => _speedKBps;
            set
            {
                _speedKBps = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SpeedText));
            }
        }

        // Tự động đổi đơn vị: dưới 1024 KB/s hiển thị KB/s, trên đó hiển thị MB/s
        public string SpeedText => _speedKBps >= 1024
            ? $"{_speedKBps / 1024.0:F2} MB/s"
            : $"{_speedKBps:F0} KB/s";

        private long _bytesTransferred;
        public long BytesTransferred
        {
            get => _bytesTransferred;
            set { _bytesTransferred = value; OnPropertyChanged(); }
        }

        private UploadItemStatus _status = UploadItemStatus.Waiting;
        public UploadItemStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                // Báo cho giao diện biết cần vẽ lại nút Cancel/Retry theo trạng thái mới
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(CanRetry));
            }
        }

        private string _message = "";
        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        // Cho phép hủy cả file đang chờ và file đang upload.
        public bool CanCancel => (Status == UploadItemStatus.Waiting || Status == UploadItemStatus.Uploading)
            && !CancellationTokenSource.IsCancellationRequested;

        // Nút Retry chỉ bật khi Error hoặc Cancelled
        public bool CanRetry => Status == UploadItemStatus.Error || Status == UploadItemStatus.Cancelled;

        public UploadItemViewModel(string filePath)
        {
            var info = new FileInfo(filePath);
            FilePath = filePath;
            FileName = info.Name;
            FileSizeBytes = info.Length;
        }

        public void RequestCancellation()
        {
            if (!CanCancel)
            {
                return;
            }

            CancellationTokenSource.Cancel();
            Message = "Đang hủy upload...";
            OnPropertyChanged(nameof(CanCancel));
        }

        public void PrepareForRetry()
        {
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new CancellationTokenSource();
            PercentComplete = 0;
            SpeedKBps = 0;
            BytesTransferred = 0;
            Status = UploadItemStatus.Waiting;
            Message = "Đang chờ lượt upload lại...";
            OnPropertyChanged(nameof(CanCancel));
            OnPropertyChanged(nameof(CanRetry));
        }

        public void Dispose() => CancellationTokenSource.Dispose();

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}