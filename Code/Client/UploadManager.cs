using System.Collections.Concurrent;
using UDM10.Client.Services;

namespace UDM10.Client
{
    internal sealed class UploadManager : IUploadManager
    {
        private readonly IUploadClient _uploadClient;
        private readonly UploadQueueService _queueService;
        private readonly SemaphoreSlim _availableSlots;
        private readonly SemaphoreSlim _queueSignal = new(0);
        private readonly CancellationTokenSource _shutdownCts = new();
        private readonly ConcurrentDictionary<string, ActiveUpload> _activeUploads =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Task _dispatcherTask;
        private int _disposeState;

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

            _dispatcherTask = Task.Run(() => DispatchQueueAsync(_shutdownCts.Token));
        }

        public void EnqueueFile(string filePath, IProgress<UploadProgress> progress, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _disposeState) != 0)
            {
                SafeReport(progress, new UploadProgress
                {
                    Status = UploadItemStatus.Error,
                    Message = "Bộ điều phối upload đã đóng."
                });
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                ReportCancelled(progress);
                return;
            }

            if (!_queueService.TryEnqueue(filePath, progress, cancellationToken))
            {
                SafeReport(progress, new UploadProgress
                {
                    Status = UploadItemStatus.Error,
                    Message = "File đã có trong hàng đợi hoặc đường dẫn không hợp lệ."
                });
                return;
            }

            SafeReport(progress, new UploadProgress
            {
                BytesTransferred = 0,
                Status = UploadItemStatus.Waiting,
                Message = "Đang chờ lượt upload..."
            });

            _queueSignal.Release();
        }

        private async Task DispatchQueueAsync(CancellationToken shutdownToken)
        {
            try
            {
                while (true)
                {
                    await _queueSignal.WaitAsync(shutdownToken);

                    if (!_queueService.TryDequeue(out UploadQueueService.QueuedUpload? upload))
                    {
                        continue;
                    }

                    if (upload!.CancellationToken.IsCancellationRequested || shutdownToken.IsCancellationRequested)
                    {
                        CompleteCancelledWithoutSlot(upload);
                        continue;
                    }

                    try
                    {
                        using CancellationTokenSource slotWaitCts =
                            CancellationTokenSource.CreateLinkedTokenSource(upload.CancellationToken, shutdownToken);
                        await _availableSlots.WaitAsync(slotWaitCts.Token);
                    }
                    catch (OperationCanceledException)
                        when (upload.CancellationToken.IsCancellationRequested || shutdownToken.IsCancellationRequested)
                    {
                        CompleteCancelledWithoutSlot(upload);
                        continue;
                    }

                    StartTrackedUpload(upload, shutdownToken);
                }
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                // DisposeAsync chủ động dừng dispatcher.
            }
        }

        private void StartTrackedUpload(UploadQueueService.QueuedUpload upload, CancellationToken shutdownToken)
        {
            var registration = new ActiveUpload();
            if (!_activeUploads.TryAdd(upload.FilePath, registration))
            {
                _queueService.MarkCompleted(upload.FilePath);
                _availableSlots.Release();
                SafeReport(upload.Progress, new UploadProgress
                {
                    Status = UploadItemStatus.Error,
                    Message = "File đang được upload bởi một task khác."
                });
                return;
            }

            _ = RunTrackedUploadAsync(upload, registration, shutdownToken);
        }

        private async Task RunTrackedUploadAsync(
            UploadQueueService.QueuedUpload upload,
            ActiveUpload registration,
            CancellationToken shutdownToken)
        {
            UploadProgress terminalProgress;

            try
            {
                terminalProgress = await ProcessUploadAsync(upload, shutdownToken);
            }
            catch (Exception ex)
            {
                // Phòng thủ cho lỗi ngoài dự kiến; ProcessUploadAsync vẫn chịu trách nhiệm cleanup slot.
                terminalProgress = new UploadProgress
                {
                    Status = UploadItemStatus.Error,
                    Message = $"Upload lỗi: {ex.Message}"
                };
            }
            finally
            {
                // Gỡ task trước khi báo trạng thái cuối để callback Retry có thể enqueue ngay
                // mà không đụng task cũ vẫn còn được đánh dấu active.
                _activeUploads.TryRemove(upload.FilePath, out _);
                registration.Completion.TrySetResult();
            }

            SafeReport(upload.Progress, terminalProgress);
        }

        private async Task<UploadProgress> ProcessUploadAsync(
            UploadQueueService.QueuedUpload upload,
            CancellationToken shutdownToken)
        {
            UploadProgress terminalProgress;
            using CancellationTokenSource uploadCts =
                CancellationTokenSource.CreateLinkedTokenSource(upload.CancellationToken, shutdownToken);
            CancellationToken effectiveToken = uploadCts.Token;

            try
            {
                effectiveToken.ThrowIfCancellationRequested();

                UploadResult result = await _uploadClient.UploadFileAsync(
                    upload.FilePath,
                    upload.Progress,
                    effectiveToken);

                terminalProgress = effectiveToken.IsCancellationRequested
                    ? CreateCancelledProgress()
                    : new UploadProgress
                    {
                        PercentComplete = result.IsSuccess ? 100 : 0,
                        Status = result.IsSuccess ? UploadItemStatus.Completed : UploadItemStatus.Error,
                        Message = result.Message
                    };
            }
            catch (OperationCanceledException) when (effectiveToken.IsCancellationRequested)
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
                // Gỡ path khỏi queue và trả đúng một slot; task active được gỡ ở RunTrackedUploadAsync.
                _queueService.MarkCompleted(upload.FilePath);
                _availableSlots.Release();
            }

            return terminalProgress;
        }

        private void CompleteCancelledWithoutSlot(UploadQueueService.QueuedUpload upload)
        {
            _queueService.MarkCompleted(upload.FilePath);
            ReportCancelled(upload.Progress);
        }

        private static void ReportCancelled(IProgress<UploadProgress> progress)
            => SafeReport(progress, CreateCancelledProgress());

        private static UploadProgress CreateCancelledProgress()
            => new()
            {
                PercentComplete = 0,
                SpeedKBps = 0,
                Status = UploadItemStatus.Cancelled,
                Message = "Đã hủy upload."
            };

        internal int ActiveUploadCount => _activeUploads.Count;
        internal int AvailableSlotCount => _availableSlots.CurrentCount;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            {
                return;
            }

            _shutdownCts.Cancel();

            try
            {
                await _dispatcherTask;
            }
            catch (OperationCanceledException)
            {
            }

            while (_queueService.TryDequeue(out UploadQueueService.QueuedUpload? queuedUpload))
            {
                CompleteCancelledWithoutSlot(queuedUpload!);
            }

            Task[] activeTasks = _activeUploads.Values
                .Select(active => active.Completion.Task)
                .ToArray();

            try
            {
                await Task.WhenAll(activeTasks);
            }
            catch
            {
                // Mỗi task đã tự cleanup; không để một lỗi làm gián đoạn quá trình đóng Client.
            }

            _queueSignal.Dispose();
            _availableSlots.Dispose();
            _shutdownCts.Dispose();
        }

        private static void SafeReport(IProgress<UploadProgress> progress, UploadProgress value)
        {
            try
            {
                progress.Report(value);
            }
            catch
            {
                // Callback UI lỗi không được làm chết dispatcher hoặc giữ slot vĩnh viễn.
            }
        }

        private sealed class ActiveUpload
        {
            public TaskCompletionSource Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static int ResolveMaxConcurrentFiles(int configuredMax)
            => configuredMax > 0 ? Math.Min(configuredMax, 3) : 3;
    }
}
