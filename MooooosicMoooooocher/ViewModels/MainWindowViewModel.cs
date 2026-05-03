using MooooosicMoooooocher.Models;
using MooooosicMoooooocher.Services;

namespace MooooosicMoooooocher.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDownloadService _downloadService;
        private readonly IFileService _fileService;
        private readonly IUrlValidator _urlValidator;
        private readonly string _musicFolder;
        private AppSettings _settings = new();
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
        private readonly ZoomPreset[] _zoomPresets =
        [
            new ZoomPreset(0.75, "75%"),
            new ZoomPreset(0.85, "85%"),
            new ZoomPreset(1.00, "100%"),
            new ZoomPreset(1.10, "110%"),
            new ZoomPreset(1.25, "125%"),
            new ZoomPreset(1.50, "150%")
        ];

        public MainWindowViewModel(
            ISettingsService settingsService,
            IUrlValidator urlValidator,
            IFFmpegService ffmpegService,
            IYtDlpService ytDlpService,
            IDownloadService downloadService,
            IFileService fileService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _urlValidator = urlValidator ?? throw new ArgumentNullException(nameof(urlValidator));

            _musicFolder = AppContext.BaseDirectory;

            SettingsPanel = new SettingsPanelViewModel();
            UrlInput = new UrlInputViewModel(_urlValidator);
            DownloadQueue = new DownloadQueueViewModel();
            ProgressConsole = new ProgressConsoleViewModel();
            FilesList = new FilesListViewModel(_fileService);
            Welcome = new WelcomeViewModel(
                ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService)),
                ytDlpService ?? throw new ArgumentNullException(nameof(ytDlpService)));

            StartDownloadsCommand = new AsyncRelayCommand(ProcessQueueAsync, () => !_isProcessingQueue);
            StopAllCommand = new RelayCommand(StopAllDownloads, () => _isProcessingQueue);
            ClearDownloadedFilesCommand = new AsyncRelayCommand(ClearDownloadedFilesAsync);
            ZoomInCommand = new RelayCommand(() => AdjustZoom(1));
            ZoomOutCommand = new RelayCommand(() => AdjustZoom(-1));
            ResetZoomCommand = new RelayCommand(() => ZoomLevel = 1.0);

            ClearAllQueueCommand = new RelayCommand(ClearAllQueue);

            SettingsPanel.SettingsChanged += OnSettingsChanged;
            UrlInput.UrlSubmitted += OnUrlSubmitted;
            UrlInput.SelectedFormatChanged += OnUrlInputFormatChanged;
            Welcome.ContinueRequested += OnWelcomeContinue;
            ProgressConsole.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProgressConsoleViewModel.IsExpanded))
                    OnPropertyChanged(nameof(IsConsoleExpanded));
            };

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

        public static double MinimumZoom => MinZoom;
        public static double MaximumZoom => MaxZoom;
        public double ZoomedFontSize => BaseFontSize * ZoomLevel;
        public string ZoomPercentDisplay => $"{Math.Round(ZoomLevel * 100)}%";

        public bool IsConsoleExpanded => ProgressConsole.IsExpanded;

        public bool IsWelcomeVisible
        {
            get => _isWelcomeVisible;
            set => SetProperty(ref _isWelcomeVisible, value);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _settings = await _settingsService.LoadAsync(cancellationToken);

            if (FeatureFlags.AlwaysShowWelcomeOnLaunch)
            {
                _settings.IsFirstRun = true;
            }

            _suppressSettingsSave = true;
            SettingsPanel.ApplySettings(_settings);
            _suppressSettingsSave = false;

            UrlInput.SelectedFormat = _settings.PreferredFormat;
            UrlInput.SetExistingUrlsProvider(() =>
                [.. DownloadQueue.Items.Select(i => i.Url)]);
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
                // Re-check dependencies even after first run in case they were
                // removed (e.g. by a clean rebuild). Show welcome again if missing.
                bool depsOk = await Welcome.EnsureDependenciesAsync(_musicFolder, cancellationToken);
                IsWelcomeVisible = !depsOk;
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

        private async void OnUrlSubmitted(DownloadItem item)
        {
            if (!_urlValidator.IsResolvableUrl(item.Url))
            {
                DownloadQueue.Add(item);
                return;
            }

            try
            {
                ProgressConsole.AppendLine($"Resolving tracks from {item.Url}...");

                var progress = new Progress<DownloadProgress>(update =>
                {
                    if (string.IsNullOrWhiteSpace(update.Message))
                    {
                        return;
                    }

                    if (!string.IsNullOrEmpty(update.LiveKey))
                    {
                        ProgressConsole.UpdateOrAppendLine(update.Message, update.LiveKey);
                    }
                    else
                    {
                        ProgressConsole.AppendLine(update.Message);
                    }
                });

                var resolvedUrls = await _downloadService.ResolvePlaylistAsync(
                    item.Url,
                    _settings.AuthToken,
                    progress);

                if (resolvedUrls.Count == 0)
                {
                    ProgressConsole.AppendLine("No tracks found. If this is a private playlist or likes page, make sure your auth token is set.");
                    return;
                }

                var existingUrls = DownloadQueue.Items.Select(i => i.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Batch additions to keep the UI thread responsive. Avalonia's
                // non-virtualizing UniformGrid layout pass is O(N) per Add, so a
                // tight loop of 3700 Adds blocks the window for many seconds and
                // Windows marks it "(Not Responding)". Inserting in chunks of 50
                // with a Task.Yield() between chunks lets the UI breathe and
                // process its message pump (input, paint, focus changes, etc.)
                // between bursts.
                const int BatchSize = 50;
                int added = 0;
                int processed = 0;
                foreach (var url in resolvedUrls)
                {
                    processed++;
                    if (existingUrls.Contains(url))
                    {
                        continue;
                    }

                    existingUrls.Add(url);
                    DownloadQueue.Add(new DownloadItem
                    {
                        Url = url,
                        Format = item.Format,
                        Status = DownloadStatus.Pending
                    });
                    added++;

                    if (processed % BatchSize == 0)
                    {
                        await Task.Yield();
                    }
                }

                ProgressConsole.AppendLine($"Added {added} track(s) to queue ({resolvedUrls.Count - added} duplicates skipped).");
            }
            catch (Exception ex)
            {
                ProgressConsole.AppendLine($"Failed to resolve {item.Url}: {ex.Message}");
            }
        }

        private void OnUrlInputFormatChanged(AudioFormat format)
        {
            foreach (var item in DownloadQueue.Items)
            {
                if (item.Status is DownloadStatus.Pending or DownloadStatus.Cancelled or DownloadStatus.Failed)
                {
                    item.Format = format;
                }
            }
        }

        private async Task ProcessQueueAsync()
        {
            foreach (var item in DownloadQueue.Items)
            {
                if (item.Status is DownloadStatus.Cancelled or DownloadStatus.Failed)
                {
                    item.Status = DownloadStatus.Pending;
                }
            }

            _isProcessingQueue = true;
            StartDownloadsCommand.RaiseCanExecuteChanged();
            StopAllCommand.RaiseCanExecuteChanged();

            _queueCts = new CancellationTokenSource();

            if (string.IsNullOrWhiteSpace(_settings.AuthToken) &&
                DownloadQueue.Items.Any(i => i.Status == DownloadStatus.Pending && i.Format == AudioFormat.WAV))
            {
                ProgressConsole.AppendLine("Auth token is required for WAV downloads. Set it in Settings.");

                // Mark all pending WAV items Failed up front so the queue processor
                // skips them. Without this short-circuit, each WAV item would still
                // attempt to download, fail with the same auth-token error, and spam
                // the console with one FAILED line per item.
                foreach (var wavItem in DownloadQueue.Items
                             .Where(i => i.Status == DownloadStatus.Pending && i.Format == AudioFormat.WAV))
                {
                    wavItem.Status = DownloadStatus.Failed;
                    wavItem.ErrorMessage = "Auth token is required for WAV downloads.";
                }
            }

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
                _settings.AuthToken,
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

                if (!string.IsNullOrWhiteSpace(result.OutputFileName))
                {
                    FilesList.AddFile(result.OutputFileName);
                }
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
                ProgressConsole.AppendLine($"FAILED: {item.Model.Url} - {item.ErrorMessage}");
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
            if (_zoomPresets.Length == 0)
            {
                return;
            }

            int currentIndex = GetClosestZoomPresetIndex();
            int nextIndex = Math.Clamp(currentIndex + direction, 0, _zoomPresets.Length - 1);
            ZoomLevel = _zoomPresets[nextIndex].Value;
        }

        private int GetClosestZoomPresetIndex()
        {
            double minDelta = double.MaxValue;
            int closestIndex = 0;

            for (int i = 0; i < _zoomPresets.Length; i++)
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

        public sealed class ZoomPreset(double value, string label)
        {
            public double Value { get; } = value;
            public string Label { get; } = label;

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
