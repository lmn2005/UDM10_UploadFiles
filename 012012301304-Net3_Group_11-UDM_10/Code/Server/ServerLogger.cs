using System;
using System.IO;

namespace UDM10.Server
{
    public enum UploadLifecycleEvent { Connect, Start, Cancel, Retry, Completed, Error, Disconnect, Timeout }

    public class ServerLogger
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();

        public ServerLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
            var dir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        public void LogInfo(string message) => Write("INFO", message);

        public void LogError(string message) => Write("ERROR", message);

        public void LogWarning(string message) => Write("WARNING", message);

        public void LogUploadEvent(
            UploadLifecycleEvent lifecycleEvent,
            string requestId,
            string clientIp,
            string fileName,
            long bytesTransferred,
            string? extraMessage = null)
        {
            string reqIdStr = string.IsNullOrWhiteSpace(requestId) ? "N/A" : requestId;
            string fileStr = string.IsNullOrWhiteSpace(fileName) ? "N/A" : fileName;

            string message = $"Event={lifecycleEvent} RequestId={reqIdStr} ClientIp={clientIp} " +
                              $"FileName={fileStr} Bytes={bytesTransferred}" +
                              (string.IsNullOrEmpty(extraMessage) ? "" : $" Message={extraMessage}");

            string level = lifecycleEvent switch
            {
                UploadLifecycleEvent.Error or UploadLifecycleEvent.Timeout => "ERROR",
                UploadLifecycleEvent.Cancel or UploadLifecycleEvent.Disconnect => "WARNING",
                _ => "INFO"
            };

            Write(level, message);
        }

        private void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            Console.WriteLine(line);
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
    }
}