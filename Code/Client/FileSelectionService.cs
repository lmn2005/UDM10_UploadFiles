using System.Windows;
using Microsoft.Win32;

namespace UDM10.Client
{
    public class FileSelectionService
    {
        public string[]? PickFilesFromDialog()
        {
            var dialog = new OpenFileDialog { Multiselect = true };
            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        public string[]? GetDroppedFiles(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.FileDrop))
                return (string[])data.GetData(DataFormats.FileDrop);
            return null;
        }
    }
}