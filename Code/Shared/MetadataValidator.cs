using System.IO;

namespace UDM10.Shared
{
    public static class MetadataValidator
    {
        
        public static bool IsValid(UploadRequest request, long maxFileSize, out ErrorCode errorCode, out string errorMessage)
        {
            if (request == null)
            {
                errorCode = ErrorCode.InvalidRequest;
                errorMessage = "Request is null.";
                return false;
            }

       
            if (request.ProtocolVersion != ProtocolConstants.CurrentVersion)
            {
                errorCode = ErrorCode.UnsupportedProtocol;
                errorMessage = $"Unsupported protocol version. Expected {ProtocolConstants.CurrentVersion}.";
                return false;
            }

           
            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                errorCode = ErrorCode.InvalidRequest;
                errorMessage = "RequestId cannot be empty.";
                return false;
            }

            
            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                errorCode = ErrorCode.FileNameEmpty;
                errorMessage = "The file name cannot be empty.";
                return false;
            }

           
            if (request.FileName.Contains("..") ||
                request.FileName.Contains("/") ||
                request.FileName.Contains("\\") ||
                request.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                errorCode = ErrorCode.InvalidFileName;
                errorMessage = "The file name contains invalid characters or path traversal attempts.";
                return false;
            }

         
            if (request.FileSize <= 0)
            {
                errorCode = ErrorCode.FileSizeInvalid;
                errorMessage = "The file size is not valid (must be greater than 0 bytes).";
                return false;
            }

        
            if (request.FileSize > maxFileSize)
            {
                errorCode = ErrorCode.FileTooLarge;
                errorMessage = $"The file size exceeds the maximum allowed limit of {maxFileSize} bytes.";
                return false;
            }

            errorCode = ErrorCode.None;
            errorMessage = string.Empty;
            return true;
        }
    }
}