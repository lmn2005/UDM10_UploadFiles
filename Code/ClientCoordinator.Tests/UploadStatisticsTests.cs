using UDM10.Client;

namespace UDM10.ClientCoordinator.Tests;

[TestClass]
public sealed class UploadStatisticsTests
{
    [TestMethod]
    public async Task Statistics_count_terminal_states_bytes_elapsed_time_and_average_speed()
    {
        var statistics = new UploadStatistics();
        string completedPath = CreateFilePath("completed.txt");
        string errorPath = CreateFilePath("error.txt");
        string cancelledPath = CreateFilePath("cancelled.txt");

        statistics.RegisterFile(completedPath, 1024);
        statistics.RegisterFile(errorPath, 2048);
        statistics.RegisterFile(cancelledPath, 4096);

        await Task.Delay(20);
        statistics.UpdateFile(completedPath, UploadItemStatus.Completed, 1024);
        statistics.UpdateFile(errorPath, UploadItemStatus.Error, 1024);
        statistics.UpdateFile(cancelledPath, UploadItemStatus.Cancelled, 512);
        statistics.RefreshElapsed();

        Assert.AreEqual(3, statistics.TotalFiles);
        Assert.AreEqual(1, statistics.CompletedFiles);
        Assert.AreEqual(1, statistics.ErrorFiles);
        Assert.AreEqual(1, statistics.CancelledFiles);
        Assert.AreEqual(7168L, statistics.TotalBytes);
        Assert.AreEqual(2560L, statistics.TransferredBytes);
        Assert.IsGreaterThan(TimeSpan.Zero, statistics.Elapsed);
        Assert.IsGreaterThan(0d, statistics.AverageSpeedKBps);
    }

    [TestMethod]
    public void Retry_removes_the_old_error_and_restarts_metrics_from_zero()
    {
        var statistics = new UploadStatistics();
        string filePath = CreateFilePath("retry.txt");

        statistics.RegisterFile(filePath, 4096);
        statistics.UpdateFile(filePath, UploadItemStatus.Error, 2048);
        Assert.AreEqual(1, statistics.ErrorFiles);

        statistics.ResetForRetry(filePath);
        Assert.AreEqual(0, statistics.ErrorFiles);
        Assert.AreEqual(0L, statistics.TransferredBytes);

        statistics.UpdateFile(filePath, UploadItemStatus.Completed, 4096);
        Assert.AreEqual(1, statistics.CompletedFiles);
        Assert.AreEqual(4096L, statistics.TransferredBytes);
    }

    private static string CreateFilePath(string fileName)
        => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), fileName);
}
