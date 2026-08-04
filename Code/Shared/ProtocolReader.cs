using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UDM10.Shared
{
    public static class ProtocolReader
    {
       
        public static byte[] ReadExactBytes(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int totalRead = 0;

            while (totalRead < length)
            {
                int read = stream.Read(buffer, totalRead, length - totalRead);
                if (read == 0)
                {
                    throw new EndOfStreamException("Kết nối bị đóng đột ngột trước khi đọc đủ dữ liệu.");
                }
                totalRead += read;
            }
            return buffer;
        }

        public static T ReadMetadata<T>(NetworkStream stream)
        {
      
            byte[] lengthBuffer = ReadExactBytes(stream, 4);
            int metadataLength = BitConverter.ToInt32(lengthBuffer, 0);

           
            byte[] metadataBytes = ReadExactBytes(stream, metadataLength);

           
            string json = Encoding.UTF8.GetString(metadataBytes);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}