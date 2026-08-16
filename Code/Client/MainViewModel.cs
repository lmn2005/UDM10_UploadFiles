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
        private readonly IUploadManager _uploadManager = new MockUploadManager();

        public void AddFilesFromDialog()
        {
            var paths = _fileSelectionService.PickFilesFromDialog();
            if (paths != null) AddFiles(paths);
        }

        public void AddFilesFromDrop(IDataObject data)
        {
            var allPaths = (string[])data.GetData(DataFormats.FileDrop);
            var validPaths = _fileSelectionService.GetDroppedFiles(data);

            if (validPaths != null)
            {
                AddFiles(validPaths);

                int skippedFolders = allPaths.Length - validPaths.Length;
                if (skippedFolders > 0)
                    MessageBox.Show($"{skippedFolders} thư mục đã bị bỏ qua (chỉ hỗ trợ file).");
            }
        }

        private void AddFiles(string[] paths)
        {
            foreach (var path in paths)
            {
                if (FileList.Any(f => f.FilePath == path)) continue;
                var item = new UploadItemViewModel(path);
                FileList.Add(item);
                StartUpload(item);
            }
        }

        private void StartUpload(UploadItemViewModel item)
        {
            var progress = new Progress<UploadProgress>(p =>
            {
                item.PercentComplete = p.PercentComplete;
                item.SpeedKBps = p.SpeedKBps;
                item.Status = p.Status;
                item.Message = p.Message ?? "";
            });

            _uploadManager.EnqueueFile(item.FilePath, progress, item.CancellationTokenSource.Token);
        }

        public void CancelFile(UploadItemViewModel item)
        {
            if (!item.CanCancel) return;
            item.CancellationTokenSource.Cancel();
        }

        public void RetryFile(UploadItemViewModel item)
        {
            if (!item.CanRetry) return;
            item.CancellationTokenSource = new System.Threading.CancellationTokenSource();
            item.PercentComplete = 0;
            item.Status = UploadItemStatus.Waiting;
            item.Message = "";
            StartUpload(item);
        }
    }
}