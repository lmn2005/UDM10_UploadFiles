using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UDM10.Server;
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

                // 1. Read Metadata
                // 1.1 Read 4 bytes for determine the length of the metadata (the length of JSON string)
                byte[] lengthBuffer = new byte[4];
                await stream.ReadExactlyAsync(lengthBuffer, 0, 4);
                int jsonLength = BitConverter.ToInt32(lengthBuffer, 0);

                // 1.2 Read the JSON string based on the length
                byte[] jsonBuffer = new byte[jsonLength];
                await stream.ReadExactlyAsync(jsonBuffer, 0, jsonLength);
                string jsonString = Encoding.UTF8.GetString(jsonBuffer);

                // 1.3 Deserialize the JSON string to get file name and size
                using JsonDocument doc = JsonDocument.Parse(jsonString);
                JsonElement root = doc.RootElement;
                string fileName = root.GetProperty("FileName").GetString() ?? "unnamed_file";
                long fileSize = root.GetProperty("FileSize").GetInt64();

                _logger.LogInfo($"[{clientEndPoint}] Request to upload file: {fileName} {fileSize} bytes");

                // 2. Validate and send ready response
                if(!MetadataValidator.IsValid(fileName, fileSize, out string validationError))
                {
                    await SendResponseAsync(stream, "ERROR", validationError);
                    return;
                }

                // Send ready signal to response to the client
                await SendResponseAsync(stream, "READY", "Server is ready to receive file data.");

                // 3. Receive file data in 64Kb chunks
                string uploadDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads");
                Directory.CreateDirectory(uploadDirectory);

                tempFilePath = Path.Combine(uploadDirectory, $"{fileName}.part");
                string finalFilePath = Path.Combine(uploadDirectory, fileName);

                byte[] buffer = new byte[64 * 1024]; // 64Kb buffer
                long totalBytesReceived = 0;

                using (FileStream fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    while(totalBytesReceived < fileSize)
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

                // 4. Finalize and clean up
                if (totalBytesReceived == fileSize)
                {
                    // Change file name from .part to offical file name
                    File.Move(tempFilePath, finalFilePath, overwrite: true);

                    // Send completed signal to the client
                    await SendResponseAsync(stream, "COMPLETED", "File uploaded successfully.");

                    _logger.LogInfo($"[{clientEndPoint}] Upload completed successfully: {fileName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{clientEndPoint}] Error occurred while handling client connection: {ex.Message}");

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

        private async Task SendResponseAsync(NetworkStream stream, string status, string message)
        {
            var response = new UploadResponse
            {
                Status = status,
                Message = message
            };

            string jsonString = JsonSerializer.Serialize(response);
            byte[] responseBytes = Encoding.UTF8.GetBytes(jsonString);

            // Send the length of the JSON string first (4 bytes) - Similar to how Client sends Metadata
            byte[] lengthBytes = BitConverter.GetBytes(responseBytes.Length);
            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);

            // Send the JSON string
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            await stream.FlushAsync();
        }
    }
}
