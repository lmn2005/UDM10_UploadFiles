namespace UDM10.Client
{
    public enum UploadItemStatus { Waiting, Uploading, Completed, Error }

    public class UploadProgress
    {
        public double PercentComplete { get; set; }
        public double SpeedKBps { get; set; }
        public UploadItemStatus Status { get; set; }
        public string? Message { get; set; }
    }
}