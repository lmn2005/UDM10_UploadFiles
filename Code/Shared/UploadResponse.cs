using System.Text.Json.Serialization;

namespace UDM10.Shared
{
    
    public sealed class UploadResponse
    {
        public string ProtocolVersion { get; set; } = ProtocolConstants.CurrentVersion;

        public string RequestId { get; set; } = string.Empty;

        public UploadStatus Status { get; set; } = UploadStatus.None;

        public ErrorCode ErrorCode { get; set; } = ErrorCode.None;

        public string ErrorMessage { get; set; } = string.Empty;

   
        [JsonIgnore]
        public ErrorCode Error
        {
            get => ErrorCode;
            set => ErrorCode = value;
        }

        [JsonIgnore]
        public string Message
        {
            get => ErrorMessage;
            set => ErrorMessage = value;
        }
    }
}