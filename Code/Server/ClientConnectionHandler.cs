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

            int receiveTimeoutMs = Math.Max(1, _config.GetValue<int>("Network:ReceiveTimeoutMs", 30000));
            int errorSendTimeoutMs = Math.Max(1000, _config.GetValue<int>("Network:ErrorSendTimeoutMs", 3000));

            string requestId = "N/A";
            string fileName = "N/A";
            long bytesTransferred = 0;
            NetworkStream? stream = null;

            _logger.LogUploadEvent(
                UploadLifecycleEvent.Connect,
                requestId,
                clientEndPoint,
                fileName,
                bytesTransferred,
                "Client kết nối thành công.");

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
                        throw new TimeoutException($"Không nhận được metadata trong {receiveTimeoutMs} ms.");
                    }
                }

                long maxAllowedSize = _config.GetValue<long>("Upload:MaxAllowedSizeInBytes", 10L * 1024 * 1024 * 1024);

                var validation = MetadataValidator.Validate(request, maxAllowedSize);
                if (!validation.IsValid)
                {
                    string currentRequestId = request?.RequestId ?? "N/A";
                    await SendErrorAsync(
                        stream,
                        currentRequestId,
                        validation.ErrorCode,
                        validation.Message,
                        serverCancellationToken,
                        errorSendTimeoutMs);
                    return;
                }

                requestId = request!.RequestId;
                fileName = request.FileName ?? "N/A";

                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Start,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    $"FileSize={request.FileSize} bytes");

                UploadResponse readyResponse = new()
                {
                    ProtocolVersion = ProtocolConstants.CurrentVersion,
                    RequestId = requestId,
                    Status = UploadStatus.Ready,
                    ErrorCode = ErrorCode.None,
                    ErrorMessage = "Server sẵn sàng nhận file.",
                    SavedFileName = null
                };

                var readyValidation = MetadataValidator.ValidateResponse(readyResponse);
                if (!readyValidation.IsValid)
                {
                    throw new InvalidDataException($"Server tạo Ready response không hợp lệ: {readyValidation.Message}");
                }

                await ProtocolWriter.WriteResponseAsync(stream, readyResponse, serverCancellationToken);

                string savedPath = await _storageService.SaveFileAsync(
                    request.FileName!,
                    request.FileSize,
                    request.FileHash,
                    stream,
                    receiveTimeoutMs,
                    serverCancellationToken);

                string savedFileName = Path.GetFileName(savedPath);
                fileName = savedFileName;
                bytesTransferred = request.FileSize;

                UploadResponse completedResponse = new()
                {
                    ProtocolVersion = ProtocolConstants.CurrentVersion,
                    RequestId = requestId,
                    Status = UploadStatus.Completed,
                    ErrorCode = ErrorCode.None,
                    ErrorMessage = "Upload thành công.",
                    SavedFileName = savedFileName
                };

                var completedValidation = MetadataValidator.ValidateResponse(completedResponse);
                if (!completedValidation.IsValid)
                {
                    throw new InvalidDataException($"Server tạo Completed response không hợp lệ: {completedValidation.Message}");
                }

                await ProtocolWriter.WriteResponseAsync(stream, completedResponse, serverCancellationToken);

                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Completed,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    "Upload hoàn tất thành công.");
            }
            catch (OperationCanceledException)
            {
                string cancelMsg = serverCancellationToken.IsCancellationRequested
                    ? "Session bị hủy do Server Graceful Shutdown."
                    : "Thao tác upload bị hủy.";

                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Cancel,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    cancelMsg);
            }
            catch (TimeoutException ex)
            {
                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Timeout,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    ex.Message);

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ConnectionLost,
                    ex.Message,
                    errorSendTimeoutMs);
            }
            catch (EndOfStreamException ex)
            {
                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Error,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    $"Message bị cắt: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ConnectionLost,
                    "Message bị cắt giữa chừng.",
                    errorSendTimeoutMs);
            }
            catch (IOException ex)
            {
                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Error,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    $"Mất kết nối: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ConnectionLost,
                    "Mất kết nối trong quá trình upload.",
                    errorSendTimeoutMs);
            }
            catch (ChecksumMismatchException ex)
            {
                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Error,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    $"Sai mã Hash: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ChecksumMismatch,
                    ex.Message,
                    errorSendTimeoutMs);
            }
            catch (StorageException ex)
            {
                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Error,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    $"Lỗi lưu trữ: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.StorageError,
                    ex.Message,
                    errorSendTimeoutMs);
            }
            catch (Exception ex)
            {
                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Error,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    $"Lỗi không xác định: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.UnknownError,
                    "Lỗi không xác định từ Server.",
                    errorSendTimeoutMs);
            }
            finally
            {
                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Disconnect,
                    requestId,
                    clientEndPoint,
                    fileName,
                    bytesTransferred,
                    "Ngắt kết nối session.");

                stream?.Dispose();
                _client.Close();
            }

            async Task TrySendErrorAsync(
                string responseRequestId,
                ErrorCode errCode,
                string message,
                int timeoutMs)
            {
                try
                {
                    if (stream is null || !_client.Connected)
                    {
                        return;
                    }

                    using var sendErrorCts = new CancellationTokenSource(timeoutMs);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(sendErrorCts.Token, serverCancellationToken);

                    await SendErrorAsync(
                        stream,
                        responseRequestId,
                        errCode,
                        message,
                        linkedCts.Token,
                        timeoutMs);
                }
                catch (Exception sendException)
                {
                    _logger.LogWarning(
                        $"[{clientEndPoint}] Không thể gửi lỗi {errCode} về client: {sendException.Message}");
                }
            }
        }

        private static Task SendErrorAsync(
            NetworkStream stream,
            string requestId,
            ErrorCode errorCode,
            string message,
            CancellationToken cancellationToken,
            int timeoutMs)
        {
            UploadResponse response = new()
            {
                ProtocolVersion = ProtocolConstants.CurrentVersion,
                RequestId = string.IsNullOrWhiteSpace(requestId) ? "N/A" : requestId,
                Status = UploadStatus.Error,
                ErrorCode = errorCode,
                ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Server từ chối yêu cầu." : message,
                SavedFileName = null
            };

            var validation = MetadataValidator.ValidateResponse(response);
            if (!validation.IsValid)
            {
                throw new InvalidDataException($"Error response không hợp lệ: {validation.Message}");
            }

            return ProtocolWriter.WriteResponseAsync(stream, response, cancellationToken);
        }
    }
}