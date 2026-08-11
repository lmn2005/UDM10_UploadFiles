using System.IO;
using System.Net.Sockets;
using UDM10.Shared;

namespace UDM10.Client.Services
{
    internal sealed class UploadClientService
    {
        private readonly ClientSettings _settings;

        public UploadClientService()
        {
            _settings = ClientSettings.Load();
        }

        public async Task<UploadResult> UploadFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return UploadResult.Fail("File không tồn tại.");
            }

            FileInfo fileInfo = new(filePath);

            try
            {
                using TcpClient client = new();
                using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(_settings.Network.ConnectTimeoutMs);

                await client.ConnectAsync(_settings.Network.ServerIp, _settings.Network.Port, connectCts.Token);

                client.ReceiveTimeout = _settings.Network.ReceiveTimeoutMs;
                client.SendTimeout = _settings.Network.ReceiveTimeoutMs;

                await using NetworkStream networkStream = client.GetStream();

                UploadRequest request = new()
                {
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length
                };

                await ProtocolWriter.WriteMetadataAsync(networkStream, request, cancellationToken);

                UploadResponse? readyResponse = await ReadResponseAsync(networkStream, cancellationToken);
                if (readyResponse?.Status == UploadStatus.Error)
                {
                    return UploadResult.Fail(string.IsNullOrWhiteSpace(readyResponse?.Message)
                        ? "Server chưa sẵn sàng nhận file."
                        : readyResponse.Message);
                }

                int chunkSize = _settings.Upload.ChunkSizeBytes > 0 ? _settings.Upload.ChunkSizeBytes : 65536;
                ChunkedFileSender fileSender = new(chunkSize);
                await fileSender.SendAsync(filePath, networkStream, cancellationToken);

                UploadResponse? finalResponse = await ReadResponseAsync(networkStream, cancellationToken);
                if (finalResponse?.Status == UploadStatus.Completed)
                {
                    return UploadResult.Success(string.IsNullOrWhiteSpace(finalResponse.Message)
                        ? $"Upload thành công: {fileInfo.Name}"
                        : finalResponse.Message);
                }

                if (finalResponse is null)
                {
                    return UploadResult.Fail("Server không trả kết quả cuối.");
                }

                if (!string.IsNullOrWhiteSpace(finalResponse.Message))
                {
                    return UploadResult.Fail(finalResponse.Message);
                }

                return UploadResult.Fail("Upload thất bại.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return UploadResult.Fail("Đã hủy upload.");
            }
            catch (OperationCanceledException)
            {
                return UploadResult.Fail("Không kết nối được Server. Kiểm tra Server đã bật và đúng port.");
            }
            catch (SocketException)
            {
                return UploadResult.Fail("Không kết nối được Server. Kiểm tra Server đã bật và đúng port.");
            }
            catch (TimeoutException)
            {
                return UploadResult.Fail("Server phản hồi quá thời gian chờ.");
            }
            catch (UnauthorizedAccessException)
            {
                return UploadResult.Fail("Không có quyền đọc file.");
            }
            catch (InvalidDataException)
            {
                return UploadResult.Fail("Server phản hồi không đúng định dạng.");
            }
            catch (EndOfStreamException)
            {
                return UploadResult.Fail("Server đóng kết nối trước khi trả kết quả.");
            }
            catch (IOException)
            {
                return UploadResult.Fail("Mất kết nối hoặc không đọc được file.");
            }
            catch (Exception ex)
            {
                return UploadResult.Fail($"Upload lỗi: {ex.Message}");
            }
        }

        private async Task<UploadResponse?> ReadResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_settings.Network.ReceiveTimeoutMs);

            try
            {
                return await ProtocolReader.ReadMetadataAsync<UploadResponse>(stream, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }
    }
}