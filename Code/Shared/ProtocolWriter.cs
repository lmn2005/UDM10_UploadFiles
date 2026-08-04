using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UDM10.Shared
{
    public static class ProtocolWriter
    {
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