<<<<<<< HEAD
﻿namespace UDM10.Shared
{
    public class UploadResponse
    {
        public UploadStatus Status { get; set; }
        public ErrorCode Error { get; set; }
        public string Message { get; set; }
    }
}
=======
namespace UDM10.Shared
{
    public sealed class UploadResponse
    {
        public UploadStatus Status { get; set; }
        public ErrorCode Error { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
>>>>>>> main
