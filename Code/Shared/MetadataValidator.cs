using System;
using System.Linq;

namespace UDM10.Shared
{
    public static class MetadataValidator
    {
        private static readonly char[] InvalidFileNameCharacters =
            ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

        private static readonly string[] ReservedWindowsNames =
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5",
            "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
            "LPT6", "LPT7", "LPT8", "LPT9"
        ];

        public static (
            bool IsValid,
            ErrorCode ErrorCode,
            string Message) Validate(
                UploadRequest? request,
                long maxAllowedSize)
        {
            if (request is null)
            {
                return Fail(
                    ErrorCode.InvalidMetadata,
                    "Metadata request không tồn tại hoặc không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(
                    request.ProtocolVersion))
            {
                return Fail(
                    ErrorCode.ProtocolVersionMismatch,
                    "ProtocolVersion không được để trống.");
            }

            if (!string.Equals(
                    request.ProtocolVersion,
                    ProtocolConstants.CurrentVersion,
                    StringComparison.Ordinal))
            {
                return Fail(
                    ErrorCode.ProtocolVersionMismatch,
                    $"ProtocolVersion không được hỗ trợ. " +
                    $"Server yêu cầu " +
                    $"{ProtocolConstants.CurrentVersion}.");
            }

            if (string.IsNullOrWhiteSpace(
                    request.RequestId))
            {
                return Fail(
                    ErrorCode.MissingRequestId,
                    "RequestId không được để trống.");
            }

            if (request.RequestId.Length >
                ProtocolConstants.MaxRequestIdLength)
            {
                return Fail(
                    ErrorCode.InvalidMetadata,
                    "RequestId vượt quá độ dài cho phép.");
            }

            if (!string.Equals(
                    request.RequestId,
                    request.RequestId.Trim(),
                    StringComparison.Ordinal) ||
                request.RequestId.Any(char.IsControl))
            {
                return Fail(
                    ErrorCode.InvalidMetadata,
                    "RequestId không được chứa khoảng trắng ở đầu/cuối " +
                    "hoặc ký tự điều khiển.");
            }

            if (request.Status != UploadStatus.Request &&
                request.Status != UploadStatus.Retry)
            {
                return Fail(
                    ErrorCode.UnsupportedStatus,
                    "Status không được hỗ trợ trong Protocol v3.");
            }

            if (!IsValidFileName(
                    request.FileName,
                    out string fileNameError))
            {
                if (string.IsNullOrWhiteSpace(request.FileName))
                {
                    return Fail(
                        ErrorCode.FileNameEmpty,
                        "FileName không được để trống.");
                }

                return Fail(
                    ErrorCode.InvalidMetadata,
                    $"FileName {fileNameError}");
            }

            if (request.FileSize < 0)
            {
                return Fail(
                    ErrorCode.FileSizeInvalid,
                    "FileSize không được là số âm.");
            }

            if (maxAllowedSize > 0 &&
                request.FileSize > maxAllowedSize)
            {
                return Fail(
                    ErrorCode.FileSizeInvalid,
                    $"FileSize vượt quá giới hạn " +
                    $"{maxAllowedSize} byte.");
            }

            if (string.IsNullOrWhiteSpace(request.FileHash) ||
                request.FileHash.Length != 64 ||
                !request.FileHash.All(Uri.IsHexDigit))
            {
                return Fail(
                    ErrorCode.InvalidMetadata,
                    "FileHash phải là chuỗi SHA-256 gồm đúng 64 ký tự hex.");
            }

            return (
                true,
                ErrorCode.None,
                "Metadata hợp lệ.");
        }

