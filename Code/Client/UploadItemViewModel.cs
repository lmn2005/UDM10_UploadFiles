using System.IO;

namespace UDM10.Client
{
    public class UploadItemViewModel
    {
        public string FileName { get; }
        public string FilePath { get; }
        public long FileSizeBytes { get; }
        public string FileSizeText => FormatSize(FileSizeBytes);

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
    }
}