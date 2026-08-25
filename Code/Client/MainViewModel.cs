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
                Statistics.UpdateFile(item.FilePath, p.Status, p.BytesTransferred);

                if (p.Status == UploadItemStatus.Error)
                    ConnectionStatus = ConnectionStatus.Error;
                else if (p.Status == UploadItemStatus.Uploading || p.Status == UploadItemStatus.Completed)
                    ConnectionStatus = ConnectionStatus.Connected;
            });

            _uploadManager.EnqueueFile(item.FilePath, progress, item.UploadCancellationToken);
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