using Avalonia.Controls;
using LinkFormatter.ViewModels;

namespace LinkFormatter.Views
{
    public partial class FilesListView : UserControl
    {
        public FilesListView()
        {
            InitializeComponent();
        }

        private async void OnClearClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            var dialog = new ConfirmDialog(
                "Clear downloaded files?",
                "This will clear the list of already downloaded files. Your files on disk will remain.");

            bool confirmed = await dialog.ShowDialog<bool>(owner);
            if (!confirmed)
            {
                return;
            }

            if (owner.DataContext is MainWindowViewModel vm)
            {
                vm.ClearDownloadedFilesCommand.Execute(null);
            }
        }
    }
}
