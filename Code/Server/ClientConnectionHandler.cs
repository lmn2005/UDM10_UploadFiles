using System;
using System.IO;
using System.Linq.Expressions;
using System.Net.Sockets;
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

            try
            {
                using NetworkStream stream = _client.GetStream();
                _logger.LogInfo($"[{clientEndPoint}] Starting to handle data flow...");

                UploadRequest request = await ProtocolReader.ReadRequestAsync(stream);

                string fileName = request.FileName ?? "unnamed_file";
                long fileSize = request.FileSize;

                _logger.LogInfo($"[{clientEndPoint}] Request to upload file: {fileName} {fileSize} bytes");

                if (!MetadataValidator.IsValid(fileName, fileSize, out string validationError))
                {
                    var errorResponse = new UploadResponse
                    {
                        Status = UploadStatus.Failed,
                        Error = ErrorCode.InvalidRequest,
                        Message = validationError
                    };
                    await ProtocolWriter.WriteResponseAsync(stream, errorResponse);
                    return;
                }

                var readyResponse = new UploadResponse
                {
                    Status = UploadStatus.Pending,
                    Error = ErrorCode.None,
                    Message = "Ready to receive file chunks"
                };
                await ProtocolWriter.WriteResponseAsync(stream, readyResponse);

                string savedPath = await _storageService.SaveFileAsync(fileName, fileSize, stream);

                var completedResponse = new UploadResponse
                {
                    Status = UploadStatus.Completed,
                    Message = "File upload successfully."
                };
                await ProtocolWriter.WriteResponseAsync(stream, completedResponse);

                _logger.LogInfo($"[{clientEndPoint}] Upload completed successfully: {savedPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{clientEndPoint}] Error occured while handling client connection: {ex.Message}");

                try
                {
                    if (_client.Connected)
                    {
                        var errorResponse = new UploadResponse
                        {
                            Status = UploadStatus.Failed,
                            Error = ErrorCode.UnknownError,
                            Message = ex.Message
                        };
                        await ProtocolWriter.WriteResponseAsync(_client.GetStream(), errorResponse);
                    }
                }
                catch
                {

                }
            }
            finally
            {
                _client.Close();
                _logger.LogInfo($"Connection to {clientEndPoint} closed.");
            }
        }
    }
}