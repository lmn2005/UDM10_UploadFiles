using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using UDM10.Client.Services;

namespace UDM10.Client
{
    public class MainViewModel : IAsyncDisposable, INotifyPropertyChanged
    {
        public ObservableCollection<UploadItemViewModel> FileList { get; } = new();
        public UploadStatistics Statistics { get; } = new();
        private readonly FileSelectionService _fileSelectionService = new();
        private readonly ClientSettings _settings;
        private readonly IUploadManager _uploadManager;
        private readonly DispatcherTimer _statisticsTimer;

        private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
        public ConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        // Dùng để bật/tắt nút "Xóa các mục hoàn tất" trên giao diện.
        public bool HasCompletedFiles =>
            FileList.Any(file => file.Status == UploadItemStatus.Completed);

        public bool CanCancelAll =>
            FileList.Any(file => file.CanCancel);

        public bool CanRetryAllFailed =>
            FileList.Any(file => file.CanRetry);

        public MainViewModel()
        {
            _settings = ClientSettings.Load();
            _uploadManager = new UploadManager(_settings);
            _statisticsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _statisticsTimer.Tick += (_, _) => Statistics.RefreshElapsed();
            _statisticsTimer.Start();
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

                var item = new UploadItemViewModel(path);
                FileList.Add(item);
                Statistics.RegisterFile(item.FilePath, item.FileSizeBytes);
                StartUpload(item);
            }

            NotifyActionStateChanged();
        }

        private void StartUpload(UploadItemViewModel item)
        {
            var progress = new Progress<UploadProgress>(p =>
            {
                item.PercentComplete = p.PercentComplete;
                item.SpeedKBps = p.SpeedKBps;
                if (p.BytesTransferred.HasValue)
                {
                    item.BytesTransferred = p.BytesTransferred.Value;
                }
                item.Status = p.Status;
                item.Message = p.Message ?? "";
                item.SavedFileName = p.SavedFileName;

                if (p.ConnectionStatus.HasValue)
                {
                    item.ConnectionStatus =
                        p.ConnectionStatus.Value;
                }

                Statistics.UpdateFile(item.FilePath, p.Status, p.BytesTransferred);

                RefreshConnectionStatus();
                NotifyActionStateChanged();
            });

            _uploadManager.EnqueueFile(item.FilePath, progress, item.UploadCancellationToken);
        }

        private void RefreshConnectionStatus()
        {
            if (FileList.Any(
                    file => file.ConnectionStatus ==
                        ConnectionStatus.Connected))
            {
                ConnectionStatus = ConnectionStatus.Connected;
            }
            else if (FileList.Any(
                         file => file.ConnectionStatus ==
                             ConnectionStatus.Connecting))
            {
                ConnectionStatus = ConnectionStatus.Connecting;
            }
            else if (FileList.Any(
                         file => file.ConnectionStatus ==
                             ConnectionStatus.Error))
            {
                ConnectionStatus = ConnectionStatus.Error;
            }
            else
            {
                ConnectionStatus = ConnectionStatus.Disconnected;
            }
        }

        public void CancelFile(UploadItemViewModel item)
        {
            if (!item.CanCancel) return;
            item.RequestCancellation();
            NotifyActionStateChanged();
        }

        public void RetryFile(UploadItemViewModel item)
        {
            if (!item.CanRetry) return;
            item.PrepareForRetry();
            Statistics.ResetForRetry(item.FilePath);
            StartUpload(item);
            NotifyActionStateChanged();
        }

        // Hủy các file đang chờ hoặc đang tải tại thời điểm người dùng thao tác.
        // Danh sách được chụp trước để callback tiến trình không làm thay đổi vòng lặp.
        public int CancelAllActiveFiles()
        {
            UploadItemViewModel[] cancellableFiles = FileList
                .Where(file => file.CanCancel)
                .ToArray();

            foreach (UploadItemViewModel item in cancellableFiles)
            {
                item.RequestCancellation();
            }

            NotifyActionStateChanged();
            return cancellableFiles.Length;
        }

        // Đưa toàn bộ file Error hoặc Cancelled về Waiting rồi xếp lại vào queue.
        // PrepareForRetry đổi trạng thái ngay nên thao tác lặp không tạo upload trùng.
        public int RetryAllFailedFiles()
        {
            UploadItemViewModel[] retryableFiles = FileList
                .Where(file => file.CanRetry)
                .ToArray();

            foreach (UploadItemViewModel item in retryableFiles)
            {
                RetryFile(item);
            }

            NotifyActionStateChanged();
            return retryableFiles.Length;
        }

        // Xóa các file Completed khỏi danh sách hiển thị và thống kê phía Client.
        // Chỉ xóa lịch sử hiển thị, không đụng đến file thật đã lưu trên Server.
        public void ClearCompletedFiles()
        {
            var completedItems = FileList
                .Where(file => file.Status == UploadItemStatus.Completed)
                .ToList();

            foreach (UploadItemViewModel item in completedItems)
            {
                FileList.Remove(item);
                Statistics.UnregisterFile(item.FilePath);
                item.Dispose();
            }

            NotifyActionStateChanged();
        }

        public async ValueTask DisposeAsync()
        {
            _statisticsTimer.Stop();
            await _uploadManager.DisposeAsync();

            foreach (UploadItemViewModel item in FileList)
            {
                item.Dispose();
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

        private void NotifyActionStateChanged()
        {
            OnPropertyChanged(nameof(HasCompletedFiles));
            OnPropertyChanged(nameof(CanCancelAll));
            OnPropertyChanged(nameof(CanRetryAllFailed));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
