using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MooooosicMoooooocher.Services;
using MooooosicMoooooocher.ViewModels;
using MooooosicMoooooocher.Views;

namespace MooooosicMoooooocher
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
                var ytDlpService = new YtDlpService();
                var soundCloudResolver = new SoundCloudResolver();
                var downloadService = new DownloadService(soundCloudResolver);
                var fileService = new FileService();

                var viewModel = new MainWindowViewModel(
                    settingsService,
                    urlValidator,
                    ffmpegService,
                    ytDlpService,
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
