using UDM10.Shared;

namespace UDM10.Server
{
    public static class MetadataValidator
    {
        public static (bool IsValid, ErrorCode ErrorCode, string Message)
            Validate(UploadRequest request, long maxAllowedSize)
        {
            if (request == null)
            {
                return (
                    false,
                    ErrorCode.InvalidMetadata,
                    "Request không tồn tại hoặc sai định dạng."
                );
            }
            if (request.ProtocolVersion != ProtocolConstants.CurrentVersion)
            {
                return (
                    false,
                    ErrorCode.ProtocolVersionMismatch,
                    $"Sai phiên bản protocol. Server yêu cầu {ProtocolConstants.CurrentVersion}."
                );
            }
            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                return (
                    false,
                    ErrorCode.MissingRequestId,
                    "RequestId không được để trống."
                );
            }
            if (request.Status == UploadStatus.Cancel)
            {
                return (
                    true,
                    ErrorCode.None,
                    "Request hợp lệ."
                );
            }
            if (request.Status != UploadStatus.Request &&
                request.Status != UploadStatus.Retry)
            {
                return (
                    false,
                    ErrorCode.UnsupportedStatus,
                    "Status không được hỗ trợ."
                );
            }
            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                return (
                    false,
                    ErrorCode.FileNameEmpty,
                    "Tên file không được để trống."
                );
            }
            if (request.FileSize <= 0)
            {
                return (
                    false,
                    ErrorCode.FileSizeInvalid,
                    "Kích thước file phải lớn hơn 0."
                );
            }
            if (maxAllowedSize > 0 && request.FileSize > maxAllowedSize)
            {
                return (
                    false,
                    ErrorCode.FileSizeInvalid,
                    "Kích thước file vượt quá giới hạn cho phép."
                );
            }
            return (
                true,
                ErrorCode.None,
                "Request hợp lệ."
            );
        }
    }
}