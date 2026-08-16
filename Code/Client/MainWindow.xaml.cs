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
            => _viewModel.AddFilesFromDialog();

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
    }
}