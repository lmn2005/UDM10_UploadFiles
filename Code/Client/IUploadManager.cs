using System;
using System.Threading;

namespace UDM10.Client
{
    public interface IUploadManager : IAsyncDisposable
    {
        void EnqueueFile(string filePath, IProgress<UploadProgress> progress, CancellationToken cancellationToken);
    }
}
