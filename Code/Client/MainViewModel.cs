using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using UDM10.Client.Services;
namespace UDM10.Client
{
    public class MainViewModel
    {
        public ObservableCollection<UploadItemViewModel> FileList { get; } = new();
        private readonly FileSelectionService _fileSelectionService = new();

        private readonly IUploadManager _uploadManager = new UploadManager();

        public void AddFilesFromDialog()
        {
            var paths = _fileSelectionService.PickFilesFromDialog();
            if (paths != null) AddFiles(paths);
        }

        public void AddFilesFromDrop(IDataObject data)
        {
            var paths = _fileSelectionService.GetDroppedFiles(data);
            if (paths != null) AddFiles(paths);
        }

        private void AddFiles(string[] paths)
        {
            foreach (var path in paths)
            {
                // Ngăn thêm cùng một file qua chọn file nhiều lần hoặc kéo-thả lại.
                if (FileList.Any(f => IsSamePath(f.FilePath, path))) continue;

                var item = new UploadItemViewModel(path);
                FileList.Add(item);

                // IProgress<T> tự động chạy callback trên đúng luồng UI
                // (nó tự "chụp" SynchronizationContext lúc tạo ra),
                // nên KHÔNG cần gọi Dispatcher.Invoke thủ công nữa.
                var progress = new Progress<UploadProgress>(p =>
                {
                    item.PercentComplete = p.PercentComplete;
                    item.SpeedKBps = p.SpeedKBps;
                    item.Status = p.Status;
                    item.Message = p.Message ?? "";
                });

                _uploadManager.EnqueueFile(item.FilePath, progress);
            }
        }

        private static bool IsSamePath(string firstPath, string secondPath)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(firstPath),
                    Path.GetFullPath(secondPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
