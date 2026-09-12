using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Shared
{
    public static class ChunkedFileSender
    {
        public static async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default)
        {
            int chunkSize = ProtocolConstants.DefaultChunkSize;
            using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[chunkSize];

            await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, true);


            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                hasher.AppendData(buffer, 0, bytesRead);
            }

            return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        }

        public static async Task SendFileAsync(
            NetworkStream stream,
            string filePath,
            int chunkSize,
            Action<int>? onChunkSent = null,
            int sendTimeoutMs = Timeout.Infinite,
            long? expectedFileSize = null,
            DateTime? expectedLastWriteUtc = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "Đường dẫn file không được để trống.",
                    nameof(filePath));
            }

            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chunkSize),
                    "Chunk size phải lớn hơn 0.");
            }

            byte[] buffer = new byte[chunkSize];

            await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, true);

            if (expectedFileSize.HasValue &&
                fileStream.Length != expectedFileSize.Value)
            {
                throw new IOException(
                    "Kích thước file đã thay đổi trước khi bắt đầu gửi.");
            }

            if (expectedLastWriteUtc.HasValue &&
                File.GetLastWriteTimeUtc(filePath) != expectedLastWriteUtc.Value)
            {
                throw new IOException(
                    "Thời điểm sửa file đã thay đổi trước khi bắt đầu gửi.");
            }

            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await WriteWithIdleTimeoutAsync(stream, buffer.AsMemory(0, bytesRead), sendTimeoutMs, cancellationToken);
                onChunkSent?.Invoke(bytesRead);
            }

            if (expectedFileSize.HasValue &&
                (fileStream.Position != expectedFileSize.Value ||
                 fileStream.Length != expectedFileSize.Value))
            {
                throw new IOException(
                    "File đã thay đổi trong quá trình gửi.");
            }

            await stream.FlushAsync(cancellationToken);
        }

        // Nếu Server giữ socket nhưng ngừng đọc (ví dụ session bị treo), WriteAsync có thể
        // chờ vô hạn khi buffer gửi đầy. Idle timeout đảm bảo Client tự thoát với lỗi rõ ràng
        // thay vì treo, và tách biệt với việc người dùng chủ động Cancel.
        private static async Task WriteWithIdleTimeoutAsync(
            NetworkStream stream,
            ReadOnlyMemory<byte> buffer,
            int sendTimeoutMs,
            CancellationToken cancellationToken)
        {
            if (sendTimeoutMs == Timeout.Infinite)
            {
                await stream.WriteAsync(buffer, cancellationToken);
                return;
            }

            using CancellationTokenSource idleCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idleCts.CancelAfter(Math.Max(1, sendTimeoutMs));

            try
            {
                await stream.WriteAsync(buffer, idleCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Không gửi được dữ liệu trong {sendTimeoutMs} ms. Server có thể đã ngừng đọc.");
            }
        }
    }
}
