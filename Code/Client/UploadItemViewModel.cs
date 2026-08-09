using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace UDM10.Client
{
    public class UploadItemViewModel : INotifyPropertyChanged
    {
        public string FileName { get; }
        public string FilePath { get; }
        public long FileSizeBytes { get; }
        public string FileSizeText => FormatSize(FileSizeBytes);

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
            set { _status = value; OnPropertyChanged(); }
        }

        private string _message = "";
        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

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