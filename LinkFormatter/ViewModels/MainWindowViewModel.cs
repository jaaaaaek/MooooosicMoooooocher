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
        private double _zoomLevel = 1.0;
        private const double MinZoom = 0.75;
        private const double MaxZoom = 1.5;
        private const double BaseFontSize = 13;
        private bool _syncingZoomPreset;
        private ZoomPreset? _selectedZoomPreset;
        private readonly IReadOnlyList<ZoomPreset> _zoomPresets = new[]
        {
            new ZoomPreset(0.75, "75%"),
            new ZoomPreset(0.85, "85%"),
            new ZoomPreset(1.00, "100%"),
            new ZoomPreset(1.10, "110%"),
            new ZoomPreset(1.25, "125%"),
            new ZoomPreset(1.50, "150%")
        };

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
            ClearDownloadedFilesCommand = new AsyncRelayCommand(ClearDownloadedFilesAsync);
            ZoomInCommand = new RelayCommand(() => AdjustZoom(1));
            ZoomOutCommand = new RelayCommand(() => AdjustZoom(-1));
            ResetZoomCommand = new RelayCommand(() => ZoomLevel = 1.0);

            ClearAllQueueCommand = new RelayCommand(ClearAllQueue);

            SettingsPanel.SettingsChanged += OnSettingsChanged;
            UrlInput.UrlSubmitted += OnUrlSubmitted;
            Welcome.ContinueRequested += OnWelcomeContinue;

            SelectedZoomPreset = _zoomPresets.First(p => Math.Abs(p.Value - 1.0) < 0.001);
        }

        public SettingsPanelViewModel SettingsPanel { get; }
        public UrlInputViewModel UrlInput { get; }
        public DownloadQueueViewModel DownloadQueue { get; }
        public ProgressConsoleViewModel ProgressConsole { get; }
        public FilesListViewModel FilesList { get; }
        public WelcomeViewModel Welcome { get; }

        public AsyncRelayCommand StartDownloadsCommand { get; }
        public RelayCommand StopAllCommand { get; }
        public AsyncRelayCommand ClearDownloadedFilesCommand { get; }
        public RelayCommand ZoomInCommand { get; }
        public RelayCommand ZoomOutCommand { get; }
        public RelayCommand ResetZoomCommand { get; }
        public RelayCommand ClearAllQueueCommand { get; }

        public IReadOnlyList<ZoomPreset> ZoomPresets => _zoomPresets;

        public ZoomPreset? SelectedZoomPreset
        {
            get => _selectedZoomPreset;
            set
            {
                if (SetProperty(ref _selectedZoomPreset, value) && value != null && !_syncingZoomPreset)
                {
                    ZoomLevel = value.Value;
                }
            }
        }

        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                double clamped = Math.Clamp(value, MinZoom, MaxZoom);
                if (SetProperty(ref _zoomLevel, clamped))
                {
                    OnPropertyChanged(nameof(ZoomedFontSize));
                    OnPropertyChanged(nameof(ZoomPercentDisplay));
                    SyncZoomPreset();
                }
            }
        }

        public double MinimumZoom => MinZoom;
        public double MaximumZoom => MaxZoom;
        public double ZoomedFontSize => BaseFontSize * ZoomLevel;
        public string ZoomPercentDisplay => $"{Math.Round(ZoomLevel * 100)}%";

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
            UrlInput.SetExistingUrlsProvider(() =>
                DownloadQueue.Items.Select(i => i.Url).ToList());
            FilesList.OutputFolder = _settings.OutputFolder;
            FilesList.SetDownloadedFilesProvider(() => _settings.DownloadedFiles);
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
            foreach (var item in DownloadQueue.Items)
            {
                if (item.Status == DownloadStatus.Cancelled)
                {
                    item.Status = DownloadStatus.Pending;
                }
            }

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
                if (update.Percent.HasValue)
                {
                    item.Progress = update.PercentComplete;
                }
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

            if (result.Success && result.WasSkipped)
            {
                item.Status = DownloadStatus.Skipped;
                item.Progress = 100;
                item.ErrorMessage = "Already downloaded.";
            }
            else if (result.Success)
            {
                item.Status = DownloadStatus.Completed;
                item.Progress = 100;
                item.OutputFileName = result.OutputFileName ?? item.OutputFileName;

                if (AddDownloadedFile(result.OutputFileName))
                {
                    await _settingsService.SaveAsync(_settings, cancellationToken);
                }
                FilesList.AddFile(result.OutputFileName);
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

        private void ClearAllQueue()
        {
            StopAllDownloads();
            DownloadQueue.ClearAllCommand.Execute(null);
            UrlInput.ClearValidation();
            FilesList.ClearSession();
            ProgressConsole.Clear();
        }

        private bool AddDownloadedFile(string? outputFileName)
        {
            if (string.IsNullOrWhiteSpace(outputFileName))
            {
                return false;
            }

            string resolved = Path.IsPathRooted(outputFileName)
                ? outputFileName
                : Path.Combine(_settings.OutputFolder, outputFileName);

            if (!_settings.DownloadedFiles.Contains(resolved, StringComparer.OrdinalIgnoreCase))
            {
                _settings.DownloadedFiles.Add(resolved);
                return true;
            }

            return false;
        }

        private async Task ClearDownloadedFilesAsync()
        {
            if (_settings.DownloadedFiles.Count == 0)
            {
                return;
            }

            _settings.DownloadedFiles.Clear();
            await _settingsService.SaveAsync(_settings);
            FilesList.ClearSession();
        }

        private void AdjustZoom(int direction)
        {
            if (_zoomPresets.Count == 0)
            {
                return;
            }

            int currentIndex = GetClosestZoomPresetIndex();
            int nextIndex = Math.Clamp(currentIndex + direction, 0, _zoomPresets.Count - 1);
            ZoomLevel = _zoomPresets[nextIndex].Value;
        }

        private int GetClosestZoomPresetIndex()
        {
            double minDelta = double.MaxValue;
            int closestIndex = 0;

            for (int i = 0; i < _zoomPresets.Count; i++)
            {
                double delta = Math.Abs(_zoomPresets[i].Value - ZoomLevel);
                if (delta < minDelta)
                {
                    minDelta = delta;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private void SyncZoomPreset()
        {
            if (_syncingZoomPreset)
            {
                return;
            }

            _syncingZoomPreset = true;
            SelectedZoomPreset = _zoomPresets[GetClosestZoomPresetIndex()];
            _syncingZoomPreset = false;
        }

        public sealed class ZoomPreset
        {
            public ZoomPreset(double value, string label)
            {
                Value = value;
                Label = label;
            }

            public double Value { get; }
            public string Label { get; }

            public override string ToString() => Label;
        }

        private async void OnWelcomeContinue()
        {
            if (!Welcome.CanContinue)
            {
                return;
            }

            Welcome.UpdateSettings(_settings);
            _settings.IsFirstRun = false;

            _suppressSettingsSave = true;
            SettingsPanel.ApplySettings(_settings);
            _suppressSettingsSave = false;

            UrlInput.SelectedFormat = _settings.PreferredFormat;
            FilesList.OutputFolder = _settings.OutputFolder;

            await _settingsService.SaveAsync(_settings);
            IsWelcomeVisible = false;
        }
    }
}
