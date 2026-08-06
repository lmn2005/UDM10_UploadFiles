<<<<<<< HEAD
﻿using System;
=======
>>>>>>> main
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UDM10.Shared
{
    public static class ProtocolWriter
    {
<<<<<<< HEAD
        public static void WriteMetadata<T>(NetworkStream stream, T metadata)
        {
           
            string json = JsonSerializer.Serialize(metadata);
            byte[] metadataBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthBuffer = BitConverter.GetBytes(metadataBytes.Length);
            stream.Write(lengthBuffer, 0, lengthBuffer.Length);
            stream.Write(metadataBytes, 0, metadataBytes.Length);

            stream.Flush();
        }
    }
}
=======
        public static async Task WriteMetadataAsync<T>(NetworkStream stream, T metadata, CancellationToken cancellationToken = default)
        {
            string json = JsonSerializer.Serialize(metadata);
            byte[] metadataBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthBuffer = BitConverter.GetBytes(metadataBytes.Length);

            await stream.WriteAsync(lengthBuffer, cancellationToken);
            await stream.WriteAsync(metadataBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }
}
>>>>>>> main
