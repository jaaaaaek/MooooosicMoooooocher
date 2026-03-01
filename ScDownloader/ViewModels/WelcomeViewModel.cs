using ScDownloader.Models;
using ScDownloader.Services;

namespace ScDownloader.ViewModels
{
    public class WelcomeViewModel : ViewModelBase
    {
        private readonly IFFmpegService _ffmpegService;
        private readonly IYtDlpService _ytDlpService;
        private string _outputFolder = string.Empty;
        private string _soundCloudToken = string.Empty;
        private bool _mp3Only;
        private string _statusMessage = string.Empty;
        private double _progressPercent;
        private bool _isBusy;
        private bool _hasError;
        private string _errorMessage = string.Empty;
        private bool _isFfmpegRequired;

        public WelcomeViewModel(IFFmpegService ffmpegService, IYtDlpService ytDlpService)
        {
            _ffmpegService = ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService));
            _ytDlpService = ytDlpService ?? throw new ArgumentNullException(nameof(ytDlpService));
            ContinueCommand = new RelayCommand(OnContinue, () => CanContinue);
        }

        public event Action? ContinueRequested;

        /// <summary>
        /// Callback to show a confirmation dialog. Set by the host (MainWindow).
        /// Returns true if the user confirms, false otherwise.
        /// </summary>
        public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }

        /// <summary>
        /// Callback to show the download progress dialog. Set by the host (MainWindow).
        /// Parameters: windowTitle, fileName, targetFolder, downloadFunc, cancellationToken.
        /// Returns true if download succeeded, false otherwise.
        /// </summary>
        public Func<string, string, string, Func<string, IProgress<DownloadProgress>?, CancellationToken, Task<bool>>, CancellationToken, Task<bool>>? ShowDownloadDialogAsync { get; set; }

        public RelayCommand ContinueCommand { get; }

        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (SetProperty(ref _outputFolder, value))
                {
                    UpdateContinueState();
                }
            }
        }

        public string SoundCloudToken
        {
            get => _soundCloudToken;
            set => SetProperty(ref _soundCloudToken, value);
        }

        public bool Mp3Only
        {
            get => _mp3Only;
            set => SetProperty(ref _mp3Only, value);
        }

        public bool IsFfmpegRequired
        {
            get => _isFfmpegRequired;
            private set => SetProperty(ref _isFfmpegRequired, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            private set => SetProperty(ref _progressPercent, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    UpdateContinueState();
                }
            }
        }

        public bool HasError
        {
            get => _hasError;
            private set
            {
                if (SetProperty(ref _hasError, value))
                {
                    UpdateContinueState();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public bool CanContinue =>
            !IsBusy &&
            !HasError &&
            !string.IsNullOrWhiteSpace(OutputFolder);

        public void ApplySettings(AppSettings settings)
        {
            OutputFolder = settings.IsFirstRun ? string.Empty : settings.OutputFolder;
            SoundCloudToken = settings.SoundCloudToken;
            Mp3Only = settings.PreferredFormat == AudioFormat.MP3;
        }

        public void UpdateSettings(AppSettings settings)
        {
            settings.OutputFolder = OutputFolder;
            settings.SoundCloudToken = SoundCloudToken;
            settings.PreferredFormat = Mp3Only ? AudioFormat.MP3 : AudioFormat.WAV;
        }

        public async Task<bool> EnsureDependenciesAsync(string musicFolder, CancellationToken cancellationToken = default)
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            ProgressPercent = 0;
            StatusMessage = string.Empty;

            // Check FFmpeg
#if DEBUG
            bool ffmpegMissing = true;
#else
            bool ffmpegMissing = !_ffmpegService.IsFFmpegAvailable(musicFolder);
#endif
            if (ffmpegMissing)
            {
                IsFfmpegRequired = true;
                bool success = await DownloadDependencyAsync(
                    "FFmpeg Required",
                    "FFmpeg is required for audio conversion and is not installed. Would you like to download it now?",
                    "Downloading FFmpeg",
                    "ffmpeg",
                    musicFolder,
                    (folder, progress, ct) => _ffmpegService.EnsureFFmpegAvailableAsync(folder, progress, ct),
                    "FFmpeg is required to continue.\nRestart the app to try again.",
                    cancellationToken);

                if (!success)
                {
                    IsBusy = false;
                    return false;
                }
            }

            // Check yt-dlp
#if DEBUG
            bool ytDlpMissing = true;
#else
            bool ytDlpMissing = !_ytDlpService.IsYtDlpAvailable(musicFolder);
#endif
            if (ytDlpMissing)
            {
                bool success = await DownloadDependencyAsync(
                    "yt-dlp Required",
                    "yt-dlp is required for downloading audio and is not installed. Would you like to download it now?",
                    "Downloading yt-dlp",
                    "yt-dlp",
                    musicFolder,
                    (folder, progress, ct) => _ytDlpService.EnsureYtDlpAvailableAsync(folder, progress, ct),
                    "yt-dlp is required to continue. Restart the app to try again.",
                    cancellationToken);

                if (!success)
                {
                    IsBusy = false;
                    return false;
                }
            }

            IsBusy = false;
            return true;
        }

        private async Task<bool> DownloadDependencyAsync(
            string confirmTitle,
            string confirmMessage,
            string dialogTitle,
            string fileName,
            string targetFolder,
            Func<string, IProgress<DownloadProgress>?, CancellationToken, Task<bool>> downloadFunc,
            string failureMessage,
            CancellationToken cancellationToken)
        {
            if (ConfirmAsync != null)
            {
                bool confirmed = await ConfirmAsync(confirmTitle, confirmMessage);
                if (!confirmed)
                {
                    HasError = true;
                    ErrorMessage = failureMessage;
                    return false;
                }
            }

            bool success;
            if (ShowDownloadDialogAsync != null)
            {
                success = await ShowDownloadDialogAsync(dialogTitle, fileName, targetFolder, downloadFunc, cancellationToken);
            }
            else
            {
                success = await downloadFunc(targetFolder, null, cancellationToken);
            }

            if (!success)
            {
                HasError = true;
                ErrorMessage = failureMessage;
            }

            return success;
        }

        private void OnContinue()
        {
            ContinueRequested?.Invoke();
        }

        private void UpdateContinueState()
        {
            OnPropertyChanged(nameof(CanContinue));
            ContinueCommand.RaiseCanExecuteChanged();
        }
    }
}
