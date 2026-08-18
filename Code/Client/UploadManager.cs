using UDM10.Client.Services;

namespace UDM10.Client
{
    internal sealed class UploadManager : IUploadManager
    {
        private readonly IUploadClient _uploadClient;
        private readonly UploadQueueService _queueService;
        private readonly SemaphoreSlim _availableSlots;
        private readonly SemaphoreSlim _queueSignal = new(0);

        public UploadManager()
            : this(ClientSettings.Load())
        {
        }

        internal UploadManager(ClientSettings settings)
            : this(
                new UploadClientService(settings ?? throw new ArgumentNullException(nameof(settings))),
                ResolveMaxConcurrentFiles(settings.Upload.MaxConcurrentFiles))
        {
        }

        // Constructor này tách phần điều phối khỏi TCP để kiểm thử được queue và giới hạn đồng thời.
        internal UploadManager(IUploadClient uploadClient, int maxConcurrentFiles)
        {
            _uploadClient = uploadClient ?? throw new ArgumentNullException(nameof(uploadClient));
            _queueService = new UploadQueueService();
            int normalizedMax = ResolveMaxConcurrentFiles(maxConcurrentFiles);
            _availableSlots = new SemaphoreSlim(normalizedMax, normalizedMax);

            _ = Task.Run(DispatchQueueAsync);
        }

        public void EnqueueFile(string filePath, IProgress<UploadProgress> progress, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                ReportCancelled(progress);
                return;
            }

            if (!_queueService.TryEnqueue(filePath, progress, cancellationToken))
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

                if (upload!.CancellationToken.IsCancellationRequested)
                {
                    CompleteCancelledWithoutSlot(upload);
                    continue;
                }

                try
                {
                    await _availableSlots.WaitAsync(upload.CancellationToken);
                }
                catch (OperationCanceledException) when (upload.CancellationToken.IsCancellationRequested)
                {
                    CompleteCancelledWithoutSlot(upload);
                    continue;
                }

                _ = ProcessUploadAsync(upload!);
            }
        }

        private async Task ProcessUploadAsync(UploadQueueService.QueuedUpload upload)
        {
            UploadProgress terminalProgress;

            try
            {
                upload.CancellationToken.ThrowIfCancellationRequested();

                UploadResult result = await _uploadClient.UploadFileAsync(
                    upload.FilePath,
                    upload.Progress,
                    upload.CancellationToken);

                terminalProgress = upload.CancellationToken.IsCancellationRequested
                    ? CreateCancelledProgress()
                    : new UploadProgress
                    {
                        PercentComplete = result.IsSuccess ? 100 : 0,
                        Status = result.IsSuccess ? UploadItemStatus.Completed : UploadItemStatus.Error,
                        Message = result.Message
                    };
            }
            catch (OperationCanceledException) when (upload.CancellationToken.IsCancellationRequested)
            {
                terminalProgress = CreateCancelledProgress();
            }
            catch (Exception ex)
            {
                // Lỗi của file này chỉ cập nhật file đó, không làm dừng dispatcher.
                terminalProgress = new UploadProgress
                {
                    Status = UploadItemStatus.Error,
                    Message = $"Upload lỗi: {ex.Message}"
                };
            }
            finally
            {
                // Gỡ dấu theo dõi trước khi báo trạng thái cuối để người dùng có thể Retry ngay.
                _queueService.MarkCompleted(upload.FilePath);
                _availableSlots.Release();
            }

            upload.Progress.Report(terminalProgress);
        }

        private void CompleteCancelledWithoutSlot(UploadQueueService.QueuedUpload upload)
        {
            _queueService.MarkCompleted(upload.FilePath);
            ReportCancelled(upload.Progress);
        }

        private static void ReportCancelled(IProgress<UploadProgress> progress)
            => progress.Report(CreateCancelledProgress());

        private static UploadProgress CreateCancelledProgress()
            => new()
            {
                PercentComplete = 0,
                SpeedKBps = 0,
                Status = UploadItemStatus.Cancelled,
                Message = "Đã hủy upload."
            };

        private static int ResolveMaxConcurrentFiles(int configuredMax)
            => configuredMax > 0 ? Math.Min(configuredMax, 3) : 3;
    }
}
