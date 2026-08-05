using Microsoft.Win32;
using System.IO;
using System.Windows;
using UDM10.Client.Services;

namespace UDM10.Client
{
    public partial class MainWindow : Window
    {
        private readonly UploadClientService _uploadClientService = new();
        private string? _selectedFilePath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new();

            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;
                SelectedFileTextBlock.Text = _selectedFilePath;
                StatusTextBlock.Text = "Đã chọn file.";
                UploadButton.IsEnabled = true;
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFilePath is null)
            {
                StatusTextBlock.Text = "Bạn chưa chọn file.";
                return;
            }

            ChooseFileButton.IsEnabled = false;
            UploadButton.IsEnabled = false;
            StatusTextBlock.Text = "Đang gửi file...";

            UploadResult result = await _uploadClientService.UploadFileAsync(_selectedFilePath);

            StatusTextBlock.Text = result.Message;
            ChooseFileButton.IsEnabled = true;
            UploadButton.IsEnabled = File.Exists(_selectedFilePath);
        }
    }
}
