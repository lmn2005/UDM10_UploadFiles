using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace UDM10.Client
{
    public class SessionStats : INotifyPropertyChanged
    {
        private readonly ObservableCollection<UploadItemViewModel> _fileList;

        public SessionStats(ObservableCollection<UploadItemViewModel> fileList)
        {
            _fileList = fileList;
            _fileList.CollectionChanged += (_, _) => RaiseAll();
        }

        public int TotalFiles => _fileList.Count;
        public int CompletedCount => _fileList.Count(f => f.Status == UploadItemStatus.Completed);
        public int ErrorCount => _fileList.Count(f => f.Status == UploadItemStatus.Error);
        public int CancelledCount => _fileList.Count(f => f.Status == UploadItemStatus.Cancelled);
        public long TotalBytes => _fileList.Sum(f => f.FileSizeBytes);
        public string TotalBytesText => TotalBytes >= 1024 * 1024
            ? $"{TotalBytes / (1024.0 * 1024):F1} MB"
            : $"{TotalBytes / 1024.0:F1} KB";

        // Gọi hàm này mỗi khi 1 file đổi trạng thái (Completed/Error/Cancelled) để thống kê cập nhật realtime
        public void Refresh() => RaiseAll();

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(TotalFiles));
            OnPropertyChanged(nameof(CompletedCount));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(CancelledCount));
            OnPropertyChanged(nameof(TotalBytes));
            OnPropertyChanged(nameof(TotalBytesText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}