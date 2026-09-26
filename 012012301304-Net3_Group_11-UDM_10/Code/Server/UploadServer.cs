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

        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            string ipString = _config.GetValue<string>("Network:ServerIp", "0.0.0.0") ?? "0.0.0.0";
            IPAddress ipAddress;

            if (ipString == "0.0.0.0" ||
                ipString.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                ipAddress = IPAddress.Any;
            }
            else if (!IPAddress.TryParse(ipString, out ipAddress!))
            {
                _logger.LogError(
                    $"[CONFIG ERROR] Network:ServerIp '{ipString}' không phải địa chỉ IP hợp lệ.");
                return false;
            }

            if (_port is < 1 or > 65535)
            {
                _logger.LogError(
                    $"[CONFIG ERROR] Network:Port phải nằm trong khoảng 1..65535, giá trị hiện tại: {_port}.");
                return false;
            }

            try
            {
                _listener = new TcpListener(ipAddress, _port);
                _listener.Server.ExclusiveAddressUse = true;
                _listener.Start();
                _isRunning = true;

                _logger.LogInfo($"[SYSTEM] Server started successfully! Listening on {ipAddress}:{_port}");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                _logger.LogError($"[BIND ERROR] Port {_port} đang bị chiếm dụng bởi ứng dụng khác (AddressAlreadyInUse).");
                return false;
            }
            catch (SocketException ex)
            {
                _logger.LogError($"[BIND ERROR] Không thể bind IP {ipAddress}:{_port}. Chi tiết: {ex.Message} (ErrorCode: {ex.SocketErrorCode})");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SYSTEM ERROR] Lỗi không xác định khi khởi động Server: {ex.Message}");
                return false;
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

                            TaskCompletionSource sessionCompletion =
                                new(TaskCreationOptions.RunContinuationsAsynchronously);

                            _activeTasks[sessionId] = sessionCompletion.Task;

                            _ = HandleClientWrapperAsync(
                                client,
                                requestId,
                                clientEndPoint,
                                sessionId,
                                sessionCompletion,
                                cancellationToken);
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
                    Task allTasks = Task.WhenAll(_activeTasks.Values);
                    Task timeoutTask = Task.Delay(5000);

                    if (await Task.WhenAny(allTasks, timeoutTask) == timeoutTask)
                    {
                        _logger.LogWarning("[SHUTDOWN] Đã hết thời hạn 5s chờ dọn dẹp session. Buộc kết thúc tiến trình Server.");
                    }
                }

                _logger.LogInfo("[SHUTDOWN] Tất cả session đã đóng sạch sẽ. Server ngừng hoạt động hoàn toàn.");
            }

            return true;
        }

        private async Task HandleClientWrapperAsync(
            TcpClient client,
            string requestId,
            string clientIp,
            long sessionId,
            TaskCompletionSource sessionCompletion,
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
                    $"Unhandled exception trong session: {ex.Message}");
            }
            finally
            {
                sessionCompletion.TrySetResult();
                _activeTasks.TryRemove(sessionId, out _);
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            try
            {
                _listener?.Stop();
            }
            catch
            {
                // Bỏ qua lỗi ngắt socket ngắt đột ngột
            }

            _logger.LogInfo("[SYSTEM] Server đã ngắt kết nối listener và ngừng nhận kết nối mới.");
        }
    }
}