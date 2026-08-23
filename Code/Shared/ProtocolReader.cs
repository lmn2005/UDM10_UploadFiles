using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Shared
{
    public static class ProtocolReader
    {
        public static async Task<T?> ReadMetadataAsync<T>(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            string? json = await ReadMessageAsync(
                stream,
                cancellationToken);

            if (string.IsNullOrEmpty(json))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(json);
        }

        public static Task<UploadRequest?> ReadRequestAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            return ReadMetadataAsync<UploadRequest>(
                stream,
                cancellationToken);
        }

        public static Task<UploadResponse?> ReadResponseAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            return ReadMetadataAsync<UploadResponse>(
                stream,
                cancellationToken);
        }

        private static async Task<string?> ReadMessageAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            byte[] lengthBuffer = new byte[4];

            int read = await ReadExactAsync(
                stream,
                lengthBuffer,
                cancellationToken);

            if (read < 4)
            {
                return null;
            }

            int length = BitConverter.ToInt32(
                lengthBuffer,
                0);

            if (length <= 0 ||
                length > ProtocolConstants.MaxMetadataLength)
            {
                throw new InvalidDataException(
                    "Kích thước metadata không hợp lệ.");
            }

            byte[] data = new byte[length];

            read = await ReadExactAsync(
                stream,
                data,
                cancellationToken);

            if (read < length)
            {
                return null;
            }

            return Encoding.UTF8.GetString(data);
        }

        private static async Task<int> ReadExactAsync(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            int totalRead = 0;

            while (totalRead < buffer.Length)
            {
                int read = await stream.ReadAsync(
                    buffer.AsMemory(
                        totalRead,
                        buffer.Length - totalRead),
                    cancellationToken);

                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }
    }
}