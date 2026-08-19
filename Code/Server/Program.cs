using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace UDM10.Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== UDM10 SERVER STARTING ===");

            IConfiguration config;

            try
            {
                // Load cấu hình chuẩn từ file appsettings.json
                config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to read appsettings.json: {ex.Message}");
                return;
            }

            // Khởi tạo các dịch vụ với IConfiguration
            ServerLogger logger = new ServerLogger("Logs/server_log.txt");
            FileStorageService storageService = new FileStorageService(config, logger);
            UploadServer server = new UploadServer(config, logger, storageService);

            await server.StartAsync();
        }
    }
}