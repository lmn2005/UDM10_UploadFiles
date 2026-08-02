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

            byte[] fakeData = System.Text.Encoding.UTF8.GetBytes("Xin chao, day la file test!");
            using (var testStream = new MemoryStream(fakeData))
            {
                string savedPath = await testStorage.SaveFileAsync("hello.txt", fakeData.Length, testStream);
                Console.WriteLine($"Đã lưu file tại: {savedPath}");
            }
            // ===== HẾT TEST TẠM =====
        }
    }
}