using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace UDM10.Client
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            FileListView.ItemsSource = _viewModel.FileList;
        }

        private void DropArea_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void DropArea_Drop(object sender, DragEventArgs e)
            => _viewModel.AddFilesFromDrop(e.Data);

        private void BtnChooseFile_Click(object sender, RoutedEventArgs e)
        {
            if (TryApplyServerEndpoint())
            {
                _viewModel.AddFilesFromDialog();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UploadItemViewModel item)
                _viewModel.CancelFile(item);
        }

        private void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UploadItemViewModel item)
                _viewModel.RetryFile(item);
        }

        private bool TryApplyServerEndpoint()
        {
            string serverIp = TxtServerIp.Text.Trim();
            if (!IPAddress.TryParse(serverIp, out _))
            {
                MessageBox.Show(
                    "Server IP không hợp lệ.",
                    "Cấu hình kết nối",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(TxtServerPort.Text.Trim(), out int serverPort) || serverPort < 1 || serverPort > 65535)
            {
                MessageBox.Show(
                    "Port phải là số nguyên từ 1 đến 65535.",
                    "Cấu hình kết nối",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            _viewModel.UpdateServerEndpoint(serverIp, serverPort);
            return true;
        }
    }
}