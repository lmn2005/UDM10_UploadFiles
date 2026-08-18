using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UDM10.Client
{
    public class UploadItemViewModel : INotifyPropertyChanged
    {
        public string FileName { get; }
        public string FilePath { get; }
        public long FileSizeBytes { get; }
        public string FileSizeText => FormatSize(FileSizeBytes);

        // Mỗi file giữ riêng 1 CancellationTokenSource để hủy độc lập,
        // không ảnh hưởng đến các file khác đang chạy song song
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();

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
            set { _speedKBps = value; OnPropertyChanged(); }
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

        // Nút Cancel chỉ bật khi đang Uploading
        public bool CanCancel => Status == UploadItemStatus.Uploading;

        // Nút Retry chỉ bật khi Error hoặc Cancelled
        public bool CanRetry => Status == UploadItemStatus.Error || Status == UploadItemStatus.Cancelled;

        public UploadItemViewModel(string filePath)
        {
            var info = new FileInfo(filePath);
            FilePath = filePath;
            FileName = info.Name;
            FileSizeBytes = info.Length;
        }

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