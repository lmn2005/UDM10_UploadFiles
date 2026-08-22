namespace UDM10.Shared
{
    public enum UploadStatus
    {
        None = 0,
        Request = 1,
        Ready = 2,
        Completed = 3,
        Error = 4,
        Cancel = 5,
        Retry = 6
    }
}