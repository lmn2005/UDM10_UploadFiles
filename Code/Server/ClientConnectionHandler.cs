using System;
using System.IO; 
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
            string? tempFilePath = null;

            try
            {
                using NetworkStream stream = _client.GetStream();
                _logger.LogInfo($"[{clientEndPoint}] Starting to handle data flow...");

                UploadRequest request = await ProtocolReader.ReadRequestAsync(stream);

                string fileName = request.FileName ?? "unnamed_file";
                long fileSize = request.FileSize;

                _logger.LogInfo($"[{clientEndPoint}] Request to upload file: {fileName} {fileSize} bytes");

                if(!MetadataValidator.IsValid(fileName, fileSize, out string validationError))
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

                string uploadDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads");
                Directory.CreateDirectory(uploadDirectory);

                tempFilePath = Path.Combine(uploadDirectory, $"{fileName}.part");
                string finalFilePath = Path.Combine(uploadDirectory, fileName);

                byte[] buffer = new byte[64 * 1024]; 
                long totalBytesReceived = 0;

                using (FileStream fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    while (totalBytesReceived < fileSize)
                    {
                        int bytesToRead = (int)Math.Min(buffer.Length, fileSize - totalBytesReceived);
                        int bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);

                        if (bytesRead == 0)
                        {
                            throw new Exception("Client disconnected while transferring file.");
                        }

                        await fs.WriteAsync(buffer, 0, bytesRead);
                        totalBytesReceived += bytesRead;
                    }
                }

                if (totalBytesReceived == fileSize)
                {
                    File.Move(tempFilePath, finalFilePath, overwrite: true);

                    var completedResponse = new UploadResponse
                    {
                        Status = UploadStatus.Completed,
                        Message = "File uploaded successfully."
                    };
                    await ProtocolWriter.WriteResponseAsync(stream, completedResponse);

                    _logger.LogInfo($"[{clientEndPoint}] Upload completed successfully: {fileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{clientEndPoint}] Error occurred while handling client connection: {ex.Message}");

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
                catch { }

                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                        _logger.LogInfo($"[{clientEndPoint}] Cleaned up incomplete file: {tempFilePath}");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogError($"[{clientEndPoint}] Failed to delete temp file: {deleteEx.Message}");
                    }
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