using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Shared
{

    public static class ProtocolWriter
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        public static Task WriteRequestAsync(
            Stream stream,
            UploadRequest request,
            CancellationToken cancellationToken = default)
        {
            return WriteMetadataAsync(
                stream,
                request,
                cancellationToken);
        }

        public static Task WriteResponseAsync(
            Stream stream,
            UploadResponse response,
            CancellationToken cancellationToken = default)
        {
            return WriteMetadataAsync(
                stream,
                response,
                cancellationToken);
        }

        public static async Task WriteMetadataAsync<T>(
            Stream stream,
            T message,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(message);

            string json = JsonSerializer.Serialize(
                message,
                JsonOptions);

            byte[] data = Encoding.UTF8.GetBytes(json);

            if (data.Length == 0 ||
                data.Length > ProtocolConstants.MaxMetadataLength)
            {
                throw new InvalidDataException(
                    $"Metadata phải nằm trong khoảng " +
                    $"1..{ProtocolConstants.MaxMetadataLength} byte.");
            }

            byte[] lengthPrefix =
                BitConverter.GetBytes(data.Length);

            await stream.WriteAsync(
                lengthPrefix.AsMemory(),
                cancellationToken);

            await stream.WriteAsync(
                data.AsMemory(),
                cancellationToken);

            await stream.FlushAsync(
                cancellationToken);
        }
    }
}