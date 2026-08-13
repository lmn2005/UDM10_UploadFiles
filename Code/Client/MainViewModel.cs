using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using UDM10.Client.Services;
namespace UDM10.Client
{
    public class MainViewModel
    {
        public ObservableCollection<UploadItemViewModel> FileList { get; } = new();
        private readonly FileSelectionService _fileSelectionService = new();


        private readonly IUploadManager _uploadManager = new UploadClientService();

        public void AddFilesFromDialog()
        {
            var paths = _fileSelectionService.PickFilesFromDialog();
            if (paths != null) AddFiles(paths);
        }

        public void AddFilesFromDrop(IDataObject data)
        {
            var paths = _fileSelectionService.GetDroppedFiles(data);
            if (paths != null) AddFiles(paths);
        }

        private void AddFiles(string[] paths)
        {
            foreach (var path in paths)
            {
                
                if (FileList.Any(f => f.FilePath == path)) continue;

                var item = new UploadItemViewModel(path);
                FileList.Add(item);

              
                var progress = new Progress<UploadProgress>(p =>
                {
                    item.PercentComplete = p.PercentComplete;
                    item.SpeedKBps = p.SpeedKBps;
                    item.Status = p.Status;
                    item.Message = p.Message ?? "";
                });

                _uploadManager.EnqueueFile(item.FilePath, progress);
            }
        }
    }
}