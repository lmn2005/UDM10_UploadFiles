namespace UDM10.Shared
{
    public class UploadRequest
    {
        public string ProtocolVersion { get; set; } = ProtocolConstants.CurrentVersion;
        public string RequestId { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string FileHash { get; set; }
        public UploadStatus Status { get; set; } = UploadStatus.Request;
    }
}
