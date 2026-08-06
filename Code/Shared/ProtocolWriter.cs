using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UDM10.Shared
{
    public static class ProtocolWriter
    {
        
        public static async Task WriteRequestAsync(NetworkStream stream, UploadRequest request)
        {
            string json = JsonSerializer.Serialize(request);
            byte[] data = Encoding.UTF8.GetBytes(json);

           
            byte[] lengthPrefix = BitConverter.GetBytes(data.Length);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
            await stream.WriteAsync(data, 0, data.Length);
        }

    
        public static async Task WriteResponseAsync(NetworkStream stream, UploadResponse response)
        {
            string json = JsonSerializer.Serialize(response);
            byte[] data = Encoding.UTF8.GetBytes(json);

   
            byte[] lengthPrefix = BitConverter.GetBytes(data.Length);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
            await stream.WriteAsync(data, 0, data.Length);
        }
    }
}