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

        public async Task HandleAsync()
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

            using CancellationTokenSource cts =
                new(
                    TimeSpan.FromMilliseconds(
                        receiveTimeoutMs));

            CancellationToken cancellationToken =
                cts.Token;

            string requestId = string.Empty;

            try
            {
                using NetworkStream stream =
                    _client.GetStream();

                UploadRequest? request =
                    await ProtocolReader
                        .ReadRequestAsync(
                            stream,
                            cancellationToken);

                if (request is null)
                {
                    await SendErrorAsync(
                        stream,
                        string.Empty,
                        ErrorCode.InvalidMetadata,
                        "UploadRequest không tồn tại " +
                        "hoặc có giá trị null.",
                        cancellationToken);

                    return;
                }

                requestId =
                    request.RequestId ?? string.Empty;

                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    $"RequestId={requestId}, " +
                    $"Status={request.Status}, " +
                    $"File={request.FileName}");

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
                    await SendErrorAsync(
                        stream,
                        requestId,
                        validation.ErrorCode,
                        validation.Message,
                        cancellationToken);

                    return;
                }

               
                if (request.Status ==
                    UploadStatus.Cancel)
                {
                    await SendErrorAsync(
                        stream,
                        requestId,
                        ErrorCode.CancelledByUser,
                        "Upload đã được hủy " +
                        "theo yêu cầu của Client.",
                        cancellationToken);

                    return;
                }

                UploadResponse readyResponse =
                    new()
                    {
                        ProtocolVersion =
                            ProtocolConstants
                                .CurrentVersion,

                        RequestId =
                            requestId,

                        Status =
                            UploadStatus.Ready,

                        ErrorCode =
                            ErrorCode.None,

                        ErrorMessage =
                            request.Status ==
                                UploadStatus.Retry
                                ? "Server sẵn sàng " +
                                  "nhận lại file."
                                : "Server sẵn sàng " +
                                  "nhận file."
                    };

                await ProtocolWriter
                    .WriteResponseAsync(
                        stream,
                        readyResponse,
                        cancellationToken);

               
                string savedPath =
                    await _storageService
                        .SaveFileAsync(
                            request.FileName,
                            request.FileSize,
                            stream,
                            requestId,
                            clientEndPoint,
                            cancellationToken);

                UploadResponse completedResponse =
                    new()
                    {
                        ProtocolVersion =
                            ProtocolConstants
                                .CurrentVersion,

                        RequestId =
                            requestId,

                        Status =
                            UploadStatus.Completed,

                        ErrorCode =
                            ErrorCode.None,

                        ErrorMessage =
                            "Upload thành công."
                    };

                await ProtocolWriter
                    .WriteResponseAsync(
                        stream,
                        completedResponse,
                        cancellationToken);

                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    $"Upload completed: " +
                    $"{savedPath}");
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    "Connection timeout hoặc " +
                    "upload bị hủy.");
            }
            catch (EndOfStreamException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Message bị cắt: " +
                    $"{ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ConnectionLost,
                    "Message bị cắt giữa chừng.");
            }
            catch (InvalidDataException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Metadata không hợp lệ: " +
                    $"{ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.InvalidMetadata,
                    ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Connection lost: " +
                    $"{ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ConnectionLost,
                    "Mất kết nối trong quá trình upload.");
            }
            catch (ChecksumMismatchException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Checksum mismatch: " +
                    $"{ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.ChecksumMismatch,
                    ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] Error: " +
                    $"{ex.Message}");

                await TrySendErrorAsync(
                    requestId,
                    ErrorCode.UnknownError,
                    "Lỗi không xác định từ Server.");
            }
            finally
            {
                _client.Close();
            }

            async Task TrySendErrorAsync(
                string responseRequestId,
                ErrorCode errorCode,
                string message)
            {
                try
                {
                    if (!_client.Connected)
                    {
                        return;
                    }

                    await SendErrorAsync(
                        _client.GetStream(),
                        responseRequestId,
                        errorCode,
                        message,
                        CancellationToken.None);
                }
                catch
                {
                 
                }
            }
        }

        private static Task SendErrorAsync(
            Stream stream,
            string requestId,
            ErrorCode errorCode,
            string message,
            CancellationToken cancellationToken)
        {
            UploadResponse response =
                new()
                {
                    ProtocolVersion =
                        ProtocolConstants
                            .CurrentVersion,

                    RequestId =
                        requestId ?? string.Empty,

                    Status =
                        UploadStatus.Error,

                    ErrorCode =
                        errorCode,

                    ErrorMessage =
                        message
                };

            return ProtocolWriter
                .WriteResponseAsync(
                    stream,
                    response,
                    cancellationToken);
        }
    }
}