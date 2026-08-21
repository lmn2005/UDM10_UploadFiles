using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using UDM10.Shared;

namespace UDM10.TempTestClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== TEMP TEST CLIENT (TIMEOUT TEST) ===");

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", 9000);
                using var stream = client.GetStream();

                // 1. Gửi Metadata Request
                var request = new UploadRequest
                {
                    FileName = "test_timeout.txt",
                    FileSize = 1024 * 1024, // 1 MB
                    FileHash = null
                };

                await ProtocolWriter.WriteMetadataAsync(stream, request);
                Console.WriteLine($"[Client] Sent UploadRequest ID: {request.RequestId}");

                // 2. Nhận phản hồi READY từ Server
                var response = await ProtocolReader.ReadMetadataAsync<UploadResponse>(stream);
                Console.WriteLine($"[Client] Server Response: {response?.Status} - {response?.Message}");

                // 3. Giả lập nghẽn/treo Client: Đứng yên 65 giây (Vượt ngưỡng 60s Timeout của Server)
                Console.WriteLine("[Client] Danger: Dang gia lap ngung truyen du lieu trong 65 giay...");
                await Task.Delay(65000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client Exited/Error]: {ex.Message}");
            }
        }
    }
}