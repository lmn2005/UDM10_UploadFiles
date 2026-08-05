namespace UDM10.Shared
{
    public sealed class UploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }
}
