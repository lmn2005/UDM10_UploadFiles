namespace UDM10.Client
{
    public enum UploadItemStatus { Waiting, Uploading, Completed, Error, Cancelled }

    public class UploadProgress
    {
        public double PercentComplete { get; set; }
        public double SpeedKBps { get; set; }
        public long? BytesTransferred { get; set; }
        public UploadItemStatus Status { get; set; }
        public ConnectionStatus? ConnectionStatus { get; set; }
        public string? Message { get; set; }
    }
}
