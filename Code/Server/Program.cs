using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace UDM10.Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== UDM10 SERVER STARTING ===");
            int port = 9000;


            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(basePath, "appsettings.json");

                string jsonString = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(jsonString);

                port = doc.RootElement.GetProperty("Network").GetProperty("Port").GetInt32();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to read appsettings.json: {ex.Message}");
                Console.WriteLine($"Using default port: {port}");
            }

            ServerLogger logger = new ServerLogger("Logs/server_log.txt");
            FileStorageService storageService = new FileStorageService("Uploads/", logger);

            UploadServer server = new UploadServer(port, logger, storageService);
            await server.StartAsync();
        }
    }
}