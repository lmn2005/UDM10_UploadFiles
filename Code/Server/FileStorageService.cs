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

        public FileStorageService(string uploadsFolder, ServerLogger logger)
        {
            _uploadsFolder = uploadsFolder;
            _logger = logger;
            Directory.CreateDirectory(_uploadsFolder);
        }

        // Nhận dữ liệu từ 'source', ghi ra file .part theo từng chunk,
        // đủ số byte thì đổi tên thành file thật. Lỗi thì xóa .part.
        public async Task<string> SaveFileAsync(string fileName, long fileSize, Stream source)
        {
            string finalPath = GetAvailablePath(fileName);
            string partPath = finalPath + ".part";

            _logger.LogInfo($"Bắt đầu nhận file '{fileName}' ({fileSize} byte).");

            long totalWritten = 0;
            var buffer = new byte[ChunkSize];

            try
            {
                using (var fileStream = new FileStream(partPath, FileMode.Create, FileAccess.Write))
                {
                    int bytesRead;
                    while (totalWritten < fileSize &&
                           (bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalWritten += bytesRead;
                    }
                }

                if (totalWritten != fileSize)
                {
                    File.Delete(partPath);
                    string errorMsg = $"Nhận thiếu dữ liệu cho '{fileName}': mong đợi {fileSize} byte, nhận được {totalWritten} byte.";
                    _logger.LogError(errorMsg);
                    throw new IOException(errorMsg);
                }

                File.Move(partPath, finalPath);
                _logger.LogInfo($"Nhận file '{fileName}' thành công, lưu tại '{finalPath}'.");
                return finalPath;
            }
            catch (Exception ex)
            {
                if (File.Exists(partPath))
                    File.Delete(partPath);
                _logger.LogError($"Lỗi khi nhận file '{fileName}': {ex.Message}");
                throw;
            }
        }

        // Không ghi đè file trùng tên: tự thêm _1, _2, _3...
        private string GetAvailablePath(string fileName)
        {
            string path = Path.Combine(_uploadsFolder, fileName);
            if (!File.Exists(path))
                return path;

            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(_uploadsFolder, $"{name}_{i}{ext}");
                i++;
            } while (File.Exists(candidate));

            return candidate;
        }
    }
}