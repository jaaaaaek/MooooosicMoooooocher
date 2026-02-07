using LinkFormatter.Models;
using LinkFormatter.Services;

namespace LinkFormatter.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDownloadService _downloadService;
        private readonly IFileService _fileService;
        private readonly string _musicFolder;
        private AppSettings _settings = new AppSettings();
        private bool _isWelcomeVisible;
        private bool _isProcessingQueue;
        private CancellationTokenSource? _queueCts;
        private bool _suppressSettingsSave;

        public MainWindowViewModel(
            ISettingsService settingsService,
            IUrlValidator urlValidator,
            IFFmpegService ffmpegService,
            IDownloadService downloadService,
            IFileService fileService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));

            _musicFolder = Path.Combine(AppContext.BaseDirectory, "Music");

            SettingsPanel = new SettingsPanelViewModel();
            UrlInput = new UrlInputViewModel(urlValidator ?? throw new ArgumentNullException(nameof(urlValidator)));
            DownloadQueue = new DownloadQueueViewModel();
            ProgressConsole = new ProgressConsoleViewModel();
            FilesList = new FilesListViewModel(_fileService);
            Welcome = new WelcomeViewModel(ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService)));

            StartDownloadsCommand = new AsyncRelayCommand(ProcessQueueAsync, () => !_isProcessingQueue);
            StopAllCommand = new RelayCommand(StopAllDownloads, () => _isProcessingQueue);

            SettingsPanel.SettingsChanged += OnSettingsChanged;
            UrlInput.UrlSubmitted += OnUrlSubmitted;
        }

        public SettingsPanelViewModel SettingsPanel { get; }
        public UrlInputViewModel UrlInput { get; }
        public DownloadQueueViewModel DownloadQueue { get; }
        public ProgressConsoleViewModel ProgressConsole { get; }
        public FilesListViewModel FilesList { get; }
        public WelcomeViewModel Welcome { get; }

        public AsyncRelayCommand StartDownloadsCommand { get; }
        public RelayCommand StopAllCommand { get; }

        public bool IsWelcomeVisible
        {
            get => _isWelcomeVisible;
            set => SetProperty(ref _isWelcomeVisible, value);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _settings = await _settingsService.LoadAsync(cancellationToken);

            _suppressSettingsSave = true;
            SettingsPanel.ApplySettings(_settings);
            _suppressSettingsSave = false;

            UrlInput.SelectedFormat = _settings.PreferredFormat;
            UrlInput.SetExistingUrlsProvider(() => _settings.DownloadedUrls);
            FilesList.OutputFolder = _settings.OutputFolder;
            Welcome.ApplySettings(_settings);

            if (_settings.IsFirstRun)
            {
                IsWelcomeVisible = true;
                await Welcome.EnsureDependenciesAsync(_musicFolder, cancellationToken);
            }
            else
            {
                IsWelcomeVisible = false;
            }
        }

        private async void OnSettingsChanged()
        {
            if (_suppressSettingsSave)
            {
                return;
            }

            SettingsPanel.UpdateSettings(_settings);
            UrlInput.SelectedFormat = _settings.PreferredFormat;
            FilesList.OutputFolder = _settings.OutputFolder;

            await _settingsService.SaveAsync(_settings);
        }

        private void OnUrlSubmitted(DownloadItem item)
        {
            DownloadQueue.Add(item);
        }

        private async Task ProcessQueueAsync()
        {
            _isProcessingQueue = true;
            StartDownloadsCommand.RaiseCanExecuteChanged();
            StopAllCommand.RaiseCanExecuteChanged();

            _queueCts = new CancellationTokenSource();

            try
            {
                DownloadItemViewModel? next;
                while ((next = DownloadQueue.GetNextPending()) != null)
                {
                    if (_queueCts.IsCancellationRequested)
                    {
                        next.Status = DownloadStatus.Cancelled;
                        break;
                    }

                    await DownloadAsync(next, _queueCts.Token);
                }
            }
            finally
            {
                _queueCts?.Dispose();
                _queueCts = null;
                _isProcessingQueue = false;
                StartDownloadsCommand.RaiseCanExecuteChanged();
                StopAllCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task DownloadAsync(DownloadItemViewModel item, CancellationToken cancellationToken)
        {
            item.Status = DownloadStatus.Downloading;
            item.Progress = 0;
            item.ErrorMessage = string.Empty;

            var progress = new Progress<DownloadProgress>(update =>
            {
                item.Progress = update.PercentComplete;
                if (!string.IsNullOrWhiteSpace(update.Message))
                {
                    ProgressConsole.AppendLine(update.Message);
                }
            });

            var result = await _downloadService.DownloadAsync(
                item.Model,
                _settings.OutputFolder,
                _settings.SoundCloudToken,
                progress,
                cancellationToken);

            if (result.Success)
            {
                item.Status = DownloadStatus.Completed;
                item.Progress = 100;
                item.OutputFileName = result.OutputFileName ?? item.OutputFileName;

                if (!_settings.DownloadedUrls.Contains(item.Url, StringComparer.OrdinalIgnoreCase))
                {
                    _settings.DownloadedUrls.Add(item.Url);
                    await _settingsService.SaveAsync(_settings, cancellationToken);
                }

                FilesList.Refresh();
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                item.Status = DownloadStatus.Cancelled;
                item.ErrorMessage = result.ErrorMessage ?? "Download cancelled.";
            }
            else
            {
                item.Status = DownloadStatus.Failed;
                item.ErrorMessage = result.ErrorMessage ?? "Download failed.";
            }
        }

        private void StopAllDownloads()
        {
            if (!_isProcessingQueue)
            {
                return;
            }

            _queueCts?.Cancel();

            foreach (var item in DownloadQueue.Items)
            {
                if (item.Status == DownloadStatus.Pending)
                {
                    item.Status = DownloadStatus.Cancelled;
                }

                if (item.Status == DownloadStatus.Downloading && _downloadService.IsDownloadActive(item.Id))
                {
                    _downloadService.CancelDownload(item.Id);
                    item.Status = DownloadStatus.Cancelled;
                }
            }
        }
    }
}
