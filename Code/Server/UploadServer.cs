using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace UDM10.Server
{
    public class UploadServer
    {
        private readonly IConfiguration _config;
        private readonly int _port;
        private readonly ServerLogger _logger;
        private readonly FileStorageService _storageService;
        private TcpListener? _listener;
        private bool _isRunning;

        public UploadServer(IConfiguration config, ServerLogger logger, FileStorageService storageService)
        {
            _config = config;
            _port = _config.GetValue<int>("Network:Port", 9000);
            _logger = logger;
            _storageService = storageService;
        }

        public async Task StartAsync()
        {
            try
            {
                string ipString = _config.GetValue<string>("Network:ServerIp", "127.0.0.1") ?? "127.0.0.1";
                IPAddress ipAddress = IPAddress.Parse(ipString);

                _listener = new TcpListener(ipAddress, _port);
                _listener.Start();
                _isRunning = true;

                _logger.LogInfo("Server started successfully!");
                _logger.LogInfo($"Server is listening on {ipAddress}:{_port}");
                Console.WriteLine("Server started successfully!");
                Console.WriteLine($"Server is listening on {ipAddress}:{_port}");

                while (_isRunning)
                {
                    try
                    {
                        TcpClient client = await _listener.AcceptTcpClientAsync();
                        string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown IP";

                        _logger.LogInfo($"New client connected: {clientEndPoint}");
                        Console.WriteLine($"\nNew client connected: {clientEndPoint}");

                        ClientConnectionHandler handler = new ClientConnectionHandler(client, _logger, _storageService, _config);

                        _ = handler.HandleAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error receiving new connection: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    _logger.LogInfo($"[Server Startup Error]: {ex.Message}");
                    Console.WriteLine($"[Server Startup Error]: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();

            _logger.LogInfo("Server stopped.");
            Console.WriteLine("Server stopped.");
        }
    }
}