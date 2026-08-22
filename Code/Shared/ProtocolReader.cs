using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Shared
{
    public static class ProtocolReader
    {
        public static async Task<UploadRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            string json = await ReadMessageAsync(stream, cancellationToken);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<UploadRequest>(json);
        }

        public static async Task<UploadResponse> ReadResponseAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            string json = await ReadMessageAsync(stream, cancellationToken);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<UploadResponse>(json);
        }

        private static async Task<string> ReadMessageAsync(Stream stream, CancellationToken cancellationToken)
        {
            byte[] lengthBuffer = new byte[4];
            int read = await ReadExactAsync(stream, lengthBuffer, 4, cancellationToken);
            if (read < 4) return null;

            int length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > ProtocolConstants.MaxMetadataLength)
                throw new InvalidDataException("Kích thước Metadata vượt quá giới hạn hoặc không hợp lệ.");

            byte[] dataBuffer = new byte[length];
            read = await ReadExactAsync(stream, dataBuffer, length, cancellationToken);
            if (read < length) return null;

            return System.Text.Encoding.UTF8.GetString(dataBuffer);
        }

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, totalRead, count - totalRead, cancellationToken);
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        }
    }
}