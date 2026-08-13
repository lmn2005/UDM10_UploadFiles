using System;
using System.IO;
using System.Threading.Tasks;

namespace UDM10.Server
{
    // Nhận dữ liệu từ một stream, ghi ra file .part theo từng chunk.
    // Đủ số byte cam kết thì đổi tên thành file chính thức; thiếu hoặc lỗi thì xóa .part.
    public class TemporaryFileManager
    {
        private readonly int _chunkSize;

        public TemporaryFileManager(int chunkSize)
        {
            _chunkSize = chunkSize > 0 ? chunkSize : 64 * 1024;
        }

        public async Task<string> ReceiveToFileAsync(string finalPath, long expectedSize, Stream source)
        {
            string partPath = finalPath + ".part";
            long totalWritten = 0;
            var buffer = new byte[_chunkSize];

            try
            {
                using (var fileStream = new FileStream(partPath, FileMode.Create, FileAccess.Write))
                {
                    int bytesRead;
                    while (totalWritten < expectedSize &&
                           (bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalWritten += bytesRead;
                    }
                }

                if (totalWritten != expectedSize)
                {
                    File.Delete(partPath);
                    throw new IOException(
                        $"Nhận thiếu dữ liệu: mong đợi {expectedSize} byte, nhận được {totalWritten} byte.");
                }

                File.Move(partPath, finalPath);
                return finalPath;
            }
            catch
            {
                if (File.Exists(partPath))
                    File.Delete(partPath);
                throw;
            }
        }
    }
}
