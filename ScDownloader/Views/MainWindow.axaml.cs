using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using ScDownloader.Services;
using ScDownloader.ViewModels;

namespace ScDownloader.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContextChanged += (_, _) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.PropertyChanged += OnViewModelPropertyChanged;
                    UpdateConsoleLayout(vm.IsConsoleExpanded);
                    vm.Welcome.ConfirmAsync = ShowConfirmDialogAsync;
                    vm.Welcome.ShowDownloadDialogAsync = ShowDownloadDialogAsync;
                }
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsConsoleExpanded) &&
                DataContext is MainWindowViewModel vm)
            {
                UpdateConsoleLayout(vm.IsConsoleExpanded);
            }
        }

        private void UpdateConsoleLayout(bool expanded)
        {
            if (ConsoleView is null) return;

            if (expanded)
            {
                Grid.SetRow(ConsoleView, 0);
                Grid.SetRowSpan(ConsoleView, 3);
            }
            else
            {
                Grid.SetRow(ConsoleView, 2);
                Grid.SetRowSpan(ConsoleView, 1);
            }
        }

        private async Task<bool> ShowConfirmDialogAsync(string title, string message)
        {
            var dialog = new ConfirmDialog(title, message);
            var result = await dialog.ShowDialog<bool?>(this);
            return result == true;
        }

        private async Task<bool> ShowDownloadDialogAsync(IFFmpegService ffmpegService, string targetFolder, CancellationToken cancellationToken)
        {
            using var dialogCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var dialog = new DownloadDialog();
            dialog.SetTargetFolder(targetFolder);

            // Cancel the download if the user closes the dialog
            dialog.Closed += (_, _) => dialogCts.Cancel();

            var progress = new Progress<DownloadProgress>(update =>
            {
                dialog.UpdateProgress(update);
            });

            // Start the download in the background, then show the dialog
            bool success = false;
            var downloadTask = Task.Run(async () =>
            {
                try
                {
                    success = await ffmpegService.EnsureFFmpegAvailableAsync(targetFolder, progress, dialogCts.Token);
                    dialog.OnDownloadComplete(success);
                }
                catch (OperationCanceledException)
                {
                    // User closed the dialog — treat as failure
                }
            }, dialogCts.Token);

            await dialog.ShowDialog<bool?>(this);

            try { await downloadTask; } catch (OperationCanceledException) { }

            return success;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (FocusManager?.GetFocusedElement() is TextBox && e.Source is not TextBox)
            {
                FocusManager?.ClearFocus();
            }
        }
    }
}
