using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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

        // Thêm danh sách để theo dõi các luồng Client đang chạy
        private readonly ConcurrentBag<Task> _activeTasks = new();

        public UploadServer(IConfiguration config, ServerLogger logger, FileStorageService storageService)
        {
            _config = config;
            _port = _config.GetValue<int>("Network:Port", 9000);
            _logger = logger;
            _storageService = storageService;
        }

        // Bổ sung CancellationToken
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Đổi mặc định thành "0.0.0.0" để hỗ trợ nhận kết nối qua mạng LAN/Internet
                string ipString = _config.GetValue<string>("Network:ServerIp", "0.0.0.0") ?? "0.0.0.0";
                IPAddress ipAddress = (ipString == "0.0.0.0" || ipString.Equals("Any", StringComparison.OrdinalIgnoreCase))
                                        ? IPAddress.Any
                                        : IPAddress.Parse(ipString);

                _listener = new TcpListener(ipAddress, _port);
                _listener.Start();
                _isRunning = true;

                _logger.LogInfo("Server started successfully!");
                _logger.LogInfo($"Server is listening on {ipAddress}:{_port}");
                Console.WriteLine("Server started successfully!");
                Console.WriteLine($"Server is listening on {ipAddress}:{_port}");

                // Đăng ký tự động gọi hàm Stop() khi nhận được tín hiệu Ctrl+C
                using (cancellationToken.Register(() => Stop()))
                {
                    while (_isRunning && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                            string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown IP";

                            _logger.LogInfo($"New client connected: {clientEndPoint}");
                            Console.WriteLine($"\nNew client connected: {clientEndPoint}");

                            ClientConnectionHandler handler = new ClientConnectionHandler(client, _logger, _storageService, _config);

                            Task sessionTask = Task.Run(async () =>
                            {
                                await handler.HandleAsync(cancellationToken);
                            }, cancellationToken);

                            _activeTasks.Add(sessionTask);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (SocketException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error receiving new connection: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    _logger.LogError($"[Server Startup Error]: {ex.Message}");
                    Console.WriteLine($"[Server Startup Error]: {ex.Message}");
                }
            }
            finally
            {
                _logger.LogWarning("[SHUTDOWN] Waiting for active upload sessions to finish or cleanup...");

                await Task.WhenAll(_activeTasks);

                _logger.LogInfo("[SHUTDOWN] All active sessions closed safely. Server fully stopped.");
                Console.WriteLine("[SHUTDOWN] All active sessions closed safely.");
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _listener?.Stop();

            _logger.LogInfo("Server stopped accepting new connections.");
            Console.WriteLine("Server stopped accepting new connections.");
        }
    }
}