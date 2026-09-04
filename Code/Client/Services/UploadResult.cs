namespace UDM10.Client.Services
{
    internal sealed class UploadResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        public string? SavedFileName { get; }

        private UploadResult(bool isSuccess, string message, string? savedFileName = null)
        {
            IsSuccess = isSuccess;
            Message = message;
            SavedFileName = savedFileName;
        }

        public static UploadResult Success(string message, string? savedFileName = null)
        {
            return new UploadResult(true, message, savedFileName);
        }

        public static UploadResult Fail(string message)
        {
            return new UploadResult(false, message);
        }
    }
}