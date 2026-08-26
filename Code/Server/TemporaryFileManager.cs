using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UDM10.Shared;

namespace UDM10.Server
{
    public class TemporaryFileManager
    {
        private readonly int _chunkSize;
        private readonly string _uploadsFolder;

        public TemporaryFileManager(int chunkSize, string uploadsFolder)
        {
            _chunkSize = chunkSize > 0 ? chunkSize : 8192;
            _uploadsFolder = string.IsNullOrEmpty(uploadsFolder) ? "Uploads" : uploadsFolder;

            if (!Directory.Exists(_uploadsFolder))
            {
                Directory.CreateDirectory(_uploadsFolder);
            }
        }

        public TemporaryFileManager(int chunkSize) : this(chunkSize, "Uploads") { }

        public TemporaryFileManager(string uploadsFolder) : this(8192, uploadsFolder) { }

        public async Task<string> ReceiveToFileAsync(
            string finalPath,
            long fileSize,
            Stream source,
            string? expectedHash,
            int receiveTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            string tempPath = finalPath + ".part";

            try
            {
                using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                await using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, _chunkSize, true))
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

                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        hasher.AppendData(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                    }

                    await fileStream.FlushAsync(cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(expectedHash))
                {
                    string actualHash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ChecksumMismatchException(
                            $"Checksum không khớp. Expected={expectedHash}, Actual={actualHash}.");
                    }
                }

                File.Move(tempPath, finalPath, overwrite: true);
                return finalPath;
            }
            catch (Exception)
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch 
                    { 
                        
                    }
                }
                throw;
            }
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