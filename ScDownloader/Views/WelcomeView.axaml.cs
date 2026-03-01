using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScDownloader.ViewModels;
using System.Linq;

namespace ScDownloader.Views
{
    public partial class WelcomeView : UserControl
    {
        public WelcomeView()
        {
            InitializeComponent();
        }

        private async void OnBrowseClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not WelcomeViewModel viewModel)
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
                Title = "Select Initial Output Folder",
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
