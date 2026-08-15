using System.Windows;
using System.Net;

namespace UDM10.Client
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            FileListView.ItemsSource = _viewModel.FileList;
            TxtServerIp.Text = _viewModel.ServerIp;
            TxtServerPort.Text = _viewModel.ServerPort.ToString();
        }

        private void DropArea_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void DropArea_Drop(object sender, DragEventArgs e)
        {
            if (TryApplyServerEndpoint())
            {
                _viewModel.AddFilesFromDrop(e.Data);
            }
        }

        private void BtnChooseFile_Click(object sender, RoutedEventArgs e)
        {
            if (TryApplyServerEndpoint())
            {
                _viewModel.AddFilesFromDialog();
            }
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

            if (!int.TryParse(TxtServerPort.Text.Trim(), out int serverPort)
                || serverPort is < 1 or > 65535)
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
