using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UDM10.Shared
{
    public static class ProtocolReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<T?> ReadMetadataAsync<T>(NetworkStream stream, CancellationToken cancellationToken = default)
        {
            byte[] lengthBuffer = await ReadExactBytesAsync(stream, 4, cancellationToken);
            int metadataLength = BitConverter.ToInt32(lengthBuffer, 0);

            if (metadataLength <= 0)
            {
                throw new InvalidDataException("Metadata không hợp lệ.");
            }

            byte[] metadataBytes = await ReadExactBytesAsync(stream, metadataLength, cancellationToken);
            string json = Encoding.UTF8.GetString(metadataBytes);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        private static async Task<byte[]> ReadExactBytesAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[length];
            int totalRead = 0;

            while (totalRead < length)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, length - totalRead), cancellationToken);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Kết nối bị đóng.");
                }

                totalRead += bytesRead;
            }

            return buffer;
        }
    }
}
