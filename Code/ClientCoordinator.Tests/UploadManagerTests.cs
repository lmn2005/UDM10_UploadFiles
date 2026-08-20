using System.Collections.Concurrent;
using System.Diagnostics;
using UDM10.Client;
using UDM10.Client.Services;

namespace UDM10.ClientCoordinator.Tests;

[TestClass]
public sealed class UploadManagerTests
{
    [TestMethod]
    public async Task EnqueueFile_limits_uploads_to_three_and_starts_next_file_when_a_slot_is_free()
    {
        var uploadClient = new ControlledUploadClient();
        await using var manager = new UploadManager(uploadClient, maxConcurrentFiles: 3);
        var progress = CreateProgresses(4);

        foreach ((string filePath, RecordingProgress reporter) in progress)
        {
            manager.EnqueueFile(filePath, reporter, CancellationToken.None);
        }

        await WaitUntilAsync(() => uploadClient.StartedCount == 3);
        Assert.AreEqual(3, uploadClient.MaxConcurrentUploads);

        uploadClient.CompleteSuccess(uploadClient.StartedPaths[0]);
        await WaitUntilAsync(() => uploadClient.StartedCount == 4);
        Assert.IsLessThanOrEqualTo(uploadClient.MaxConcurrentUploads, 3);

        uploadClient.CompleteAllSuccessfully();
        await WaitUntilAsync(() => progress.All(item => item.Reporter.HasStatus(UploadItemStatus.Completed)));
    }

    [TestMethod]
    public async Task EnqueueFile_rejects_a_path_that_is_already_queued_or_uploading()
    {
        var uploadClient = new ControlledUploadClient();
        await using var manager = new UploadManager(uploadClient, maxConcurrentFiles: 1);
        string filePath = CreateFilePath("duplicate.txt");
        var firstProgress = new RecordingProgress();
        var duplicateProgress = new RecordingProgress();

        manager.EnqueueFile(filePath, firstProgress, CancellationToken.None);
        manager.EnqueueFile(filePath, duplicateProgress, CancellationToken.None);

        await WaitUntilAsync(() => uploadClient.StartedCount == 1);
        Assert.IsTrue(duplicateProgress.HasStatus(UploadItemStatus.Error));
        Assert.AreEqual(1, uploadClient.StartedCount);

        uploadClient.CompleteSuccess(uploadClient.StartedPaths[0]);
        await WaitUntilAsync(() => firstProgress.HasStatus(UploadItemStatus.Completed));
    }

    [TestMethod]
    public async Task Failed_upload_releases_its_slot_and_does_not_stop_the_queue()
    {
        var uploadClient = new ControlledUploadClient();
        await using var manager = new UploadManager(uploadClient, maxConcurrentFiles: 1);
        var failedProgress = new RecordingProgress();
        var succeedingProgress = new RecordingProgress();

        manager.EnqueueFile(CreateFilePath("failed.txt"), failedProgress, CancellationToken.None);
        manager.EnqueueFile(CreateFilePath("succeeds.txt"), succeedingProgress, CancellationToken.None);

        await WaitUntilAsync(() => uploadClient.StartedCount == 1);
        uploadClient.CompleteFailure(uploadClient.StartedPaths[0], "Server không nhận file.");

        await WaitUntilAsync(() => uploadClient.StartedCount == 2);
        uploadClient.CompleteSuccess(uploadClient.StartedPaths[1]);

        await WaitUntilAsync(() => failedProgress.HasStatus(UploadItemStatus.Error)
            && succeedingProgress.HasStatus(UploadItemStatus.Completed));
    }

