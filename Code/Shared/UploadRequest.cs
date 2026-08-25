namespace UDM10.Shared
{
  
    public sealed class UploadRequest
    {
        public string ProtocolVersion { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string FileHash { get; set; } = string.Empty;

        public UploadStatus Status { get; set; } = UploadStatus.Request;
    }
}