using System;

namespace UDM10.Shared
{
    public class UploadRequest
    {
        public string ProtocolVersion { get; set; }
            = ProtocolConstants.CurrentVersion;

        public string RequestId { get; set; }
            = Guid.NewGuid().ToString("N");

        public string FileName { get; set; }
            = string.Empty;

        public string FileHash { get; set; }
            = string.Empty;

        public long FileSize { get; set; }

        public UploadStatus Status { get; set; }
            = UploadStatus.Request;
    }
}