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
            ArgumentNullException.ThrowIfNull(stream);

            string? json = await ReadMessageAsync(
                stream,
                cancellationToken);

           
            if (json is null)
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    "Metadata JSON không hợp lệ.",
                    ex);
            }
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
            byte[] lengthBuffer = new byte[sizeof(int)];

            int prefixBytes = await ReadExactAsync(
                stream,
                lengthBuffer,
                cancellationToken);

         
            if (prefixBytes == 0)
            {
                return null;
            }

          
            if (prefixBytes != lengthBuffer.Length)
            {
                throw new EndOfStreamException(
                    "Message bị cắt giữa chừng: thiếu length prefix.");
            }

            int length = BitConverter.ToInt32(
                lengthBuffer,
                0);

            if (length <= 0)
            {
                throw new InvalidDataException(
                    "Metadata length phải lớn hơn 0.");
            }

            if (length > ProtocolConstants.MaxMetadataLength)
            {
                throw new InvalidDataException(
                    $"Metadata vượt quá giới hạn " +
                    $"{ProtocolConstants.MaxMetadataLength} byte.");
            }

            byte[] data = new byte[length];

            int payloadBytes = await ReadExactAsync(
                stream,
                data,
                cancellationToken);

            
            if (payloadBytes != length)
            {
                throw new EndOfStreamException(
                    "Message bị cắt giữa chừng: " +
                    "thiếu metadata payload.");
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