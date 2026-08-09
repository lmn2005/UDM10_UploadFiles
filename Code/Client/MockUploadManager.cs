using System;
using System.Threading.Tasks;

namespace UDM10.Client
{
    public class MockUploadManager : IUploadManager
    {
        public void EnqueueFile(string filePath, IProgress<UploadProgress> progress)
        {
            _ = Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    await Task.Delay(300);
                    progress.Report(new UploadProgress
                    {
                        PercentComplete = i,
                        SpeedKBps = 512,
                        Status = i < 100 ? UploadItemStatus.Uploading : UploadItemStatus.Completed,
                        Message = i < 100 ? "Đang tải..." : "Hoàn tất"
                    });
                }
            });
        }
    }
}