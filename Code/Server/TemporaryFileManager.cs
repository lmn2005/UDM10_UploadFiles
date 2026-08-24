using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Server
{
    public class TemporaryFileManager
    {
        private readonly int _chunkSize;
        private readonly string _uploadsFolder;

        // Constructor nhận cả 2 tham số
        public TemporaryFileManager(int chunkSize, string uploadsFolder)
        {
            _chunkSize = chunkSize > 0 ? chunkSize : 8192;
            _uploadsFolder = string.IsNullOrEmpty(uploadsFolder) ? "Uploads" : uploadsFolder;
            
            if (!Directory.Exists(_uploadsFolder))
            {
                Directory.CreateDirectory(_uploadsFolder);
            }
        }

        // Constructor dự phòng nếu code cũ chỉ truyền chunkSize
        public TemporaryFileManager(int chunkSize) : this(chunkSize, "Uploads")
        {
        }

        // Constructor dự phòng nếu code cũ chỉ truyền uploadsFolder
        public TemporaryFileManager(string uploadsFolder) : this(8192, uploadsFolder)
        {
        }

        public async Task<string> ReceiveToFileAsync(
            string finalPath, 
            long fileSize, 
            Stream source, 
            CancellationToken cancellationToken = default)
        {
            string tempPath = finalPath + ".part";

            try
            {
                // Dùng _chunkSize làm buffer size cho FileStream
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, _chunkSize, true))
                {
                    byte[] buffer = new byte[_chunkSize];
                    long totalBytesRead = 0;
                    int bytesRead;

                    while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalBytesRead += bytesRead;
                    }
                }

                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }

                File.Move(tempPath, finalPath);
                return finalPath;
            }
            catch (Exception)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }
        }
    }
}