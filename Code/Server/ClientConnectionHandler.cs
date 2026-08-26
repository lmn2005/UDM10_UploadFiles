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
            string requestId = string.Empty;
            NetworkStream? stream = null;

            try
            {
                stream = _client.GetStream();

                UploadRequest? request;
                using (CancellationTokenSource metadataCts =
                    CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken))
                {
                    metadataCts.CancelAfter(receiveTimeoutMs);
                    try
                    {
                        request = await ProtocolReader.ReadRequestAsync(stream, metadataCts.Token);
                    }
                    catch (OperationCanceledException) when (!serverCancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException(
                            $"Không nhận được metadata trong {receiveTimeoutMs} ms.");
                    }
                }

                long maxAllowedSize = _config.GetValue<long>("Upload:MaxAllowedSizeInBytes", 10L * 1024 * 1024 * 1024);

                var validation = MetadataValidator.Validate(request, maxAllowedSize);
                if (!validation.IsValid)
                {
                    string currentRequestId = request?.RequestId ?? string.Empty;

                    await SendErrorAsync(
                        stream,
                        currentRequestId,
                        validation.ErrorCode,
                        validation.Message,
                        serverCancellationToken);

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
                    serverCancellationToken);

                string savedPath = await _storageService.SaveFileAsync(
                    request.FileName!,
                    request.FileSize,
                    request.FileHash,
                    stream,
                    receiveTimeoutMs,
                    serverCancellationToken);

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
                    serverCancellationToken);

                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    $"Upload completed: " +
                    $"{savedPath}");
            }
            catch (OperationCanceledException)
            {
                if (serverCancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning($"[{clientEndPoint}] Session cancelled due to Server Graceful Shutdown.");
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
                await TrySendErrorAsync(requestId, ErrorCode.InvalidMetadata, ex.Message);
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"[{clientEndPoint}] Timeout: {ex.Message}");
                await TrySendErrorAsync(requestId, ErrorCode.ConnectionLost, ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError($"[{clientEndPoint}] Connection lost: {ex.Message}");
                await TrySendErrorAsync(requestId, ErrorCode.ConnectionLost, "Mất kết nối trong quá trình upload.");
            }
            catch (ChecksumMismatchException ex)
            {
                _logger.LogError($"[{clientEndPoint}] Checksum mismatch: {ex.Message}");
                await TrySendErrorAsync(requestId, ErrorCode.ChecksumMismatch, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{clientEndPoint}] Error: {ex.Message}");
                await TrySendErrorAsync(requestId, ErrorCode.UnknownError, "Lỗi không xác định từ Server.");
            }
            finally
            {
                stream?.Dispose();
                _client.Close();
            }

            async Task TrySendErrorAsync(
                string responseRequestId,
                ErrorCode errCode,
                string message)
            {
                try
                {
                    if (stream is null || !_client.Connected) return;

                    await SendErrorAsync(
                        stream,
                        responseRequestId,
                        errCode,
                        message,
                        CancellationToken.None);
                }
                catch (Exception sendException)
                {
                    _logger.LogWarning(
                        $"[{clientEndPoint}] Không thể gửi lỗi {errCode} về client: " +
                        $"{sendException.Message}");
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
