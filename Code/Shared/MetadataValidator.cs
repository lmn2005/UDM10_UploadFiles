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

            if (string.IsNullOrWhiteSpace(
                    request.FileName))
            {
                return Fail(
                    ErrorCode.FileNameEmpty,
                    "FileName không được để trống.");
            }

            if (request.FileName.Length >
                ProtocolConstants.MaxFileNameLength)
            {
                return Fail(
                    ErrorCode.InvalidMetadata,
                    "FileName vượt quá độ dài cho phép.");
            }

            if (!string.Equals(
                    request.FileName,
                    request.FileName.Trim(),
                    StringComparison.Ordinal) ||
                request.FileName.EndsWith('.') ||
                request.FileName.Any(char.IsControl) ||
                request.FileName.IndexOfAny(
                    InvalidFileNameCharacters) >= 0)
            {
                return Fail(
                    ErrorCode.InvalidMetadata,
                    "FileName không hợp lệ trên Windows hoặc chứa đường dẫn.");
            }

            string baseName = request.FileName.Split('.')[0];

            if (request.FileName is "." or ".." ||
                ReservedWindowsNames.Contains(
                    baseName,
                    StringComparer.OrdinalIgnoreCase))
            {
                return Fail(
                    ErrorCode.InvalidMetadata,
                    "FileName là tên thiết bị dành riêng trên Windows.");
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
