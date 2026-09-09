using System.Text.Json.Serialization;

namespace UDM10.Shared
{
    public sealed class UploadRequest
    {
        [JsonRequired]
        public string ProtocolVersion { get; set; } = string.Empty;

        [JsonRequired]
        public string RequestId { get; set; } = string.Empty;

        [JsonRequired]
        public string FileName { get; set; } = string.Empty;

        [JsonRequired]
        public long FileSize { get; set; }

        [JsonRequired]
        public string FileHash { get; set; } = string.Empty;

        [JsonRequired]
        public UploadStatus Status { get; set; } = UploadStatus.None;
    }
}