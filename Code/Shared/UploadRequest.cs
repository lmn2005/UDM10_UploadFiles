using System;

namespace UDM10.Shared
{
    public class UploadRequest
    {
        public string ProtocolVersion { get; set; } = ProtocolConstants.CurrentVersion;
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public string? FileHash { get; set; }   // SHA-256, dạng hex string, tính từ nội dung file
    }
}