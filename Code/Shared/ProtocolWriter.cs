using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Shared
{
    public static class ProtocolWriter
    {
        public static async Task WriteRequestAsync(Stream stream, UploadRequest request, CancellationToken cancellationToken = default)
        {
            string json = JsonSerializer.Serialize(request);
            await WriteMessageAsync(stream, json, cancellationToken);
        }

        public static async Task WriteResponseAsync(Stream stream, UploadResponse response, CancellationToken cancellationToken = default)
        {
            string json = JsonSerializer.Serialize(response);
            await WriteMessageAsync(stream, json, cancellationToken);
        }

        // Backwards-compatible method name used across the solution
        public static Task WriteMetadataAsync(Stream stream, UploadRequest request, CancellationToken cancellationToken = default)
            => WriteRequestAsync(stream, request, cancellationToken);

        public static Task WriteMetadataAsync(Stream stream, UploadResponse response, CancellationToken cancellationToken = default)
            => WriteResponseAsync(stream, response, cancellationToken);

        private static async Task WriteMessageAsync(Stream stream, string message, CancellationToken cancellationToken)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
            byte[] lengthPrefix = BitConverter.GetBytes(data.Length);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length, cancellationToken);
            await stream.WriteAsync(data, 0, data.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }
}
