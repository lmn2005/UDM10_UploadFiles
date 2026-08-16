using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Client.Services
{
    // Tách hợp đồng upload để UploadManager có thể được kiểm thử mà không cần TCP Server thật.
    internal interface IUploadClient
    {
        Task<UploadResult> UploadFileAsync(
            string filePath,
            IProgress<UploadProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
