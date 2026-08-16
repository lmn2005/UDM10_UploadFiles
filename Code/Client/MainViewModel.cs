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
        private readonly ClientSettings _settings;
        private readonly IUploadManager _uploadManager;

        public MainViewModel()
        {
            _settings = ClientSettings.Load();
            _uploadManager = new UploadManager(_settings);
        }

        public string ServerIp => _settings.Network.ServerIp;
        public int ServerPort => _settings.Network.Port;

        public void UpdateServerEndpoint(string serverIp, int serverPort)
        {
            _settings.Network.ServerIp = serverIp;
            _settings.Network.Port = serverPort;
        }
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
                // Ngăn thêm cùng một file qua chọn file nhiều lần hoặc kéo-thả lại.
                if (FileList.Any(f => IsSamePath(f.FilePath, path))) continue;

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
