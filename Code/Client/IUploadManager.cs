using System;

namespace UDM10.Client
{
    public interface IUploadManager
    {
        void EnqueueFile(string filePath, IProgress<UploadProgress> progress);
    }
}