    [TestMethod]
    public async Task Cancelling_an_active_upload_releases_its_slot_and_starts_the_next_file()
    {
        var uploadClient = new ControlledUploadClient();
        await using var manager = new UploadManager(uploadClient, maxConcurrentFiles: 1);
        using var firstCancellation = new CancellationTokenSource();
        var firstProgress = new RecordingProgress();
        var secondProgress = new RecordingProgress();

        manager.EnqueueFile(CreateFilePath("cancelled.txt"), firstProgress, firstCancellation.Token);
        manager.EnqueueFile(CreateFilePath("next.txt"), secondProgress, CancellationToken.None);

        await WaitUntilAsync(() => uploadClient.StartedCount == 1);
        firstCancellation.Cancel();

        await WaitUntilAsync(() => firstProgress.HasStatus(UploadItemStatus.Cancelled)
            && uploadClient.StartedCount == 2);
        Assert.AreEqual(1, uploadClient.MaxConcurrentUploads);

        uploadClient.CompleteSuccess(uploadClient.StartedPaths[1]);
        await WaitUntilAsync(() => secondProgress.HasStatus(UploadItemStatus.Completed));
    }

    [TestMethod]
    public async Task Cancelling_a_waiting_upload_does_not_open_a_connection_for_it()
    {
        var uploadClient = new ControlledUploadClient();
        await using var manager = new UploadManager(uploadClient, maxConcurrentFiles: 1);
        using var waitingCancellation = new CancellationTokenSource();
        var firstProgress = new RecordingProgress();
        var waitingProgress = new RecordingProgress();
        var lastProgress = new RecordingProgress();

        manager.EnqueueFile(CreateFilePath("active.txt"), firstProgress, CancellationToken.None);
        manager.EnqueueFile(CreateFilePath("waiting.txt"), waitingProgress, waitingCancellation.Token);
        manager.EnqueueFile(CreateFilePath("last.txt"), lastProgress, CancellationToken.None);

        await WaitUntilAsync(() => uploadClient.StartedCount == 1);
        waitingCancellation.Cancel();
        await WaitUntilAsync(() => waitingProgress.HasStatus(UploadItemStatus.Cancelled));
        Assert.AreEqual(1, uploadClient.StartedCount);

        uploadClient.CompleteSuccess(uploadClient.StartedPaths[0]);
        await WaitUntilAsync(() => uploadClient.StartedCount == 2);
        uploadClient.CompleteSuccess(uploadClient.StartedPaths[1]);

        await WaitUntilAsync(() => firstProgress.HasStatus(UploadItemStatus.Completed)
            && lastProgress.HasStatus(UploadItemStatus.Completed));
    }

    [TestMethod]
    public async Task Failed_upload_can_be_retried_from_the_beginning_without_a_duplicate_task()
    {
        var uploadClient = new ControlledUploadClient();
        await using var manager = new UploadManager(uploadClient, maxConcurrentFiles: 1);
        string filePath = CreateFilePath("retry.txt");
        var firstProgress = new RecordingProgress();
        var retryProgress = new RecordingProgress();

        manager.EnqueueFile(filePath, firstProgress, CancellationToken.None);
        await WaitUntilAsync(() => uploadClient.StartedCount == 1);
        uploadClient.CompleteFailure(uploadClient.StartedPaths[0], "Kết nối bị lỗi.");
        await WaitUntilAsync(() => firstProgress.HasStatus(UploadItemStatus.Error));

        manager.EnqueueFile(filePath, retryProgress, CancellationToken.None);
        await WaitUntilAsync(() => uploadClient.StartedCount == 2);
        uploadClient.CompleteSuccess(uploadClient.StartedPaths[1]);

        await WaitUntilAsync(() => retryProgress.HasStatus(UploadItemStatus.Completed));
        Assert.AreEqual(2, uploadClient.StartedPaths.Count(path => path == filePath));
        Assert.AreEqual(1, uploadClient.MaxConcurrentUploads);
    }

    [TestMethod]
    public async Task Terminal_callback_can_retry_immediately_after_the_old_task_is_untracked()
    {
        var uploadClient = new ControlledUploadClient();
        await using var manager = new UploadManager(uploadClient, maxConcurrentFiles: 1);
        string filePath = CreateFilePath("immediate-retry.txt");
        var retryProgress = new RecordingProgress();
        int activeCountSeenByCallback = -1;

        var firstProgress = new CallbackProgress(report =>
        {
            if (report.Status != UploadItemStatus.Error)
            {
                return;
            }

            Volatile.Write(ref activeCountSeenByCallback, manager.ActiveUploadCount);
            manager.EnqueueFile(filePath, retryProgress, CancellationToken.None);
        });

        manager.EnqueueFile(filePath, firstProgress, CancellationToken.None);
        await WaitUntilAsync(() => uploadClient.StartedCount == 1);
        uploadClient.CompleteFailure(uploadClient.StartedPaths[0], "Lỗi để Retry ngay.");

        await WaitUntilAsync(() => uploadClient.StartedCount == 2);
        Assert.AreEqual(0, Volatile.Read(ref activeCountSeenByCallback));
        Assert.AreEqual(1, uploadClient.MaxConcurrentUploads);

        uploadClient.CompleteSuccess(uploadClient.StartedPaths[1]);
        await WaitUntilAsync(() => retryProgress.HasStatus(UploadItemStatus.Completed));
    }

