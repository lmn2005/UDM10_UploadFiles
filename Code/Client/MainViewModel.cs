using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace UDM10.Client
{
    public class MainViewModel
    {
        public ObservableCollection<UploadItemViewModel> FileList { get; } = new();
        private readonly FileSelectionService _fileSelectionService = new();

        // Tạm thời dùng mock, thay bằng UploadManager thật của Hiệp khi anh ấy code xong
        private readonly IUploadManager _uploadManager = new MockUploadManager();

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
                // Ngăn thêm trùng cùng một đường dẫn (yêu cầu bắt buộc)
                if (FileList.Any(f => f.FilePath == path)) continue;

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
    }
}