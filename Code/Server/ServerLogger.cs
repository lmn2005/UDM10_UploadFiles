using System;
using System.IO;

namespace UDM10.Server
{
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

        private void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

            // Console để xem log ngay khi chạy Server
            Console.WriteLine(line);

            // Ghi ra file, khóa lại để tránh nhiều luồng ghi cùng lúc
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
    }
}