    [TestMethod]
    public async Task Scheduler_handles_many_simultaneous_terminal_states_without_leaking_slots()
    {
        const int fileCount = 90;
        var uploadClient = new RacingUploadClient();
        await using var manager = new UploadManager(uploadClient, maxConcurrentFiles: 3);
        var progress = new List<RecordingProgress>(fileCount);
        var cancellations = new List<CancellationTokenSource>();

        for (int index = 0; index < fileCount; index++)
        {
            string outcome = (index % 3) switch
            {
                0 => "completed",
                1 => "error",
                _ => "cancelled"
            };

            var reporter = new RecordingProgress();
            var cancellation = new CancellationTokenSource();
            progress.Add(reporter);
            cancellations.Add(cancellation);

            manager.EnqueueFile(
                CreateFilePath($"{outcome}-{index}.txt"),
                reporter,
                cancellation.Token);
        }

        await Task.Delay(15);
        for (int index = 2; index < fileCount; index += 3)
        {
            cancellations[index].Cancel();
        }

        await WaitUntilAsync(() => progress.All(reporter => reporter.HasTerminalStatus()), TimeSpan.FromSeconds(8));
        await WaitUntilAsync(() => manager.ActiveUploadCount == 0);

        Assert.IsLessThanOrEqualTo(uploadClient.MaxConcurrentUploads, 3);
        Assert.AreEqual(0, uploadClient.ActiveUploads);
        Assert.AreEqual(3, manager.AvailableSlotCount);
        Assert.IsTrue(uploadClient.StartCounts.Values.All(count => count == 1));

        foreach (CancellationTokenSource cancellation in cancellations)
        {
            cancellation.Dispose();
        }
    }

    [TestMethod]
    public async Task Disposing_the_scheduler_cancels_and_waits_for_all_active_uploads()
    {
        var uploadClient = new ControlledUploadClient();
        var manager = new UploadManager(uploadClient, maxConcurrentFiles: 3);

        for (int index = 0; index < 8; index++)
        {
            manager.EnqueueFile(
                CreateFilePath($"shutdown-{index}.txt"),
                new RecordingProgress(),
                CancellationToken.None);
        }

        await WaitUntilAsync(() => uploadClient.StartedCount == 3);
        await manager.DisposeAsync();

        Assert.AreEqual(0, uploadClient.ActiveUploads);
        Assert.AreEqual(0, manager.ActiveUploadCount);
    }

    private static IReadOnlyList<(string FilePath, RecordingProgress Reporter)> CreateProgresses(int count)
        => Enumerable.Range(1, count)
            .Select(index => (CreateFilePath($"file-{index}.txt"), new RecordingProgress()))
            .ToArray();

