using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UDM10.Shared
{
    public static class ProtocolReader
    {
       
        public static async Task<byte[]> ReadExactBytesAsync(NetworkStream stream, int length)
        {
            byte[] buffer = new byte[length];
            int totalRead = 0;

            while (totalRead < length)
            {
                int read = await stream.ReadAsync(buffer, totalRead, length - totalRead);
                if (read == 0)
                {
                    throw new Exception("Kết nối bị đóng đột ngột trước khi đọc đủ dữ liệu.");
                }
                totalRead += read;
            }
            return buffer;
        }

        public static async Task<UploadRequest> ReadRequestAsync(NetworkStream stream)
        {
         
            byte[] lengthBuffer = await ReadExactBytesAsync(stream, 4);
            int length = BitConverter.ToInt32(lengthBuffer, 0);

          
            byte[] dataBuffer = await ReadExactBytesAsync(stream, length);
            string json = Encoding.UTF8.GetString(dataBuffer);

            return JsonSerializer.Deserialize<UploadRequest>(json);
        }

        public static async Task<UploadResponse> ReadResponseAsync(NetworkStream stream)
        {
            byte[] lengthBuffer = await ReadExactBytesAsync(stream, 4);
            int length = BitConverter.ToInt32(lengthBuffer, 0);

            byte[] dataBuffer = await ReadExactBytesAsync(stream, length);
            string json = Encoding.UTF8.GetString(dataBuffer);

            return JsonSerializer.Deserialize<UploadResponse>(json);
        }
    }
}