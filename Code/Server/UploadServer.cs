using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UDM10.Server
{
    public class UploadServer
    {
        private readonly int _port;
        private readonly ServerLogger _logger;
        private readonly FileStorageService _storageService;
        private TcpListener? _listener;
        private bool _isRunning;

        public UploadServer(int port, ServerLogger logger, FileStorageService storageService)
        {
            _port = port;
            _logger = logger;
            _storageService = storageService;
        }

        public async Task StartAsync()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                _logger.LogInfo("Server started successfully!");
                _logger.LogInfo($"Server is listening on port {_port}");
                Console.WriteLine("Server started successfully!");
                Console.WriteLine($"Server is listening on port {_port}");

                while (_isRunning)
                {
                    try
                    {
                        TcpClient client = await _listener.AcceptTcpClientAsync();
                        string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown IP";

                        _logger.LogInfo($"New client connected: {clientEndPoint}");
                        Console.WriteLine($"\nNew client connected: {client.Client.RemoteEndPoint}");

                        ClientConnectionHandler handler = new ClientConnectionHandler(client, _logger, _storageService);

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
