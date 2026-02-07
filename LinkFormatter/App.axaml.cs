using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LinkFormatter.Services;
using LinkFormatter.ViewModels;
using LinkFormatter.Views;

namespace LinkFormatter
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
                _ = viewModel.InitializeAsync();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
