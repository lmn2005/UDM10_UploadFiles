using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace UDM10.Client
{
    public class UploadItemViewModel : INotifyPropertyChanged
    {
        private string _statusMessage = "Chưa upload";

        public string FileName { get; }
        public string FilePath { get; }
        public long FileSizeBytes { get; }
        public string FileSizeText => FormatSize(FileSizeBytes);

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value)
                {
                    return;
                }

                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public UploadItemViewModel(string filePath)
        {
            FileInfo info = new(filePath);
            FilePath = filePath;
            FileName = info.Name;
            FileSizeBytes = info.Length;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / (1024.0 * 1024):F1} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes} B";
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
