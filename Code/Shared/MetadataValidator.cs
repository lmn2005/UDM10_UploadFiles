using UDM10.Shared;

namespace UDM10.Server
{
    public static class MetadataValidator
    {
        public static (bool IsValid, ErrorCode Error, string Message) Validate(UploadRequest request)
        {
            if (request == null)
                return (false, ErrorCode.InvalidMetadata, "Request không tồn tại hoặc sai định dạng JSON.");

            if (request.ProtocolVersion != ProtocolConstants.CurrentVersion)
                return (false, ErrorCode.ProtocolVersionMismatch, $"Sai phiên bản protocol. Server chỉ hỗ trợ {ProtocolConstants.CurrentVersion}.");

            if (string.IsNullOrWhiteSpace(request.RequestId))
                return (false, ErrorCode.MissingRequestId, "Thiếu RequestId (UploadId).");

            if (request.Status == UploadStatus.Cancel)
                return (true, ErrorCode.None, "Valid CANCEL command"); 

            if (string.IsNullOrWhiteSpace(request.FileName))
                return (false, ErrorCode.FileNameEmpty, "Tên file không được để trống.");

            if (request.FileSize <= 0)
                return (false, ErrorCode.FileSizeInvalid, "Kích thước file phải lớn hơn 0.");

            return (true, ErrorCode.None, "Valid");
        }
    }
}