using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UDM10.Shared;

namespace UDM10.Server
{
    public class ClientConnectionHandler
    {
        private readonly TcpClient _client;
        private readonly ServerLogger _logger;
        private readonly FileStorageService _storageService;

        public ClientConnectionHandler(TcpClient client, ServerLogger logger, FileStorageService storageService)
        {
            _client = client;
            _logger = logger;
            _storageService = storageService;
        }

        public async Task HandleAsync()
        {
            string clientEndPoint = _client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            CancellationToken cancellationToken = CancellationToken.None;

            try
            {
                using NetworkStream stream = _client.GetStream();
                _logger.LogInfo($"[{clientEndPoint}] Starting to handle data flow...");

                UploadRequest? request = await ProtocolReader.ReadMetadataAsync<UploadRequest>(stream, cancellationToken);
                if (request is null)
                {
                    _logger.LogError($"[{clientEndPoint}] Không đọc được metadata từ client.");
                    return;
                }

                string fileName = request.FileName ?? string.Empty;
                long fileSize = request.FileSize;

                _logger.LogInfo($"[{clientEndPoint}] Request (ID: {request.RequestId}) upload: {fileName} ({fileSize} bytes)");

                if (!MetadataValidator.IsValid(fileName, fileSize, out ErrorCode validationErrorCode, out string validationError))
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

                var readyResponse = new UploadResponse
                {
                    RequestId = request.RequestId,
                    Status = UploadStatus.Ready,
                    Error = ErrorCode.None,
                    Message = "Ready to receive file chunks"
                };
                await ProtocolWriter.WriteMetadataAsync(stream, readyResponse, cancellationToken);

                string savedPath = await _storageService.SaveFileAsync(fileName, fileSize, stream);

                var completedResponse = new UploadResponse
                {
                    RequestId = request.RequestId,
                    Status = UploadStatus.Completed,
                    Error = ErrorCode.None,
                    Message = "File upload successfully."
                };
                await ProtocolWriter.WriteMetadataAsync(stream, completedResponse, cancellationToken);

                _logger.LogInfo($"[{clientEndPoint}] Upload completed: {savedPath}");
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
                        await ProtocolWriter.WriteMetadataAsync(_client.GetStream(), errorResponse, cancellationToken);
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