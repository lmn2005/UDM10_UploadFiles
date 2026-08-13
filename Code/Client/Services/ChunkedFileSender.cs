using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Client.Services
{
    // Đọc file theo từng chunk và gửi qua stream đích (NetworkStream).
    // Không đọc toàn bộ file vào RAM cùng lúc.
    internal sealed class ChunkedFileSender
    {
        private readonly int _chunkSize;

        public ChunkedFileSender(int chunkSize)
        {
            _chunkSize = chunkSize > 0 ? chunkSize : 64 * 1024;
        }

        public async Task SendAsync(string filePath, Stream destination, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[_chunkSize];

            await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _chunkSize, true);

            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            await destination.FlushAsync(cancellationToken);
        }
    }
}
