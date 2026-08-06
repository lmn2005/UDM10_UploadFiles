namespace UDM10.Shared
{
    // Cấu trúc phản hồi từ Server về Client
    public class UploadResponse
    {
        public UploadStatus Status { get; set; }
        public ErrorCode Error { get; set; }
        public string Message { get; set; }
    }
}