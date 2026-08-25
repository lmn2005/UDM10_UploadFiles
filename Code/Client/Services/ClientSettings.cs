using System.IO;
using System.Text.Json;

namespace UDM10.Client.Services
{
    internal sealed class ClientSettings
    {
        public NetworkSettings Network { get; set; } = new();
        public UploadSettings Upload { get; set; } = new();

        public static ClientSettings Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
            {
                return new ClientSettings();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ClientSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new ClientSettings();
            }
            catch (IOException)
            {
                return new ClientSettings();
            }
            catch (JsonException)
            {
                return new ClientSettings();
            }
        }
    }

    internal sealed class NetworkSettings
    {
        public string ServerIp { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 9000;
        public int ConnectTimeoutMs { get; set; } = 5000;
        public int ReceiveTimeoutMs { get; set; } = 10000;
    }

    internal sealed class UploadSettings
    {
        public int MaxConcurrentFiles { get; set; } = 3;
        public int ChunkSizeBytes { get; set; } = 8192;
        public string SaveDirectory { get; set; } = "Uploads";
    }
}
