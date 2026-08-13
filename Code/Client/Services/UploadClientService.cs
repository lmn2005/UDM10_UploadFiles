using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UDM10.Shared;

namespace UDM10.Client.Services
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
            CancellationToken cancellationToken = CancellationToken.None;

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

                await ProtocolWriter.WriteMetadataAsync(stream, request, cancellationToken);
                var readyResponse = await ProtocolReader.ReadMetadataAsync<UploadResponse>(stream, cancellationToken);

                if (readyResponse is null)
                {
                    progress.Report(new UploadProgress { Status = UploadItemStatus.Error, Message = "Server không phản hồi." });
                    return;
                }

                if (readyResponse.Status == UploadStatus.Error)
                {
                    progress.Report(new UploadProgress { Status = UploadItemStatus.Error, Message = $"Từ chối: {readyResponse.Message}" });
                    return;
                }

                if (readyResponse.Status != UploadStatus.Ready)
                {
                    progress.Report(new UploadProgress { Status = UploadItemStatus.Error, Message = "Server trả trạng thái không hợp lệ." });
                    return;
                }

                progress.Report(new UploadProgress { Status = UploadItemStatus.Uploading, Message = "Đang tải lên...", PercentComplete = 0 });

                ChunkedFileSender fileSender = new(_chunkSize);
                await fileSender.SendAsync(filePath, stream, cancellationToken);

                var finalResponse = await ProtocolReader.ReadMetadataAsync<UploadResponse>(stream, cancellationToken);

                if (finalResponse?.Status == UploadStatus.Completed)
                {
                    progress.Report(new UploadProgress { Status = UploadItemStatus.Completed, PercentComplete = 100, Message = "Hoàn tất" });
                }
                else
                {
                    progress.Report(new UploadProgress { Status = UploadItemStatus.Error, Message = finalResponse?.Message ?? "Lỗi lưu file tại Server" });
                }
            }
            catch (Exception ex)
            {
                progress.Report(new UploadProgress { Status = UploadItemStatus.Error, Message = $"Lỗi mạng: {ex.Message}" });
            }
        }
    }
}