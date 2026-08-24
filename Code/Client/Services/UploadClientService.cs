using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UDM10.Client;
using UDM10.Shared;

namespace UDM10.Client.Services
{
    internal sealed class UploadClientService : IUploadClient
    {
        private readonly ClientSettings _settings;

        public UploadClientService()
            : this(ClientSettings.Load())
        {
        }

        internal UploadClientService(ClientSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<UploadResult> UploadFileAsync(
            string filePath,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return UploadResult.Fail("File không tồn tại.");
            }

            FileInfo fileInfo = new(filePath);

            try
            {
                progress?.Report(new UploadProgress
                {
                    BytesTransferred = 0,
                    Status = UploadItemStatus.Uploading,
                    Message = "Đang kết nối Server..."
                });

                using TcpClient client = new();
                using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(_settings.Network.ConnectTimeoutMs);

                await client.ConnectAsync(_settings.Network.ServerIp, _settings.Network.Port, connectCts.Token);

                client.ReceiveTimeout = _settings.Network.ReceiveTimeoutMs;
                client.SendTimeout = _settings.Network.ReceiveTimeoutMs;

                await using NetworkStream networkStream = client.GetStream();
                UploadRequest request = new()
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length
                };

                await ProtocolWriter.WriteRequestAsync(networkStream, request, cancellationToken);

                var readyResponse = await ReadResponseAsync(networkStream, cancellationToken);

           
                if (readyResponse == null)
                {
                    return UploadResult.Fail("Server không phản hồi yêu cầu ban đầu.");
                }

                if (readyResponse.ProtocolVersion != ProtocolConstants.CurrentVersion)
                {
                    return UploadResult.Fail($"Lỗi giao thức: Phản hồi dùng bản {readyResponse.ProtocolVersion}, yêu cầu bản {ProtocolConstants.CurrentVersion}");
                }

                if (readyResponse.RequestId != request.RequestId)
                {
                    return UploadResult.Fail("Lỗi giao thức: RequestId không khớp với yêu cầu đã gửi.");
                }
            

                if (readyResponse.Status == UploadStatus.Error)
                {
                    return UploadResult.Fail(string.IsNullOrWhiteSpace(readyResponse.ErrorMessage)
                        ? "Server từ chối nhận file."
                        : readyResponse.ErrorMessage);
                }

                if (readyResponse.Status != UploadStatus.Ready)
                {
                    return UploadResult.Fail("Server trả trạng thái không hợp lệ.");
                }

                progress?.Report(new UploadProgress
                {
                    PercentComplete = 0,
                    BytesTransferred = 0,
                    Status = UploadItemStatus.Uploading,
                    Message = "Server đã sẵn sàng, đang gửi file..."
                });

             
                int chunkSize = _settings.Upload.ChunkSizeBytes > 0 ? _settings.Upload.ChunkSizeBytes : 8192;
                long totalSent = 0;
                Stopwatch stopwatch = Stopwatch.StartNew();

                await ChunkedFileSender.SendFileAsync(networkStream, filePath, chunkSize, bytesRead =>
                {
                    totalSent += bytesRead;

                    double percentComplete = fileInfo.Length == 0
                        ? 100
                        : Math.Min(100, totalSent * 100d / fileInfo.Length);
                    double elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);

                    progress?.Report(new UploadProgress
                    {
                        PercentComplete = percentComplete,
                        SpeedKBps = totalSent / 1024d / elapsedSeconds,
                        BytesTransferred = totalSent,
                        Status = UploadItemStatus.Uploading,
                        Message = "Đang gửi file..."
                    });
                }, cancellationToken);

                if (fileInfo.Length == 0)
                {
                    progress?.Report(new UploadProgress
                    {
                        PercentComplete = 100,
                        BytesTransferred = 0,
                        Status = UploadItemStatus.Uploading,
                        Message = "Đang chờ Server xác nhận..."
                    });
                }

                var finalResponse = await ReadResponseAsync(networkStream, cancellationToken);

             
                if (finalResponse != null)
                {
                    if (finalResponse.ProtocolVersion != ProtocolConstants.CurrentVersion)
                    {
                        return UploadResult.Fail($"Lỗi giao thức: Phản hồi dùng bản {finalResponse.ProtocolVersion}, yêu cầu bản {ProtocolConstants.CurrentVersion}");
                    }

                    if (finalResponse.RequestId != request.RequestId)
                    {
                        return UploadResult.Fail("Lỗi giao thức: RequestId không khớp với yêu cầu đã gửi.");
                    }
                }
             

                if (finalResponse?.Status == UploadStatus.Completed)
                {
                    return UploadResult.Success($"Upload thành công: {fileInfo.Name}");
                }

                if (finalResponse is null)
                {
                    return UploadResult.Fail("Server không trả kết quả cuối.");
                }

                if (!string.IsNullOrWhiteSpace(finalResponse.ErrorMessage))
                {
                    return UploadResult.Fail(finalResponse.ErrorMessage);
                }

                return UploadResult.Fail("Upload thất bại.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Để UploadManager phân biệt Cancel với Error và giải phóng đúng slot.
                throw;
            }
            catch (OperationCanceledException)
            {
                return UploadResult.Fail("Không kết nối được Server. Kiểm tra Server đã bật và đúng port.");
            }
            catch (SocketException)
            {
                return UploadResult.Fail("Không kết nối được Server. Kiểm tra Server đã bật và đúng port.");
            }
            catch (TimeoutException)
            {
                return UploadResult.Fail("Server phản hồi quá thời gian chờ.");
            }
            catch (UnauthorizedAccessException)
            {
                return UploadResult.Fail("Không có quyền đọc file.");
            }
            catch (InvalidDataException)
            {
                return UploadResult.Fail("Server phản hồi không đúng định dạng.");
            }
            catch (EndOfStreamException)
            {
                return UploadResult.Fail("Server đóng kết nối trước khi trả kết quả.");
            }
            catch (IOException)
            {
                return UploadResult.Fail("Mất kết nối hoặc không đọc được file.");
            }
            catch (Exception ex)
            {
                return UploadResult.Fail($"Upload lỗi: {ex.Message}");
            }
        }

        private async Task<UploadResponse?> ReadResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Math.Max(1, _settings.Network.ReceiveTimeoutMs));

            try
            {
                return await ProtocolReader.ReadResponseAsync(stream, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Hủy và await trực tiếp thao tác đọc để không để lại task đọc socket chạy ngầm.
                throw new TimeoutException();
            }
        }
    }
}
