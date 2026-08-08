using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using UDM10.Client.Services;

namespace UDM10.Client
{
    public partial class MainWindow : Window
    {
        private readonly UploadClientService _uploadClientService = new();

        public ObservableCollection<UploadItemViewModel> FileList { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            FileListView.ItemsSource = FileList;
        }

        private void DropArea_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void DropArea_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
                AddFiles(paths);
        }

        private void BtnChooseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Multiselect = true };
            if (dialog.ShowDialog() == true)
                AddFiles(dialog.FileNames);
        }

        private void AddFiles(string[] paths)
        {
            foreach (var path in paths)
                FileList.Add(new UploadItemViewModel(path));
        }

        private async void BtnUploadSelected_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is not UploadItemViewModel selectedFile)
            {
                TxtUploadStatus.Text = "Chọn một file để upload.";
                return;
            }

            BtnUploadSelected.IsEnabled = false;
            selectedFile.StatusMessage = "Đang upload...";
            TxtUploadStatus.Text = $"Đang upload {selectedFile.FileName}...";

            try
            {
                UploadResult result = await _uploadClientService.UploadFileAsync(selectedFile.FilePath);
                selectedFile.StatusMessage = result.Message;
                TxtUploadStatus.Text = result.Message;
            }
            finally
            {
                BtnUploadSelected.IsEnabled = true;
            }
        }
    }
}
