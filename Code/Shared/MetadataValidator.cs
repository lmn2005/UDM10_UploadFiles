namespace UDM10.Shared
{
    public static class MetadataValidator
    {
        public static bool IsValid(string? fileName, long fileSize, out ErrorCode errorCode, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                errorCode = ErrorCode.FileNameEmpty;
                errorMessage = "The file name cannot be empty.";
                return false;
            }

            if (fileSize <= 0)
            {
                errorCode = ErrorCode.FileSizeInvalid;
                errorMessage = "The file size is not valid (must be greater than 0 bytes).";
                return false;
            }

            errorCode = ErrorCode.None;
            errorMessage = string.Empty;
            return true;
        }
    }
}