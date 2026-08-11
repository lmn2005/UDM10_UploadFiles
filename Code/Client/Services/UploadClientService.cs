using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using UDM10.Shared;

namespace UDM10.Client
{
    public class UploadClientService : IUploadManager
    {
        private readonly string _serverIp = "127.0.0.1";
        private readonly int _port = 9000;
        private readonly int _chunkSize = ProtocolConstants.ChunkSize;

        public void EnqueueFile(string filePath, IProgress<UploadProgress> progress)
        {
            _ = Task.Run(async () => await UploadFileAsync(filePath, progress));
        }

        private async Task UploadFileAsync(string filePath, IProgress<UploadProgress> progress)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                progress.Report(new UploadProgress { Status = UploadItemStatus.Waiting, Message = "Đang kết nối Server..." });

                using TcpClient client = new TcpClient();
                await client.ConnectAsync(_serverIp, _port);
                using NetworkStream stream = client.GetStream();

                var request = new UploadRequest
                {
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length
                };

                await ProtocolWriter.WriteRequestAsync(stream, request);
                var response = await ProtocolReader.ReadResponseAsync(stream);

             
                if (response.Status == UploadStatus.Error)
                {
                    progress.Report(new UploadProgress { Status = UploadItemStatus.Error, Message = $"Từ chối: {response.Message}" });
                    return;
                }

                if (response.Status == UploadStatus.Ready)
                {
                    progress.Report(new UploadProgress { Status = UploadItemStatus.Uploading, Message = "Đang tải lên...", PercentComplete = 0 });

                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    byte[] buffer = new byte[_chunkSize];
                    int bytesRead;
                    long totalRead = 0;

                    
                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        double percent = (double)totalRead / fileInfo.Length * 100;
                        progress.Report(new UploadProgress { Status = UploadItemStatus.Uploading, PercentComplete = Math.Round(percent, 1), Message = "Đang truyền dữ liệu..." });
                    }

              
                    var completedResponse = await ProtocolReader.ReadResponseAsync(stream);
                    if (completedResponse.Status == UploadStatus.Completed)
                    {
                        progress.Report(new UploadProgress { Status = UploadItemStatus.Completed, PercentComplete = 100, Message = "Hoàn tất" });
                    }
                    else
                    {
                        progress.Report(new UploadProgress { Status = UploadItemStatus.Error, Message = completedResponse.Message ?? "Lỗi lưu file tại Server" });
                    }
                }
            }
            catch (Exception ex)
            {
                progress.Report(new UploadProgress { Status = UploadItemStatus.Error, Message = $"Lỗi mạng: {ex.Message}" });
            }
        }
    }
}