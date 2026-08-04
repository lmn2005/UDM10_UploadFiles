using System;
using System.IO;
using System.Threading.Tasks;

namespace UDM10.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // ===== TEST TẠM - xóa sau khi test xong =====
            var testLogger = new ServerLogger("Extra/TestLogs/server.log");
            var testStorage = new FileStorageService("Extra/TestData/Uploads", testLogger);

            string sampleFolder = "Extra/TestData/SampleFiles";
            string[] filePaths = Directory.GetFiles(sampleFolder);

            foreach (string filePath in filePaths)
            {
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;

                using (var fileStream = File.OpenRead(filePath))
                {
                    try
                    {
                        string savedPath = await testStorage.SaveFileAsync(fileName, fileSize, fileStream);
                        Console.WriteLine($"OK: {fileName} -> {savedPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"LỖI với {fileName}: {ex.Message}");
                    }
                }
            }
            // ===== HẾT TEST TẠM =====
        }
    }
}