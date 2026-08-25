using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using UDM10.Shared;

namespace UDM10.Server
{
    public class ClientConnectionHandler
    {
        private readonly TcpClient _client;
        private readonly ServerLogger _logger;
        private readonly FileStorageService _storageService;
        private readonly IConfiguration _config;

        public ClientConnectionHandler(
            TcpClient client,
            ServerLogger logger,
            FileStorageService storageService,
            IConfiguration config)
        {
            _client = client;
            _logger = logger;
            _storageService = storageService;
            _config = config;
        }

        public async Task HandleAsync(CancellationToken serverCancellationToken = default)
        {
            string clientEndPoint = _client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

            int receiveTimeoutMs = Math.Max(1, _config.GetValue<int>("Network:ReceiveTimeoutMs", 60000));
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(receiveTimeoutMs));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken, timeoutCts.Token);
            CancellationToken cancellationToken = linkedCts.Token;

            string requestId = string.Empty;

            try
            {
                using NetworkStream stream = _client.GetStream();

                UploadRequest? request = await ProtocolReader.ReadMetadataAsync<UploadRequest>(
                    stream,
                    cancellationToken);

                long maxAllowedSize = _config.GetValue<long>("Upload:MaxAllowedSizeInBytes", 10L * 1024 * 1024 * 1024);

                if (!MetadataValidator.IsValid(request!, maxAllowedSize, out ErrorCode errorCode, out string errorMessage))
                {
                    string currentRequestId = request?.RequestId ?? string.Empty;

                    await SendErrorAsync(
                        stream,
                        currentRequestId,
                        errorCode,
                        errorMessage,
                        cancellationToken);

                    return;
                }

                requestId = request!.RequestId;

                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    $"RequestId={requestId}, " +
                    $"File={request.FileName}, " +
                    $"Size={request.FileSize} bytes");

                UploadResponse readyResponse = new()
                {
                    RequestId = requestId,
                    Status = UploadStatus.Ready,
                    Error = ErrorCode.None,
                    Message = "Server sẵn sàng nhận file."
                };

                await ProtocolWriter.WriteMetadataAsync(
                    stream,
                    readyResponse,
                    cancellationToken);

                string savedPath = await _storageService.SaveFileAsync(
                    request.FileName!,
                    request.FileSize,
                    request.FileHash,
                    stream,
                    cancellationToken);

                UploadResponse completedResponse = new()
                {
                    RequestId = requestId,
                    Status = UploadStatus.Completed,
                    Error = ErrorCode.None,
                    Message = "Upload thành công."
                };

                await ProtocolWriter.WriteMetadataAsync(
                    stream,
                    completedResponse,
                    cancellationToken);

                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    $"Upload completed: " +
                    $"{savedPath}");
            }
            catch (OperationCanceledException ex)
            {
                if (serverCancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning($"[{clientEndPoint}] Session cancelled due to Server Graceful Shutdown.");
                }
                else if (ex.Message == "CLIENT_CANCELLED")
                {
                    _logger.LogWarning($"[{clientEndPoint}] Session cancelled explicitly by Client.");
                }
                else if (timeoutCts.IsCancellationRequested)
                {
                    _logger.LogError($"[{clientEndPoint}] Connection timed out. Client was inactive too long.");
                }
                else
                {
                    _logger.LogWarning($"[{clientEndPoint}] Upload operation was cancelled.");
                }
            }
            catch (EndOfStreamException ex)
            {
                _logger.LogError($"[{clientEndPoint}] Message bị cắt: {ex.Message}");
                await TrySendErrorAsync(requestId, ErrorCode.ConnectionLost, "Message bị cắt giữa chừng.");
            }
            catch (InvalidDataException ex)
            {
                _logger.LogError($"[{clientEndPoint}] Metadata không hợp lệ: {ex.Message}");
                await TrySendErrorAsync(requestId, ErrorCode.InvalidRequest, ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError($"[{clientEndPoint}] Connection lost: {ex.Message}");
                await TrySendErrorAsync(requestId, ErrorCode.ConnectionLost, "Mất kết nối trong quá trình upload.");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Checksum") || ex.Message.Contains("Hash"))
                {
                    _logger.LogError($"[{clientEndPoint}] Checksum mismatch: {ex.Message}");
                    await TrySendErrorAsync(requestId, ErrorCode.ChecksumMismatch, ex.Message);
                }
                else
                {
                    _logger.LogError($"[{clientEndPoint}] Error: {ex.Message}");
                    await TrySendErrorAsync(requestId, ErrorCode.UnknownError, "Lỗi không xác định từ Server.");
                }
            }
            finally
            {
                _client.Close();
            }

            async Task TrySendErrorAsync(
                string responseRequestId,
                ErrorCode errCode,
                string message)
            {
                try
                {
                    if (!_client.Connected) return;

                    await SendErrorAsync(
                        _client.GetStream(),
                        responseRequestId,
                        errCode,
                        message,
                        CancellationToken.None);
                }
                catch
                {

                }
            }
        }

        private static Task SendErrorAsync(
            NetworkStream stream,
            string requestId,
            ErrorCode errorCode,
            string message,
            CancellationToken cancellationToken)
        {
            UploadResponse response = new()
            {
                RequestId = requestId ?? string.Empty,
                Status = UploadStatus.Error,
                Error = errorCode,
                Message = message
            };

            return ProtocolWriter.WriteMetadataAsync(
                stream,
                response,
                cancellationToken);
        }
    }
}