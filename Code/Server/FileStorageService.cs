using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace UDM10.Server
{
    public class FileStorageService
    {
        private readonly int _chunkSize;
        private readonly string _uploadsFolder;
        private readonly ServerLogger _logger;
        private readonly DuplicateFileNameResolver _nameResolver;
        private readonly TemporaryFileManager _tempFileManager;

        public FileStorageService(IConfiguration config, ServerLogger logger)
        {
            _uploadsFolder = config.GetValue<string>("Upload:SaveDirectory", "Uploads") ?? "Uploads";
            _chunkSize = config.GetValue<int>("Upload:ChunkSizeBytes", 65536);

            _logger = logger;
            _nameResolver = new DuplicateFileNameResolver(_uploadsFolder);
            _tempFileManager = new TemporaryFileManager(_chunkSize);
            Directory.CreateDirectory(_uploadsFolder);
        }

        // Nhận dữ liệu từ 'source', ghi ra file .part theo từng chunk,
        // đủ số byte thì đổi tên thành file thật. Lỗi thì xóa .part.
        public async Task<string> SaveFileAsync(string fileName, long fileSize, string? expectedHash, Stream source, CancellationToken cancellationToken = default)
        {
            string finalPath = _nameResolver.GetAvailablePath(fileName);

            _logger.LogInfo($"Starting to receive file '{fileName}' ({fileSize} bytes).");

            try
            {
                string savedPath = await _tempFileManager.ReceiveToFileAsync(finalPath, fileSize, expectedHash, source, cancellationToken);
                _logger.LogInfo($"Successfully received file '{fileName}' and saved it to '{savedPath}'.");
                return savedPath;
            }
            catch (OperationCanceledException ex) when (ex.Message == "CLIENT_CANCELLED")
            {
                _logger.LogWarning($"[CANCEL] The upload of file '{fileName}' was explicitly cancelled by the client.");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogError($"[TIMEOUT] The process of receiving file '{fileName}' timed out due to inactivity.");
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError($"[DISCONNECT] Connection lost while receiving file '{fileName}': {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error receiving file '{fileName}': {ex.Message}");
                throw;
            }
        }
    }
}