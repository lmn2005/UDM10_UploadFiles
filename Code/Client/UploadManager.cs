using UDM10.Client.Services;

namespace UDM10.Client
{
    internal sealed class UploadManager : IUploadManager
    {
        private readonly UploadClientService _uploadClientService;
        private readonly UploadQueueService _queueService;
        private readonly SemaphoreSlim _availableSlots;
        private readonly SemaphoreSlim _queueSignal = new(0);

        public UploadManager()
        {
            ClientSettings settings = ClientSettings.Load();
            int configuredMax = settings.Upload.MaxConcurrentFiles;
            int maxConcurrentFiles = configuredMax > 0
                ? Math.Min(configuredMax, 3)
                : 3;

            _uploadClientService = new UploadClientService();
            _queueService = new UploadQueueService();
            _availableSlots = new SemaphoreSlim(maxConcurrentFiles, maxConcurrentFiles);

            _ = Task.Run(DispatchQueueAsync);
        }

        public void EnqueueFile(string filePath, IProgress<UploadProgress> progress)
        {
            if (!_queueService.TryEnqueue(filePath, progress))
            {
                progress.Report(new UploadProgress
                {
                    Status = UploadItemStatus.Error,
                    Message = "File đã có trong hàng đợi hoặc đường dẫn không hợp lệ."
                });
                return;
            }

            progress.Report(new UploadProgress
            {
                Status = UploadItemStatus.Waiting,
                Message = "Đang chờ lượt upload..."
            });

            _queueSignal.Release();
        }

        private async Task DispatchQueueAsync()
        {
            while (true)
            {
                await _queueSignal.WaitAsync();

                if (!_queueService.TryDequeue(out UploadQueueService.QueuedUpload? upload))
                {
                    continue;
                }

                await _availableSlots.WaitAsync();
                _ = ProcessUploadAsync(upload!);
            }
        }

        private async Task ProcessUploadAsync(UploadQueueService.QueuedUpload upload)
        {
            try
            {
                UploadResult result = await _uploadClientService.UploadFileAsync(
                    upload.FilePath,
                    upload.Progress);

                upload.Progress.Report(new UploadProgress
                {
                    PercentComplete = result.IsSuccess ? 100 : 0,
                    Status = result.IsSuccess ? UploadItemStatus.Completed : UploadItemStatus.Error,
                    Message = result.Message
                });
            }
            catch (Exception ex)
            {
                // Lỗi của file này chỉ cập nhật file đó, không làm dừng dispatcher.
                upload.Progress.Report(new UploadProgress
                {
                    Status = UploadItemStatus.Error,
                    Message = $"Upload lỗi: {ex.Message}"
                });
            }
            finally
            {
                _queueService.MarkCompleted(upload.FilePath);
                _availableSlots.Release();
            }
        }
    }
}
