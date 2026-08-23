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
            _settings =
                settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<UploadResult> UploadFileAsync(
            string filePath,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) ||
                !File.Exists(filePath))
            {
                return UploadResult.Fail(
                    "File không tồn tại.");
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

                using CancellationTokenSource connectCts =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(cancellationToken);

                connectCts.CancelAfter(
                    _settings.Network.ConnectTimeoutMs);

                await client.ConnectAsync(
                    _settings.Network.ServerIp,
                    _settings.Network.Port,
                    connectCts.Token);

                await using NetworkStream stream =
                    client.GetStream();

                string fileHash =
                    await ChunkedFileSender.ComputeHashAsync(
                        filePath,
                        cancellationToken);

                UploadRequest request = new()
                {
                    ProtocolVersion =
                        ProtocolConstants.CurrentVersion,

                    RequestId =
                        Guid.NewGuid().ToString("N"),

                    FileName =
                        fileInfo.Name,

                    FileSize =
                        fileInfo.Length,

                    FileHash =
                        fileHash,

                    Status =
                        UploadStatus.Request
                };

                await ProtocolWriter.WriteMetadataAsync(
                    stream,
                    request,
                    cancellationToken);

                UploadResponse? readyResponse =
                    await ReadResponseAsync(
                        stream,
                        cancellationToken);

                if (readyResponse == null)
                {
                    return UploadResult.Fail(
                        "Server không phản hồi.");
                }

                if (!IsValidResponse(
                        readyResponse,
                        request.RequestId,
                        out string protocolError))
                {
                    return UploadResult.Fail(protocolError);
                }

                if (readyResponse.Status == UploadStatus.Error)
                {
                    return UploadResult.Fail(
                        readyResponse.ErrorMessage ??
                        "Server từ chối upload.");
                }

                if (readyResponse.Status != UploadStatus.Ready)
                {
                    return UploadResult.Fail(
                        "Server trả trạng thái không hợp lệ.");
                }

                progress?.Report(new UploadProgress
                {
                    PercentComplete = 0,
                    BytesTransferred = 0,
                    Status = UploadItemStatus.Uploading,
                    Message =
                        "Server đã sẵn sàng, đang gửi file..."
                });

                int chunkSize =
                    _settings.Upload.ChunkSizeBytes > 0
                        ? _settings.Upload.ChunkSizeBytes
                        : ProtocolConstants.DefaultChunkSize;

                long totalSent = 0;

                Stopwatch stopwatch =
                    Stopwatch.StartNew();

                await ChunkedFileSender.SendFileAsync(
                    stream,
                    filePath,
                    chunkSize,
                    bytesRead =>
                    {
                        totalSent += bytesRead;

                        double percent =
                            fileInfo.Length == 0
                                ? 100
                                : Math.Min(
                                    100,
                                    totalSent * 100d /
                                    fileInfo.Length);

                        double seconds =
                            Math.Max(
                                stopwatch.Elapsed.TotalSeconds,
                                0.001);

                        progress?.Report(
                            new UploadProgress
                            {
                                PercentComplete = percent,
                                SpeedKBps =
                                    totalSent /
                                    1024d /
                                    seconds,
                                BytesTransferred =
                                    totalSent,
                                Status =
                                    UploadItemStatus.Uploading,
                                Message =
                                    "Đang gửi file..."
                            });
                    },
                    cancellationToken);

                UploadResponse? finalResponse =
                    await ReadResponseAsync(
                        stream,
                        cancellationToken);

                if (finalResponse == null)
                {
                    return UploadResult.Fail(
                        "Server không trả kết quả cuối.");
                }

                if (!IsValidResponse(
                        finalResponse,
                        request.RequestId,
                        out string finalError))
                {
                    return UploadResult.Fail(finalError);
                }

                if (finalResponse.Status ==
                    UploadStatus.Completed)
                {
                    return UploadResult.Success(
                        finalResponse.ErrorMessage ??
                        $"Upload thành công: {fileInfo.Name}");
                }

                if (finalResponse.Status ==
                    UploadStatus.Error)
                {
                    return UploadResult.Fail(
                        finalResponse.ErrorMessage ??
                        "Upload thất bại.");
                }

                return UploadResult.Fail(
                    "Server trả trạng thái không hợp lệ.");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
               
                throw;
            }
            catch (SocketException)
            {
                return UploadResult.Fail(
                    "Không kết nối được Server.");
            }
            catch (TimeoutException)
            {
                return UploadResult.Fail(
                    "Server phản hồi quá thời gian.");
            }
            catch (UnauthorizedAccessException)
            {
                return UploadResult.Fail(
                    "Không có quyền đọc file.");
            }
            catch (InvalidDataException)
            {
                return UploadResult.Fail(
                    "Server phản hồi không đúng định dạng.");
            }
            catch (EndOfStreamException)
            {
                return UploadResult.Fail(
                    "Server đóng kết nối trước khi hoàn tất.");
            }
            catch (IOException)
            {
                return UploadResult.Fail(
                    "Mất kết nối trong quá trình upload.");
            }
            catch (Exception ex)
            {
                return UploadResult.Fail(
                    $"Upload lỗi: {ex.Message}");
            }
        }

        private static bool IsValidResponse(
            UploadResponse response,
            string requestId,
            out string error)
        {
            if (response.ProtocolVersion !=
                ProtocolConstants.CurrentVersion)
            {
                error =
                    $"Sai ProtocolVersion: " +
                    $"{response.ProtocolVersion}.";

                return false;
            }

            if (response.RequestId != requestId)
            {
                error =
                    "RequestId của response không khớp.";

                return false;
            }

            error = string.Empty;
            return true;
        }

        private async Task<UploadResponse?> ReadResponseAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            using CancellationTokenSource timeoutCts =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);

            timeoutCts.CancelAfter(
                Math.Max(
                    1,
                    _settings.Network.ReceiveTimeoutMs));

            try
            {
                return await ProtocolReader
                    .ReadMetadataAsync<UploadResponse>(
                        stream,
                        timeoutCts.Token);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }
    }
}