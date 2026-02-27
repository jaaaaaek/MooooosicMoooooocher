using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ScDownloader.Services;
using ScDownloader.ViewModels;
using ScDownloader.Views;

namespace ScDownloader
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var settingsService = new SettingsService();
                var urlValidator = new UrlValidator();
                var ffmpegService = new FFmpegService();
                var downloadService = new DownloadService();
                var fileService = new FileService();

                var viewModel = new MainWindowViewModel(
                    settingsService,
                    urlValidator,
                    ffmpegService,
                    downloadService,
                    fileService);

                var mainWindow = new MainWindow
                {
                    DataContext = viewModel
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Opened += async (_, _) => await viewModel.InitializeAsync();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
