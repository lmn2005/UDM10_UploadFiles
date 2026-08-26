using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Shared
{

    public static class ProtocolWriter
    {
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
                ProtocolSerialization.JsonOptions);

            byte[] data =
                ProtocolSerialization.Utf8.GetBytes(json);

            if (data.Length == 0 ||
                data.Length > ProtocolConstants.MaxMetadataLength)
            {
                throw new InvalidDataException(
                    $"Metadata phải nằm trong khoảng " +
                    $"1..{ProtocolConstants.MaxMetadataLength} byte.");
            }

            byte[] lengthPrefix = new byte[sizeof(int)];

            BinaryPrimitives.WriteInt32LittleEndian(
                lengthPrefix,
                data.Length);

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
