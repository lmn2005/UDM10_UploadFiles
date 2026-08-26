using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Server
{
    public class TemporaryFileManager
    {
        private readonly int _chunkSize;
        private readonly string _uploadsFolder;

        // Constructor nhận cả 2 tham số
        public TemporaryFileManager(int chunkSize, string uploadsFolder)
        {
            _chunkSize = chunkSize > 0 ? chunkSize : 8192;
            _uploadsFolder = string.IsNullOrEmpty(uploadsFolder) ? "Uploads" : uploadsFolder;

            if (!Directory.Exists(_uploadsFolder))
            {
                Directory.CreateDirectory(_uploadsFolder);
            }
        }

        // Constructor dự phòng nếu code cũ chỉ truyền chunkSize
        public TemporaryFileManager(int chunkSize) : this(chunkSize, "Uploads")
        {
        }

        // Constructor dự phòng nếu code cũ chỉ truyền uploadsFolder
        public TemporaryFileManager(string uploadsFolder) : this(8192, uploadsFolder)
        {
        }

        public async Task<string> ReceiveToFileAsync(
            string finalPath,
            long fileSize,
            Stream source,
            string expectedHash,
            int receiveTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            string tempPath = finalPath + ".part";

            try
            {
                // Dùng _chunkSize làm buffer size cho FileStream
                using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (FileStream fileStream =
                    CreateTemporaryFile(tempPath))
                {
                    byte[] buffer = new byte[_chunkSize];
                    long totalBytesRead = 0;

                    while (totalBytesRead < fileSize)
                    {
                        int requestedBytes = (int)Math.Min(buffer.Length, fileSize - totalBytesRead);
                        int bytesRead = await ReadWithIdleTimeoutAsync(
                            source,
                            buffer.AsMemory(0, requestedBytes),
                            receiveTimeoutMs,
                            cancellationToken);

                        if (bytesRead == 0)
                        {
                            throw new EndOfStreamException(
                                $"File bị cắt giữa chừng: nhận {totalBytesRead}/{fileSize} byte.");
                        }

                        try
                        {
                            await fileStream.WriteAsync(
                                buffer.AsMemory(0, bytesRead),
                                cancellationToken);
                        }
                        catch (Exception ex)
                            when (IsStorageException(ex))
                        {
                            throw new StorageException(
                                "Không thể ghi dữ liệu vào file tạm.",
                                ex);
                        }

                        hasher.AppendData(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                    }

                    try
                    {
                        await fileStream.FlushAsync(
                            cancellationToken);
                    }
                    catch (Exception ex)
                        when (IsStorageException(ex))
                    {
                        throw new StorageException(
                            "Không thể hoàn tất ghi file tạm.",
                            ex);
                    }
                }

                string actualHash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ChecksumMismatchException(
                        $"Checksum không khớp. Expected={expectedHash}, Actual={actualHash}.");
                }

                try
                {
                    File.Move(tempPath, finalPath);
                }
                catch (Exception ex)
                    when (IsStorageException(ex))
                {
                    throw new StorageException(
                        "Không thể đổi file tạm thành file chính thức.",
                        ex);
                }

                return finalPath;
            }
            catch (Exception)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
        }

        private FileStream CreateTemporaryFile(
            string tempPath)
        {
            try
            {
                return new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    _chunkSize,
                    useAsync: true);
            }
            catch (Exception ex)
                when (IsStorageException(ex))
            {
                throw new StorageException(
                    "Không thể tạo file tạm trên Server.",
                    ex);
            }
        }

        private static bool IsStorageException(
            Exception exception)
        {
            return exception is IOException or
                UnauthorizedAccessException;
        }

        private static async Task<int> ReadWithIdleTimeoutAsync(
            Stream source,
            Memory<byte> buffer,
            int receiveTimeoutMs,
            CancellationToken cancellationToken)
        {
            using CancellationTokenSource idleCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idleCts.CancelAfter(Math.Max(1, receiveTimeoutMs));

            try
            {
                return await source.ReadAsync(buffer, idleCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Không nhận được dữ liệu mới trong {receiveTimeoutMs} ms.");
            }
        }
    }
}