    private static string CreateFilePath(string fileName)
        => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), fileName);

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < (timeout ?? TimeSpan.FromSeconds(3)))
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Điều kiện không đạt trong thời gian chờ kiểm thử.");
    }

    private sealed class RecordingProgress : IProgress<UploadProgress>
    {
        private readonly ConcurrentQueue<UploadProgress> _reports = new();

        public void Report(UploadProgress value) => _reports.Enqueue(value);

        public bool HasStatus(UploadItemStatus status)
            => _reports.Any(report => report.Status == status);

        public bool HasTerminalStatus()
            => _reports.Any(report => report.Status is UploadItemStatus.Completed
                or UploadItemStatus.Error
                or UploadItemStatus.Cancelled);
    }

    private sealed class CallbackProgress : IProgress<UploadProgress>
    {
        private readonly Action<UploadProgress> _callback;

        public CallbackProgress(Action<UploadProgress> callback)
        {
            _callback = callback;
        }

        public void Report(UploadProgress value) => _callback(value);
    }

    private sealed class ControlledUploadClient : IUploadClient
    {
        private readonly object _syncRoot = new();
        private readonly List<string> _startedPaths = new();
        private readonly Dictionary<string, TaskCompletionSource<UploadResult>> _pendingUploads = new();
        private int _activeUploads;

        public int MaxConcurrentUploads { get; private set; }
        public int ActiveUploads
        {
            get
            {
                lock (_syncRoot)
                {
                    return _activeUploads;
                }
            }
        }

        public int StartedCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _startedPaths.Count;
                }
            }
        }

        public IReadOnlyList<string> StartedPaths
        {
            get
            {
                lock (_syncRoot)
                {
                    return _startedPaths.ToArray();
                }
            }
        }

        public async Task<UploadResult> UploadFileAsync(
            string filePath,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<UploadResult> completion;
            lock (_syncRoot)
            {
                completion = new TaskCompletionSource<UploadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _startedPaths.Add(filePath);
                _pendingUploads.Add(filePath, completion);
                _activeUploads++;
                MaxConcurrentUploads = Math.Max(MaxConcurrentUploads, _activeUploads);
            }

            progress?.Report(new UploadProgress
            {
                Status = UploadItemStatus.Uploading,
                Message = "Đang gửi file..."
            });

            try
            {
                return await completion.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                lock (_syncRoot)
                {
                    _activeUploads--;
                    _pendingUploads.Remove(filePath);
                }
            }
        }

        public void CompleteSuccess(string filePath)
            => Complete(filePath, UploadResult.Success("Upload thành công."));

        public void CompleteFailure(string filePath, string message)
            => Complete(filePath, UploadResult.Fail(message));

        public void CompleteAllSuccessfully()
        {
            string[] pendingPaths;
            lock (_syncRoot)
            {
                pendingPaths = _pendingUploads.Keys.ToArray();
            }

            foreach (string filePath in pendingPaths)
            {
                CompleteSuccess(filePath);
            }
        }

        private void Complete(string filePath, UploadResult result)
        {
            TaskCompletionSource<UploadResult>? completion;
            lock (_syncRoot)
            {
                _pendingUploads.TryGetValue(filePath, out completion);
            }

            Assert.IsNotNull(completion, $"Không tìm thấy upload đang chờ: {filePath}");
            completion.TrySetResult(result);
        }
    }

    private sealed class RacingUploadClient : IUploadClient
    {
        private int _activeUploads;
        private int _maxConcurrentUploads;

        public ConcurrentDictionary<string, int> StartCounts { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public int ActiveUploads => Volatile.Read(ref _activeUploads);
        public int MaxConcurrentUploads => Volatile.Read(ref _maxConcurrentUploads);

        public async Task<UploadResult> UploadFileAsync(
            string filePath,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            StartCounts.AddOrUpdate(filePath, 1, (_, count) => count + 1);
            int active = Interlocked.Increment(ref _activeUploads);
            UpdateMaximum(active);

            progress?.Report(new UploadProgress
            {
                Status = UploadItemStatus.Uploading,
                BytesTransferred = 0,
                Message = "Đang upload..."
            });

            try
            {
                await Task.Delay(4 + Math.Abs(filePath.GetHashCode()) % 8, cancellationToken);

                if (Path.GetFileName(filePath).StartsWith("error-", StringComparison.Ordinal))
                {
                    return UploadResult.Fail("Lỗi mô phỏng.");
                }

                return UploadResult.Success("Hoàn tất.");
            }
            finally
            {
                Interlocked.Decrement(ref _activeUploads);
            }
        }

        private void UpdateMaximum(int candidate)
        {
            int observed;
            while (candidate > (observed = Volatile.Read(ref _maxConcurrentUploads)))
            {
                if (Interlocked.CompareExchange(ref _maxConcurrentUploads, candidate, observed) == observed)
                {
                    return;
                }
            }
        }
    }
}
