namespace UDM10.Shared
{
    // Cấu trúc gói tin Client gửi lên Server
    public class UploadRequest
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
    }
}