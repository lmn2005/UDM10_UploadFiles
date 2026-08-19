using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Server
{
    // Nhận dữ liệu từ một stream, ghi ra file .part theo từng chunk,
    // đồng thời tính SHA-256 tăng dần. Đủ byte và khớp hash thì đổi tên
    // thành file chính thức; sai kích thước, bị hủy hoặc sai hash thì xóa .part.
    public class TemporaryFileManager
    {
        private readonly int _chunkSize;

        public TemporaryFileManager(int chunkSize)
        {
            _chunkSize = chunkSize > 0 ? chunkSize : 64 * 1024;
        }

        public async Task<string> ReceiveToFileAsync(string finalPath, long expectedSize, string? expectedHash, Stream source, CancellationToken cancellationToken = default)
        {
            string partPath = finalPath + ".part";
            long totalWritten = 0;
            var buffer = new byte[_chunkSize];
            using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            try
            {
                using (var fileStream = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    while (totalWritten < expectedSize)
                    {
                        // Kiểm tra nếu có tín hiệu Hủy/Timeout từ CancellationToken
                        cancellationToken.ThrowIfCancellationRequested();

                        int bytesToRead = (int)Math.Min(buffer.Length, expectedSize - totalWritten);
                        int bytesRead = await source.ReadAsync(buffer, 0, bytesToRead, cancellationToken);

                        // Phát hiện Client ngắt luồng/CANCEL: Luồng trả về 0 byte khi chưa đủ dữ liệu
                        if (bytesRead == 0)
                        {
                            throw new OperationCanceledException("CLIENT_CANCELLED");
                        }

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        hasher.AppendData(buffer, 0, bytesRead);
                        totalWritten += bytesRead;
                    }
                }

                if (totalWritten != expectedSize)
                {
                    if (File.Exists(partPath)) File.Delete(partPath);
                    throw new IOException(
                        $"Nhận thiếu dữ liệu: mong đợi {expectedSize} byte, nhận được {totalWritten} byte.");
                }

                if (!string.IsNullOrEmpty(expectedHash))
                {
                    string actualHash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(partPath)) File.Delete(partPath);
                        throw new ChecksumMismatchException(
                            "File nhận được không khớp checksum với file gốc, có thể đã bị lỗi trong quá trình truyền.");
                    }
                }

                if (File.Exists(finalPath)) File.Delete(finalPath);
                File.Move(partPath, finalPath);
                return finalPath;
            }
            catch
            {
                if (File.Exists(partPath))
                {
                    try { File.Delete(partPath); } catch { }
                }
                throw;
            }
        }
    }
}