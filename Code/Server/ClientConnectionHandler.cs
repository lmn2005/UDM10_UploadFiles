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

        public async Task HandleAsync(
            CancellationToken serverCancellationToken = default)
        {
            string clientEndPoint =
                _client.Client.RemoteEndPoint?.ToString()
                ?? "Unknown";

            int receiveTimeoutMs =
                Math.Max(
                    1,
                    _config.GetValue<int>(
                        "Network:ReceiveTimeoutMs",
                        60000));

            string requestId = string.Empty;
            NetworkStream? stream = null;

            try
            {
                stream = _client.GetStream();

               
                UploadRequest? request;

                using (
                    CancellationTokenSource metadataCts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                serverCancellationToken))
                {
                    metadataCts.CancelAfter(
                        receiveTimeoutMs);

                    try
                    {
                        request =
                            await ProtocolReader
                                .ReadRequestAsync(
                                    stream,
                                    metadataCts.Token);
                    }
                    catch (
                        OperationCanceledException)
                        when (
                            !serverCancellationToken
                                .IsCancellationRequested)
                    {
                        throw new TimeoutException(
                            $"Không nhận được metadata trong " +
                            $"{receiveTimeoutMs} ms.");
                    }
                }

               
                long maxAllowedSize =
                    _config.GetValue<long>(
                        "Upload:MaxAllowedSizeInBytes",
                        10L * 1024 * 1024 * 1024);

                var validation =
                    MetadataValidator.Validate(
                        request,
                        maxAllowedSize);

                if (!validation.IsValid)
                {
                    string currentRequestId =
                        request?.RequestId ?? string.Empty;

                    await SendErrorAsync(
                        stream,
                        currentRequestId,
                        validation.ErrorCode,
                        validation.Message,
                        serverCancellationToken);

                    return;
                }

                requestId =
                    request!.RequestId;

                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    $"RequestId={requestId}, " +
                    $"File={request.FileName}, " +
                    $"Size={request.FileSize} bytes, " +
                    $"Status={request.Status}");

             
                UploadResponse readyResponse = new()
                {
                    ProtocolVersion =
                        ProtocolConstants.CurrentVersion,

                    RequestId =
                        requestId,

                    Status =
                        UploadStatus.Ready,

                    ErrorCode =
                        ErrorCode.None,

                    ErrorMessage =
                        "Server sẵn sàng nhận file.",

                    SavedFileName =
                        null
                };

                var readyValidation =
                    MetadataValidator.ValidateResponse(
                        readyResponse);

                if (!readyValidation.IsValid)
                {
                    throw new InvalidDataException(
                        $"Server tạo Ready response không hợp lệ: " +
                        $"{readyValidation.Message}");
                }

                await ProtocolWriter.WriteResponseAsync(
                    stream,
                    readyResponse,
                    serverCancellationToken);

                

                string savedPath =
                    await _storageService.SaveFileAsync(
                        request.FileName,
                        request.FileSize,
                        request.FileHash,
                        stream,
                        receiveTimeoutMs,
                        serverCancellationToken);

                string savedFileName =
                    Path.GetFileName(savedPath);

                UploadResponse completedResponse = new()
                {
                    ProtocolVersion =
                        ProtocolConstants.CurrentVersion,

                    RequestId =
                        requestId,

                    Status =
                        UploadStatus.Completed,

                    ErrorCode =
                        ErrorCode.None,

                    ErrorMessage =
                        "Upload thành công.",

                    SavedFileName =
                        savedFileName
                };

                var completedValidation =
                    MetadataValidator.ValidateResponse(
                        completedResponse);

                if (!completedValidation.IsValid)
                {
                    throw new InvalidDataException(
                        $"Server tạo Completed response " +
                        $"không hợp lệ: " +
                        $"{completedValidation.Message}");
                }

                await ProtocolWriter.WriteResponseAsync(
                    stream,
                    completedResponse,
                    serverCancellationToken);

                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    $"Upload completed. " +
                    $"SavedFileName={savedFileName}");
            }
            catch (OperationCanceledException)
            {
                if (serverCancellationToken
                    .IsCancellationRequested)
                {
                    _logger.LogWarning(
                        $"[{clientEndPoint}] " +
                        "Session cancelled due to Server " +
                        "Graceful Shutdown.");
                }
                else
                {
                    _logger.LogWarning(
                        $"[{clientEndPoint}] " +
                        "Upload operation was cancelled.");
                }
            }
            catch (EndOfStreamException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Message bị cắt: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ConnectionLost,
                    "Message bị cắt giữa chừng.");
            }
            catch (IOException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Connection lost: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ConnectionLost,
                    "Mất kết nối trong quá trình upload.");
            }
            catch (ChecksumMismatchException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Checksum mismatch: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ChecksumMismatch,
                    ex.Message);
            }
            catch (StorageException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Storage error: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.StorageError,
                    ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Error: {ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.UnknownError,
                    "Lỗi không xác định từ Server.");
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
                    if (stream is null ||
                        !_client.Connected)
                    {
                        return;
                    }

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
                        $"[{clientEndPoint}] " +
                        $"Không thể gửi lỗi {errCode} về client: " +
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
                ProtocolVersion =
                    ProtocolConstants.CurrentVersion,

                RequestId =
                    requestId ?? string.Empty,

                Status =
                    UploadStatus.Error,

                ErrorCode =
                    errorCode,

                ErrorMessage =
                    string.IsNullOrWhiteSpace(message)
                        ? "Server từ chối yêu cầu."
                        : message,

                SavedFileName =
                    null
            };

            var validation =
                MetadataValidator.ValidateResponse(
                    response);

            if (!validation.IsValid)
            {
                throw new InvalidDataException(
                    $"Error response không hợp lệ: " +
                    $"{validation.Message}");
            }

            return ProtocolWriter.WriteResponseAsync(
                stream,
                response,
                cancellationToken);
        }
    }
}