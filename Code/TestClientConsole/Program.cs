using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UDM10.Shared;

namespace TestClientConsole
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string serverIp = args.Length > 1 ? args[1] : "127.0.0.1";
            int port = args.Length > 2 && int.TryParse(args[2], out int configuredPort)
                && configuredPort is >= 1 and <= 65535
                    ? configuredPort
                    : 9000;
            CancellationToken cancellationToken = CancellationToken.None;

            string filePath = args.Length > 0
                ? args[0]
                : Path.Combine(AppContext.BaseDirectory, "sample_test.txt");

            Console.WriteLine("Cú pháp: dotnet run --project Code/TestClientConsole -- <file> <server-ip> <port>");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Không tìm thấy file: {filePath}");
                Console.WriteLine("Tạo file mẫu để test...");
                File.WriteAllText(filePath, "Đây là nội dung file test upload UDM10 - " + DateTime.Now);
            }

            var fileInfo = new FileInfo(filePath);
            Console.WriteLine($"Chuẩn bị upload: {fileInfo.Name} ({fileInfo.Length} bytes)");

            try
            {
                using TcpClient client = new TcpClient();
                Console.WriteLine($"Đang kết nối tới {serverIp}:{port}...");
                await client.ConnectAsync(serverIp, port);
                Console.WriteLine("Đã kết nối.");

                using NetworkStream stream = client.GetStream();
                string fileHash = await ChunkedFileSender.ComputeHashAsync(filePath, cancellationToken);
                Console.WriteLine($"Hash tính được: {fileHash}");
                var request = new UploadRequest
                {
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    FileHash = fileHash
                };

                Console.WriteLine($"Gửi metadata (RequestId: {request.RequestId})...");
                await ProtocolWriter.WriteRequestAsync(stream, request, cancellationToken);

                var readyResponse = await ProtocolReader.ReadMetadataAsync<UploadResponse>(stream, cancellationToken);
                Console.WriteLine($"Phản hồi Ready: Status={readyResponse?.Status}, Message={readyResponse?.Message}");

                if (readyResponse?.Status != UploadStatus.Ready)
                {
                    Console.WriteLine("Server không sẵn sàng nhận file. Dừng lại.");
                    return;
                }

                Console.WriteLine("Đang gửi dữ liệu file...");
                await ChunkedFileSender.SendFileAsync(stream, filePath, ProtocolConstants.ChunkSize, cancellationToken: cancellationToken);

                var finalResponse = await ProtocolReader.ReadMetadataAsync<UploadResponse>(stream, cancellationToken);
                Console.WriteLine($"Phản hồi cuối: Status={finalResponse?.Status}, Message={finalResponse?.Message}");

                if (finalResponse?.Status == UploadStatus.Completed)
                {
                    Console.WriteLine("✅ UPLOAD THÀNH CÔNG!");
                }
                else
                {
                    Console.WriteLine("❌ UPLOAD THẤT BẠI: " + finalResponse?.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ LỖI: {ex.Message}");
            }
        }
    }
}
