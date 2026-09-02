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

                if (p.ConnectionStatus.HasValue)
                {
                    item.ConnectionStatus =
                        p.ConnectionStatus.Value;
                }

                Statistics.UpdateFile(item.FilePath, p.Status, p.BytesTransferred);

                RefreshConnectionStatus();
                OnPropertyChanged(nameof(HasCompletedFiles));
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
        }

        public void RetryFile(UploadItemViewModel item)
        {
            if (!item.CanRetry) return;
            item.PrepareForRetry();
            Statistics.ResetForRetry(item.FilePath);
            StartUpload(item);
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

            OnPropertyChanged(nameof(HasCompletedFiles));
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
