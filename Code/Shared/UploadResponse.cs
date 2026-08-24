namespace UDM10.Shared
{
    public class UploadResponse
    {
        public string ProtocolVersion { get; set; } = ProtocolConstants.CurrentVersion;
        public string RequestId { get; set; }
        public UploadStatus Status { get; set; }

        // Keep original property names for compatibility
        public ErrorCode ErrorCode { get; set; } = ErrorCode.None;
        public string ErrorMessage { get; set; }

        // Backwards-compatible aliases used in server/client code
        public ErrorCode Error { get => ErrorCode; set => ErrorCode = value; }
        public string Message { get => ErrorMessage; set => ErrorMessage = value; }
    }
}
