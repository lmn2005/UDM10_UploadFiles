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

        private readonly ConcurrentDictionary<long, Task> _activeTasks = new();
        private long _nextSessionId;

        public UploadServer(IConfiguration config, ServerLogger logger, FileStorageService storageService)
        {
            _config = config;
            _port = _config.GetValue<int>("Network:Port", 9000);
            _logger = logger;
            _storageService = storageService;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            string ipString = _config.GetValue<string>("Network:ServerIp", "0.0.0.0") ?? "0.0.0.0";
            IPAddress ipAddress = (ipString == "0.0.0.0" || ipString.Equals("Any", StringComparison.OrdinalIgnoreCase))
                                    ? IPAddress.Any
                                    : IPAddress.Parse(ipString);

            try
            {
                _listener = new TcpListener(ipAddress, _port);

                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                _listener.Start();
                _isRunning = true;

                _logger.LogInfo($"[SYSTEM] Server started successfully! Listening on {ipAddress}:{_port}");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                _logger.LogError($"[BIND ERROR] Port {_port} đang bị chiếm dụng bởi ứng dụng khác (AddressAlreadyInUse).");
                return;
            }
            catch (SocketException ex)
            {
                _logger.LogError($"[BIND ERROR] Không thể bind IP {ipAddress}:{_port}. Chi tiết: {ex.Message} (ErrorCode: {ex.SocketErrorCode})");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SYSTEM ERROR] Lỗi không xác định khi khởi động Server: {ex.Message}");
                return;
            }

            try
            {
                using (cancellationToken.Register(() => Stop()))
                {
                    while (_isRunning && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                            string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown IP";

                            long sessionId = Interlocked.Increment(ref _nextSessionId);
                            string requestId = $"REQ-{sessionId:D4}";

                            _logger.LogInfo($"[{requestId}] Client connected from {clientEndPoint}");

                            Task sessionTask = Task.Run(() => HandleClientWrapperAsync(client, requestId, clientEndPoint, sessionId, cancellationToken));
                            _activeTasks[sessionId] = sessionTask;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (SocketException ex)
                        {
                            if (_isRunning)
                            {
                                _logger.LogError($"[NETWORK] Lỗi AcceptTcpClientAsync: {ex.Message}");
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"[ERROR] Lỗi không xác định khi accept client: {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                _logger.LogWarning("[SHUTDOWN] Đang chờ tất cả active upload sessions dọn dẹp và hoàn tất...");

                if (!_activeTasks.IsEmpty)
                {
                    await Task.WhenAll(_activeTasks.Values);
                }

                _logger.LogInfo("[SHUTDOWN] Tất cả session đã đóng sạch sẽ. Server ngừng hoạt động hoàn toàn.");
            }
        }

        private async Task HandleClientWrapperAsync(
            TcpClient client,
            string requestId,
            string clientIp,
            long sessionId,
            CancellationToken cancellationToken)
        {
            try
            {
                ClientConnectionHandler handler = new ClientConnectionHandler(client, _logger, _storageService, _config);
                await handler.HandleAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogUploadEvent(
                    UploadLifecycleEvent.Error,
                    requestId,
                    clientIp,
                    "N/A",
                    0,
                    $"Unhandled exception: {ex.Message}");
            }
            finally
            {
                _activeTasks.TryRemove(sessionId, out _);
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _listener?.Stop();

            _logger.LogInfo("[SYSTEM] Server đã ngắt kết nối listener và ngừng nhận kết nối mới.");
        }
    }
}