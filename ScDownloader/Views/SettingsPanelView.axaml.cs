using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using ScDownloader.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace ScDownloader.Views
{
    public partial class SettingsPanelView : UserControl
    {
        public SettingsPanelView()
        {
            InitializeComponent();
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsPanelViewModel viewModel)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Output Folder",
                AllowMultiple = false
            });

            var selectedFolder = folders.FirstOrDefault();
            if (selectedFolder is not null)
            {
                viewModel.OutputFolder = selectedFolder.Path.LocalPath;
            }
        }
    }
}
