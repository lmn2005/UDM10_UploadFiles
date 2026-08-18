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
            int chunkSize = ProtocolConstants.ChunkSize;
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
            CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[chunkSize];

            await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, true);

            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                onChunkSent?.Invoke(bytesRead);
            }

            await stream.FlushAsync(cancellationToken);
        }
    }
}