namespace UDM10.Shared
{
    public class UploadResponse
    {
        public string ProtocolVersion { get; set; } = ProtocolConstants.CurrentVersion;
        public string RequestId { get; set; }
        public UploadStatus Status { get; set; }
        public ErrorCode ErrorCode { get; set; } = ErrorCode.None;
        public string ErrorMessage { get; set; }
    }
}