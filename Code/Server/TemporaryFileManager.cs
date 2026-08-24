using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Server
{
    public class TemporaryFileManager
    {
        private readonly int _chunkSize;

        public TemporaryFileManager(int chunkSize)
        {
            _chunkSize = chunkSize > 0 ? chunkSize : 64 * 1024;
        }

        public async Task<string> ReceiveToFileAsync(
            string finalPath, long expectedSize, Stream source, CancellationToken cancellationToken = default)
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
                           (bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
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
                // Bắt cả OperationCanceledException (khi Cancel) lẫn lỗi khác (khi Error) —
                // cả 2 trường hợp đều phải dọn dẹp file .part
                if (File.Exists(partPath))
                    File.Delete(partPath);
                throw;
            }
        }
    }
}