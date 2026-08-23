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

        public ClientConnectionHandler(TcpClient client, ServerLogger logger, FileStorageService storageService, IConfiguration config)
        {
            _client = client;
            _logger = logger;
            _storageService = storageService;
            _config = config;
        }

        public async Task HandleAsync(CancellationToken serverCancellationToken = default)
        {
            string clientEndPoint = _client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

            int receiveTimeoutMs = _config.GetValue<int>("Network:ReceiveTimeoutMs", 60000);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(receiveTimeoutMs));

            // Kết hợp CancellationToken từ Server (Ctrl+C / Shutdown) và Timeout nội bộ
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken, timeoutCts.Token);
            CancellationToken cancellationToken = linkedCts.Token;

            try
            {
                using NetworkStream stream = _client.GetStream();
                _logger.LogInfo($"[{clientEndPoint}] Starting to handle the data flow...");

                UploadRequest? request = await ProtocolReader.ReadMetadataAsync<UploadRequest>(stream, cancellationToken);
                if (request is null)
                {
                    _logger.LogError($"[{clientEndPoint}] Unable to read metadata from the client.");
                    return;
                }

                string fileName = request.FileName ?? string.Empty;
                long fileSize = request.FileSize;

                _logger.LogInfo($"[{clientEndPoint}] Upload (ID: {request.RequestId}) request: {fileName} ({fileSize} bytes)");

                long maxAllowedSize = _config.GetValue<long>("Upload:MaxAllowedSizeInBytes", 10737418240);

                if (!MetadataValidator.IsValid(request, maxAllowedSize, out ErrorCode validationErrorCode, out string validationError))
                {
                    var errorResponse = new UploadResponse
                    {
                        RequestId = request.RequestId,
                        Status = UploadStatus.Error,
                        Error = validationErrorCode,
                        Message = validationError
                    };
                    await ProtocolWriter.WriteMetadataAsync(stream, errorResponse, cancellationToken);
                    return;
                }

                UploadResponse readyResponse = new UploadResponse
                {
                    RequestId = request.RequestId,
                    Status = UploadStatus.Ready,
                    Error = ErrorCode.None,
                    Message = "Ready to receive file chunks"
                };
                await ProtocolWriter.WriteMetadataAsync(stream, readyResponse, cancellationToken);

                string savedPath = await _storageService.SaveFileAsync(fileName, fileSize, request.FileHash, stream, cancellationToken);
                UploadResponse completedResponse = new UploadResponse
                {
                    RequestId = request.RequestId,
                    Status = UploadStatus.Completed,
                    Error = ErrorCode.None,
                    Message = "File upload successfully."
                };
                await ProtocolWriter.WriteMetadataAsync(stream, completedResponse, cancellationToken);

                _logger.LogInfo($"[{clientEndPoint}] Upload completed: {savedPath}");
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
            catch (ChecksumMismatchException ex)
            {
                _logger.LogError($"[{clientEndPoint}] Checksum mismatch: {ex.Message}");
                try
                {
                    if (_client.Connected)
                    {
                        var errorResponse = new UploadResponse
                        {
                            Status = UploadStatus.Error,
                            Error = ErrorCode.ChecksumMismatch,
                            Message = ex.Message
                        };
                        await ProtocolWriter.WriteMetadataAsync(_client.GetStream(), errorResponse, CancellationToken.None);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{clientEndPoint}] Error: {ex.Message}");
                try
                {
                    if (_client.Connected)
                    {
                        var errorResponse = new UploadResponse
                        {
                            Status = UploadStatus.Error,
                            Error = ErrorCode.UnknownError,
                            Message = ex.Message
                        };
                        await ProtocolWriter.WriteMetadataAsync(_client.GetStream(), errorResponse, CancellationToken.None);
                    }
                }
                catch { }
            }
            finally
            {
                _client.Close();
            }
        }
    }
}