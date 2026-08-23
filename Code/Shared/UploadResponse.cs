namespace UDM10.Shared
{
    public class UploadResponse
    {
        public string ProtocolVersion { get; set; }
            = ProtocolConstants.CurrentVersion;

        public string RequestId { get; set; }
            = string.Empty;

        public UploadStatus Status { get; set; }

        public ErrorCode ErrorCode { get; set; }
            = ErrorCode.None;

        public string ErrorMessage { get; set; }
            = string.Empty;

        public string Message
        {
            get => ErrorMessage;
            set => ErrorMessage = value;
        }

        public ErrorCode Error
        {
            get => ErrorCode;
            set => ErrorCode = value;
        }
    }
}