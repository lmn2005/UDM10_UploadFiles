using System.Text.Json.Serialization;

namespace UDM10.Shared
{
    public sealed class UploadResponse
    {
        [JsonRequired]
        public string ProtocolVersion { get; set; } =
            ProtocolConstants.CurrentVersion;

        [JsonRequired]
        public string RequestId { get; set; } =
            string.Empty;

        [JsonRequired]
        public UploadStatus Status { get; set; } =
            UploadStatus.None;

        [JsonRequired]
        public ErrorCode ErrorCode { get; set; } =
            ErrorCode.None;

        [JsonRequired]
        public string ErrorMessage { get; set; } =
            string.Empty;

    
        public string SavedFileName { get; set; } =
            string.Empty;
    }
}