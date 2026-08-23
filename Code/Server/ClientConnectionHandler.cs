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
                _config.GetValue<int>(
                    "Network:ReceiveTimeoutMs",
                    60000);

            using CancellationTokenSource cts =
                new(TimeSpan.FromMilliseconds(receiveTimeoutMs));

            CancellationToken cancellationToken = cts.Token;

            try
            {
                using NetworkStream stream =
                    _client.GetStream();

                UploadRequest? request =
                    await ProtocolReader.ReadMetadataAsync<UploadRequest>(
                        stream,
                        cancellationToken);

                if (request == null)
                {
                    return;
                }

                _logger.LogInfo(
                    $"[{clientEndPoint}] RequestId={request.RequestId}, " +
                    $"Status={request.Status}, File={request.FileName}");

                long maxAllowedSize =
                    _config.GetValue<long>(
                        "Upload:MaxAllowedSizeInBytes",
                        10737418240);

                var validation =
                    MetadataValidator.Validate(
                        request,
                        maxAllowedSize);

                if (!validation.IsValid)
                {
                    await SendErrorAsync(
                        stream,
                        request.RequestId,
                        validation.ErrorCode,
                        validation.Message,
                        cancellationToken);

                    return;
                }
                if (request.Status == UploadStatus.Cancel)
                {
                    await SendErrorAsync(
                        stream,
                        request.RequestId,
                        ErrorCode.CancelledByUser,
                        "Upload đã được hủy.",
                        cancellationToken);

                    return;
                }

                UploadResponse readyResponse = new()
                {
                    ProtocolVersion =
                        ProtocolConstants.CurrentVersion,

                    RequestId = request.RequestId,

                    Status = UploadStatus.Ready,

                    ErrorCode = ErrorCode.None,

                    ErrorMessage =
                        "Server sẵn sàng nhận file."
                };

                await ProtocolWriter.WriteMetadataAsync(
                    stream,
                    readyResponse,
                    cancellationToken);

                string savedPath =
                    await _storageService.SaveFileAsync(
                        request.FileName,
                        request.FileSize,
                        request.FileHash,
                        stream,
                        cancellationToken);

                UploadResponse completedResponse = new()
                {
                    ProtocolVersion =
                        ProtocolConstants.CurrentVersion,

                    RequestId = request.RequestId,

                    Status = UploadStatus.Completed,

                    ErrorCode = ErrorCode.None,

                    ErrorMessage =
                        "Upload thành công."
                };

                await ProtocolWriter.WriteMetadataAsync(
                    stream,
                    completedResponse,
                    cancellationToken);

                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    $"Upload completed: {savedPath}");
            }
            catch (OperationCanceledException ex)
                when (ex.Message == "CLIENT_CANCELLED")
            {
                _logger.LogInfo(
                    $"[{clientEndPoint}] " +
                    "Client đã hủy upload.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    "Connection timeout.");
            }
            catch (ChecksumMismatchException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Checksum mismatch: {ex.Message}");

                await TrySendErrorAsync(
                    ErrorCode.ChecksumMismatch,
                    ex.Message);
            }
            catch (IOException ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] " +
                    $"Connection lost: {ex.Message}");

                await TrySendErrorAsync(
                    ErrorCode.ConnectionLost,
                    "Mất kết nối trong quá trình upload.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"[{clientEndPoint}] Error: {ex.Message}");

                await TrySendErrorAsync(
                    ErrorCode.UnknownError,
                    "Lỗi không xác định từ Server.");
            }
            finally
            {
                _client.Close();
            }

            async Task TrySendErrorAsync(
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
                        null,
                        errorCode,
                        message,
                        cancellationToken);
                }
                catch
                {             
                }
            }
        }

        private static Task SendErrorAsync(
            Stream stream,
            string? requestId,
            ErrorCode errorCode,
            string message,
            CancellationToken cancellationToken)
        {
            UploadResponse response = new()
            {
                ProtocolVersion =
                    ProtocolConstants.CurrentVersion,

                RequestId = requestId ?? string.Empty,

                Status = UploadStatus.Error,

                ErrorCode = errorCode,

                ErrorMessage = message
            };

            return ProtocolWriter.WriteMetadataAsync(
                stream,
                response,
                cancellationToken);
        }
    }
}