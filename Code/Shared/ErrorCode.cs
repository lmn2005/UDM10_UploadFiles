namespace UDM10.Shared
{
    public enum ErrorCode
    {
        None = 0,
        UnknownError = 1,
        InvalidMetadata = 2,
        FileNameEmpty = 3,
        FileSizeInvalid = 4,
        ProtocolVersionMismatch = 5,
        MissingRequestId = 6,
        ServerBusy = 7,
        ConnectionLost = 8,
        StorageError = 9,
        CancelledByUser = 10,
        ChecksumMismatch = 11,
        UnsupportedStatus = 12
    }
}