using System;
using System.IO;
using System.Threading.Tasks;

namespace UDM10.Server
{
    public class FileStorageService
    {
        private const int ChunkSize = 64 * 1024; // 64 KB
        private readonly string _uploadsFolder;
        private readonly ServerLogger _logger;
        private readonly DuplicateFileNameResolver _nameResolver;
        private readonly TemporaryFileManager _tempFileManager;

        public FileStorageService(string uploadsFolder, ServerLogger logger)
        {
            _uploadsFolder = uploadsFolder;
            _logger = logger;
            _nameResolver = new DuplicateFileNameResolver(_uploadsFolder);
            _tempFileManager = new TemporaryFileManager(ChunkSize);
            Directory.CreateDirectory(_uploadsFolder);
        }

        // Nhận dữ liệu từ 'source', ghi ra file .part theo từng chunk,
        // đủ số byte thì đổi tên thành file thật. Lỗi thì xóa .part.
        public async Task<string> SaveFileAsync(string fileName, long fileSize, Stream source)
        {
            string finalPath = _nameResolver.GetAvailablePath(fileName);

            _logger.LogInfo($"Bắt đầu nhận file '{fileName}' ({fileSize} byte).");

            try
            {
                string savedPath = await _tempFileManager.ReceiveToFileAsync(finalPath, fileSize, source);
                _logger.LogInfo($"Nhận file '{fileName}' thành công, lưu tại '{savedPath}'.");
                return savedPath;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi nhận file '{fileName}': {ex.Message}");
                throw;
            }
        }
    }
}