        public static (
    bool IsValid,
    string Message) ValidateResponse(
        UploadResponse? response)
        {
            if (response is null)
            {
                return ResponseFail(
                    "Response không tồn tại.");
            }

            if (string.IsNullOrWhiteSpace(
                    response.ProtocolVersion))
            {
                return ResponseFail(
                    "Response ProtocolVersion không được để trống.");
            }

            if (!string.Equals(
                    response.ProtocolVersion,
                    ProtocolConstants.CurrentVersion,
                    StringComparison.Ordinal))
            {
                return ResponseFail(
                    $"Response ProtocolVersion không hợp lệ. " +
                    $"Yêu cầu {ProtocolConstants.CurrentVersion}.");
            }

            if (string.IsNullOrWhiteSpace(
                    response.RequestId))
            {
                return ResponseFail(
                    "Response RequestId không được để trống.");
            }

            if (response.RequestId.Length >
                ProtocolConstants.MaxRequestIdLength)
            {
                return ResponseFail(
                    "Response RequestId vượt quá độ dài cho phép.");
            }

            if (!string.Equals(
                    response.RequestId,
                    response.RequestId.Trim(),
                    StringComparison.Ordinal) ||
                response.RequestId.Any(char.IsControl))
            {
                return ResponseFail(
                    "Response RequestId không được chứa khoảng trắng ở đầu/cuối " +
                    "hoặc ký tự điều khiển.");
            }

            if (!Enum.IsDefined(response.Status))
            {
                return ResponseFail(
                    $"Response có Status không hợp lệ: " +
                    $"{(int)response.Status}.");
            }

            if (!Enum.IsDefined(response.ErrorCode))
            {
                return ResponseFail(
                    $"Response có ErrorCode không hợp lệ: " +
                    $"{(int)response.ErrorCode}.");
            }

            switch (response.Status)
            {
                case UploadStatus.Ready:

                    if (response.ErrorCode != ErrorCode.None)
                    {
                        return ResponseFail(
                            "Response Ready không được chứa ErrorCode.");
                    }

                    if (!string.IsNullOrEmpty(
                            response.SavedFileName))
                    {
                        return ResponseFail(
                            "Response Ready không được chứa SavedFileName.");
                    }

                    break;

                case UploadStatus.Completed:

                    if (response.ErrorCode != ErrorCode.None)
                    {
                        return ResponseFail(
                            "Response Completed không được chứa ErrorCode.");
                    }

                    if (!IsValidFileName(
                            response.SavedFileName,
                            out string savedFileNameError))
                    {
                        return ResponseFail(
                            $"SavedFileName {savedFileNameError}");
                    }

                    break;

                case UploadStatus.Error:

                    if (response.ErrorCode == ErrorCode.None)
                    {
                        return ResponseFail(
                            "Response Error phải có ErrorCode.");
                    }

                    if (string.IsNullOrWhiteSpace(
                            response.ErrorMessage))
                    {
                        return ResponseFail(
                            "Response Error phải có ErrorMessage.");
                    }

                    if (!string.IsNullOrEmpty(
                            response.SavedFileName))
                    {
                        return ResponseFail(
                            "Response Error không được chứa SavedFileName.");
                    }

                    break;

                default:

                    return ResponseFail(
                        $"Status {response.Status} không được phép " +
                        "xuất hiện trong UploadResponse.");
            }

            return (true, string.Empty);
        }

        private static bool IsValidFileName(
            string? fileName,
            out string message)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                message = "không được để trống.";
                return false;
            }

            if (fileName.Length >
                ProtocolConstants.MaxFileNameLength)
            {
                message = "vượt quá độ dài cho phép.";
                return false;
            }

            if (!string.Equals(
                    fileName,
                    fileName.Trim(),
                    StringComparison.Ordinal) ||
                fileName.EndsWith('.') ||
                fileName.Any(char.IsControl) ||
                fileName.IndexOfAny(
                    InvalidFileNameCharacters) >= 0)
            {
                message =
                    "không hợp lệ trên Windows hoặc chứa đường dẫn.";

                return false;
            }

            string baseName = fileName.Split('.')[0];

            if (fileName is "." or ".." ||
                ReservedWindowsNames.Contains(
                    baseName,
                    StringComparer.OrdinalIgnoreCase))
            {
                message =
                    "là tên thiết bị dành riêng trên Windows.";

                return false;
            }

            message = string.Empty;
            return true;
        }

        private static (
            bool IsValid,
            string Message) ResponseFail(
                string message)
        {
            return (false, message);
        }

        private static (
            bool IsValid,
            ErrorCode ErrorCode,
            string Message) Fail(
                ErrorCode errorCode,
                string message)
        {
            return (
                false,
                errorCode,
                message);
        }
    }
}