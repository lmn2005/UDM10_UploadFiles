namespace UDM10.Client.Services
{
    internal sealed class UploadResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }

        private UploadResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public static UploadResult Success(string message)
        {
            return new UploadResult(true, message);
        }

        public static UploadResult Fail(string message)
        {
            return new UploadResult(false, message);
        }
    }
}
