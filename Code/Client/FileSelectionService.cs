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

        public string[] GetDroppedPaths(IDataObject data)
        {
            if (data is null ||
                !data.GetDataPresent(DataFormats.FileDrop))
            {
                return [];
            }

            return data.GetData(DataFormats.FileDrop) as string[] ?? [];
        }
    }
}
