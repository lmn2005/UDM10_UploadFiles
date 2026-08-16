using System;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Client
{
    public class MockUploadManager : IUploadManager
    {
        public void EnqueueFile(string filePath, IProgress<UploadProgress> progress, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i <= 100; i += 10)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(300, cancellationToken);
                        progress.Report(new UploadProgress
                        {
                            PercentComplete = i,
                            SpeedKBps = 512,
                            Status = i < 100 ? UploadItemStatus.Uploading : UploadItemStatus.Completed,
                            Message = i < 100 ? "Đang tải..." : "Hoàn tất"
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    progress.Report(new UploadProgress
                    {
                        Status = UploadItemStatus.Cancelled,
                        Message = "Đã hủy bởi người dùng"
                    });
                }
            }, cancellationToken);
        }
    }
}