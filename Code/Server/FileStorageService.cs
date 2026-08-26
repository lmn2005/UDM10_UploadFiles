using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace UDM10.Server
{
    public class FileStorageService
    {
        private const int ChunkSize = 64 * 1024; // 64 KB
        private readonly string _uploadsFolder;
        private readonly ServerLogger _logger;
        private readonly DuplicateFileNameResolver _nameResolver;
        private readonly TemporaryFileManager _tempFileManager;

        public FileStorageService(
            IConfiguration config,
            ServerLogger logger,
            DuplicateFileNameResolver? nameResolver = null,
            TemporaryFileManager? tempFileManager = null)
        {
            _uploadsFolder = config.GetValue<string>("Upload:SaveDirectory") ?? "Uploads";
            _logger = logger;
            _nameResolver = nameResolver ?? new DuplicateFileNameResolver(_uploadsFolder);

            int chunkSize = config.GetValue<int>(
                "Upload:ChunkSizeBytes",
                ChunkSize);

            _tempFileManager =
                tempFileManager ??
                new TemporaryFileManager(
                    chunkSize,
                    _uploadsFolder);
        }

        // Nhận dữ liệu từ 'source', ghi ra file .part theo từng chunk,
        // đủ số byte thì đổi tên thành file thật. Lỗi thì xóa .part.
        public async Task<string> SaveFileAsync(
            string fileName,
            long fileSize,
            string expectedHash,
            Stream source,
            int receiveTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            string finalPath = _nameResolver.GetAvailablePath(fileName);
            _logger.LogInfo($"Bắt đầu nhận file '{fileName}' ({fileSize} byte).");

            try
            {
                string savedPath = await _tempFileManager.ReceiveToFileAsync(
                    finalPath,
                    fileSize,
                    source,
                    expectedHash,
                    receiveTimeoutMs,
                    cancellationToken);

                _logger.LogInfo($"Nhận file '{fileName}' thành công, lưu tại '{savedPath}'.");
                return savedPath;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning($"[CANCEL] Quá trình tải file '{fileName}' bị hủy: {ex.Message}");
                throw;
            }
            finally
            {
                _nameResolver.ReleasePath(finalPath);
            }
        }
    }
}
