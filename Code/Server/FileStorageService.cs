using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UDM10.Server
{
    public class FileStorageService
    {
        private const int ChunkSize = 64 * 1024;
        private readonly string _uploadsFolder;
        private readonly ServerLogger _logger;
        private readonly DuplicateFileNameResolver _nameResolver;
        private readonly TemporaryFileManager _tempFileManager;

        public FileStorageService(string uploadsFolder, ServerLogger logger)
        {
            _uploadsFolder = uploadsFolder;
            _logger = logger;
            _nameResolver = new DuplicateFileNameResolver(_uploadsFolder);
            _tempFileManager = new TemporaryFileManager(ChunkSize);
            Directory.CreateDirectory(_uploadsFolder);
        }

        public async Task<string> SaveFileAsync(
            string fileName,
            long fileSize,
            Stream source,
            string requestId,
            string clientIp,
            CancellationToken cancellationToken = default)
        {
            string finalPath = _nameResolver.GetAvailablePath(fileName);

            _logger.LogUploadEvent(UploadLifecycleEvent.Start, requestId, clientIp, fileName, 0);

            try
            {
                string savedPath = await _tempFileManager.ReceiveToFileAsync(
                    finalPath, fileSize, source, cancellationToken);

                _logger.LogUploadEvent(UploadLifecycleEvent.Completed, requestId, clientIp, fileName, fileSize);
                return savedPath;
            }
            catch (OperationCanceledException)
            {
                _logger.LogUploadEvent(UploadLifecycleEvent.Cancel, requestId, clientIp, fileName, 0);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogUploadEvent(UploadLifecycleEvent.Error, requestId, clientIp, fileName, 0, ex.Message);
                throw;
            }
        }
    }
}