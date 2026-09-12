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

        internal UploadClientService(
            ClientSettings settings)
        {
            _settings =
                settings ??
                throw new ArgumentNullException(
                    nameof(settings));
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

            FileInfo fileInfo =
                new(filePath);

            try
            {
                progress?.Report(
                    new UploadProgress
                    {
                        BytesTransferred = 0,
                        Status =
                            UploadItemStatus.Uploading,
                        ConnectionStatus =
                            ConnectionStatus.Disconnected,
                        Message =
                            "Đang tính SHA-256..."
                    });

                fileInfo.Refresh();
                long sizeBeforeHash = fileInfo.Length;
                DateTime lastWriteBeforeHash = fileInfo.LastWriteTimeUtc;

                // Tính hash trước khi mở kết nối TCP: nếu file lớn khiến việc đọc/hash
                // chậm, Server sẽ không bắt đầu đếm timeout chờ metadata trong lúc đó.
                string fileHash =
                    await ChunkedFileSender
                        .ComputeHashAsync(
                            filePath,
                            cancellationToken);

                fileInfo.Refresh();
                long hashedFileSize = fileInfo.Length;
                DateTime hashedLastWriteUtc = fileInfo.LastWriteTimeUtc;

                if (hashedFileSize != sizeBeforeHash ||
                    hashedLastWriteUtc != lastWriteBeforeHash)
                {
                    return UploadResult.Fail(
                        "File đã thay đổi trong lúc tính SHA-256, " +
                        "hủy upload để tránh gửi metadata sai.");
                }

                progress?.Report(
                    new UploadProgress
                    {
                        BytesTransferred = 0,
                        Status =
                            UploadItemStatus.Uploading,
                        ConnectionStatus =
                            ConnectionStatus.Connecting,
                        Message =
                            "Đang kết nối Server..."
                    });

                using TcpClient client =
                    new();

                using CancellationTokenSource connectCts =
                    CancellationTokenSource
                        .CreateLinkedTokenSource(
                            cancellationToken);

                connectCts.CancelAfter(
                    Math.Max(
                        1,
                        _settings.Network
                            .ConnectTimeoutMs));

                await client.ConnectAsync(
                    _settings.Network.ServerIp,
                    _settings.Network.Port,
                    connectCts.Token);

                await using NetworkStream stream =
                    client.GetStream();

                progress?.Report(
                    new UploadProgress
                    {
                        BytesTransferred = 0,
                        Status =
                            UploadItemStatus.Uploading,
                        ConnectionStatus =
                            ConnectionStatus.Connected,
                        Message =
                            "Đã kết nối Server, " +
                            "đang gửi metadata..."
                    });

                UploadRequest request = new()
                {
                    ProtocolVersion =
                        ProtocolConstants.CurrentVersion,

                    RequestId =
                        Guid.NewGuid().ToString("N"),

                    FileName =
                        fileInfo.Name,

                    FileSize =
                        hashedFileSize,

                    FileHash =
                        fileHash,

                    Status =
                        UploadStatus.Request
                };


                var requestValidation =
    MetadataValidator.Validate(
        request,
        0);

                if (!requestValidation.IsValid)
                {
                    return UploadResult.Fail(
                        $"Request không hợp lệ: " +
                        $"{requestValidation.Message}");
                }

                await ProtocolWriter.WriteRequestAsync(
                    stream,
                    request,
                    cancellationToken);


                UploadResponse? readyResponse =
                    await ReadResponseAsync(
                        stream,
                        cancellationToken);

                if (readyResponse is null)
                {
                    return UploadResult.Fail(
                        "Server không trả kết quả Ready.");
                }

                if (!IsValidResponse(
                        readyResponse,
                        request.RequestId,
                        out string readyError))
                {
                    return UploadResult.Fail(
                        readyError);
                }

                if (readyResponse.Status ==
                    UploadStatus.Error)
                {
                    return UploadResult.Fail(
                        FormatServerError(
                            readyResponse));
                }

                if (readyResponse.Status !=
                    UploadStatus.Ready)
                {
                    return UploadResult.Fail(
                        $"Server trả trạng thái " +
                        $"không hợp lệ: " +
                        $"{readyResponse.Status}.");
                }

                // File có thể bị sửa/ghi đè giữa lúc tính hash và lúc thật sự gửi
                // (ví dụ do một tiến trình khác). Gửi tiếp sẽ khiến Server nhận dữ
                // liệu không khớp fileHash/fileSize đã công bố, nên dừng sớm với
                // thông báo rõ ràng thay vì để Server phát hiện checksum sai sau đó.
                FileInfo currentInfo = new(filePath);
                if (!currentInfo.Exists ||
                    currentInfo.Length != hashedFileSize ||
                    currentInfo.LastWriteTimeUtc != hashedLastWriteUtc)
                {
                    return UploadResult.Fail(
                        "File đã thay đổi sau khi tính SHA-256, " +
                        "hủy upload để tránh gửi sai dữ liệu.");
                }

                progress?.Report(
                    new UploadProgress
                    {
                        PercentComplete = 0,
                        BytesTransferred = 0,
                        Status =
                            UploadItemStatus.Uploading,
                        ConnectionStatus =
                            ConnectionStatus.Connected,
                        Message =
                            "Server đã sẵn sàng, " +
                            "đang gửi file..."
                    });

               

                int chunkSize =
                    _settings.Upload
                        .ChunkSizeBytes > 0
                        ? _settings.Upload
                            .ChunkSizeBytes
                        : ProtocolConstants
                            .DefaultChunkSize;

                if (chunkSize <= 0)
                {
                    chunkSize =
                        ProtocolConstants
                            .DefaultChunkSize;
                }

                int sendTimeoutMs =
                    _settings.Network.SendTimeoutMs > 0
                        ? _settings.Network.SendTimeoutMs
                        : 30000;

                long totalSent = 0;

                Stopwatch stopwatch =
                    Stopwatch.StartNew();

                await ChunkedFileSender.SendFileAsync(
                    stream,
                    filePath,
                    chunkSize,
                    bytesSent =>
                    {
                        totalSent += bytesSent;

                        double percent =
                            fileInfo.Length == 0
                                ? 100
                                : Math.Min(
                                    100,
                                    totalSent * 100d /
                                    fileInfo.Length);

                        double seconds =
                            Math.Max(
                                stopwatch.Elapsed
                                    .TotalSeconds,
                                0.001);

                        progress?.Report(
                            new UploadProgress
                            {
                                PercentComplete =
                                    percent,

                                SpeedKBps =
                                    totalSent /
                                    1024d /
                                    seconds,

                                BytesTransferred =
                                    totalSent,

                                Status =
                                    UploadItemStatus
                                        .Uploading,

                                ConnectionStatus =
                                    ConnectionStatus
                                        .Connected,

                                Message =
                                    "Đang gửi file..."
                            });
                    },
                    sendTimeoutMs: sendTimeoutMs,
                    expectedFileSize: hashedFileSize,
                    expectedLastWriteUtc: hashedLastWriteUtc,
                    cancellationToken: cancellationToken);

               

                UploadResponse? finalResponse =
                    await ReadResponseAsync(
                        stream,
                        cancellationToken);

                if (finalResponse is null)
                {
                    return UploadResult.Fail(
                        "Server không trả kết quả cuối.");
                }

                if (!IsValidResponse(
                        finalResponse,
                        request.RequestId,
                        out string finalError))
                {
                    return UploadResult.Fail(
                        finalError);
                }

                if (finalResponse.Status ==
                    UploadStatus.Completed)
                {
                    string savedFileName =
                        finalResponse.SavedFileName!;

                    string message =
                        string.IsNullOrWhiteSpace(
                            finalResponse.ErrorMessage)
                            ? "Upload thành công."
                            : finalResponse.ErrorMessage.Trim();

                    message += string.Equals(
                            savedFileName,
                            fileInfo.Name,
                            StringComparison.OrdinalIgnoreCase)
                        ? $" Tên file Server đã lưu: " +
                          $"{savedFileName}."
                        : $" Server đổi tên file thành: " +
                          $"{savedFileName}.";

                    return UploadResult.Success(
                        message,
                        savedFileName);
                }

                if (finalResponse.Status ==
                    UploadStatus.Error)
                {
                    return UploadResult.Fail(
                        FormatServerError(
                            finalResponse));
                }

                return UploadResult.Fail(
                    $"Server trả trạng thái không hợp lệ: " +
                    $"{finalResponse.Status}.");
            }
            catch (OperationCanceledException)
                when (
                    cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (SocketException ex)
            {
                return UploadResult.Fail(
                    $"Không kết nối được Server: " +
                    $"{ex.Message}");
            }
            catch (TimeoutException ex)
            {
                return UploadResult.Fail(
                    string.IsNullOrWhiteSpace(ex.Message)
                        ? "Server phản hồi quá thời gian."
                        : ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                return UploadResult.Fail(
                    "Không có quyền đọc file.");
            }
            catch (InvalidDataException ex)
            {
                return UploadResult.Fail(
                    $"Protocol không hợp lệ: " +
                    $"{ex.Message}");
            }
            catch (EndOfStreamException ex)
            {
                return UploadResult.Fail(
                    $"Message bị cắt giữa chừng: " +
                    $"{ex.Message}");
            }
            catch (IOException ex)
            {
                return UploadResult.Fail(
                    $"Mất kết nối trong quá trình " +
                    $"upload: {ex.Message}");
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
            var validation =
                MetadataValidator.ValidateResponse(
                    response);

            if (!validation.IsValid)
            {
                error =
                    $"Response protocol không hợp lệ: " +
                    $"{validation.Message}";

                return false;
            }

            if (!string.Equals(
                    response.RequestId,
                    requestId,
                    StringComparison.Ordinal))
            {
                error =
                    "RequestId của response không khớp " +
                    "với request hiện tại.";

                return false;
            }

            error = string.Empty;
            return true;
        }

        private async Task<UploadResponse?>
            ReadResponseAsync(
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
                    _settings.Network
                        .ReceiveTimeoutMs));

            try
            {
                return await ProtocolReader
                    .ReadResponseAsync(
                        stream,
                        timeoutCts.Token);
            }
            catch (OperationCanceledException)
                when (
                    !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }

        private static string FormatServerError(
            UploadResponse response)
        {
            return $"{response.ErrorCode}: " +
                   $"{response.ErrorMessage}";
        }
    }
}
