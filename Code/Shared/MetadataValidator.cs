using System;
using System.IO;
using System.Linq;
using UDM10.Shared;

namespace UDM10.Server
{

    public static class MetadataValidator
    {
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


            if (request.Status == UploadStatus.Cancel)
            {
                return (
                    true,
                    ErrorCode.None,
                    "Yêu cầu CANCEL hợp lệ.");
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


            if (Path.GetFileName(request.FileName) !=
                    request.FileName ||
                request.FileName.Contains('/') ||
                request.FileName.Contains('\\'))
            {
                return Fail(
                    ErrorCode.InvalidMetadata,
                    "FileName chỉ được chứa tên file, " +
                    "không được chứa đường dẫn.");
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

            if (request.FileHash.Length != 64 ||
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
