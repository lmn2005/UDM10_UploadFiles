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

            FileInfo fileInfo = new(filePath);

            try
            {
                progress?.Report(
                    new UploadProgress
                    {
                        BytesTransferred = 0,
                        Status =
                            UploadItemStatus.Uploading,
                        Message =
                            "Đang kết nối Server..."
                    });

                using TcpClient client = new();

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

               
                string fileHash =
                    await ChunkedFileSender
                        .ComputeHashAsync(
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
                        "Server không phản hồi Ready.");
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

                progress?.Report(
                    new UploadProgress
                    {
                        PercentComplete = 0,
                        BytesTransferred = 0,
                        Status =
                            UploadItemStatus.Uploading,
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

                                Message =
                                    "Đang gửi file..."
                            });
                    },
                    cancellationToken);

               
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
                    return UploadResult.Success(
                        string.IsNullOrWhiteSpace(
                            finalResponse.ErrorMessage)
                            ? $"Upload thành công: " +
                              $"{fileInfo.Name}"
                            : finalResponse
                                .ErrorMessage);
                }

                if (finalResponse.Status ==
                    UploadStatus.Error)
                {
                    return UploadResult.Fail(
                        FormatServerError(
                            finalResponse));
                }

                return UploadResult.Fail(
                    $"Server trả trạng thái " +
                    $"không hợp lệ: " +
                    $"{finalResponse.Status}.");
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
              
                throw;
            }
            catch (SocketException ex)
            {
                return UploadResult.Fail(
                    $"Không kết nối được Server: " +
                    $"{ex.Message}");
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
            if (response is null)
            {
                error =
                    "Response không tồn tại.";

                return false;
            }

        
            if (string.IsNullOrWhiteSpace(
                    response.ProtocolVersion))
            {
                error =
                    "Response thiếu ProtocolVersion.";

                return false;
            }

            if (!string.Equals(
                    response.ProtocolVersion,
                    ProtocolConstants.CurrentVersion,
                    StringComparison.Ordinal))
            {
                error =
                    $"Sai ProtocolVersion: " +
                    $"Server trả " +
                    $"{response.ProtocolVersion}, " +
                    $"Client yêu cầu " +
                    $"{ProtocolConstants.CurrentVersion}.";

                return false;
            }

          
            if (string.IsNullOrWhiteSpace(
                    response.RequestId))
            {
                error =
                    "Response thiếu RequestId.";

                return false;
            }

            
            if (!string.Equals(
                    response.RequestId,
                    requestId,
                    StringComparison.Ordinal))
            {
                error =
                    "RequestId của response " +
                    "không khớp với request hiện tại.";

                return false;
            }

            if (!Enum.IsDefined(response.Status) ||
                response.Status is UploadStatus.None or
                    UploadStatus.Request or
                    UploadStatus.Cancel or
                    UploadStatus.Retry)
            {
                error =
                    $"Response có Status không hợp lệ: " +
                    $"{response.Status}.";

                return false;
            }

            if (!Enum.IsDefined(response.ErrorCode))
            {
                error =
                    $"Response có ErrorCode không tồn tại: " +
                    $"{(int)response.ErrorCode}.";

                return false;
            }

            if (response.Status == UploadStatus.Error)
            {
                if (response.ErrorCode == ErrorCode.None ||
                    string.IsNullOrWhiteSpace(
                        response.ErrorMessage))
                {
                    error =
                        "Response Error phải có ErrorCode và ErrorMessage.";

                    return false;
                }
            }
            else if (response.ErrorCode != ErrorCode.None)
            {
                error =
                    "Response thành công không được chứa ErrorCode.";

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
                when (!cancellationToken
                    .IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }

        private static string FormatServerError(
            UploadResponse response)
        {
            string code =
                response.ErrorCode.ToString();

            string message =
                string.IsNullOrWhiteSpace(
                    response.ErrorMessage)
                    ? "Server từ chối yêu cầu."
                    : response.ErrorMessage;

            return $"{code}: {message}";
        }
    }
}
