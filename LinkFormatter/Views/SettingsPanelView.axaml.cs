using Avalonia.Controls;
using Avalonia.Interactivity;
using LinkFormatter.ViewModels;
using System.Threading.Tasks;

namespace LinkFormatter.Views
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

            if (TopLevel.GetTopLevel(this) is not Window window)
            {
                return;
            }

            var dialog = new OpenFolderDialog
            {
                Title = "Select Output Folder"
            };

            var selectedFolder = await dialog.ShowAsync(window);
            if (!string.IsNullOrWhiteSpace(selectedFolder))
            {
                viewModel.OutputFolder = selectedFolder;
            }
        }
    }
}
