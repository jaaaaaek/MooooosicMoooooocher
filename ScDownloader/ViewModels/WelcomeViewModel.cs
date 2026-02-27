using ScDownloader.Models;
using ScDownloader.Services;

namespace ScDownloader.ViewModels
{
    public class WelcomeViewModel : ViewModelBase
    {
        private readonly IFFmpegService _ffmpegService;
        private string _outputFolder = string.Empty;
        private string _soundCloudToken = string.Empty;
        private bool _mp3Only;
        private string _statusMessage = string.Empty;
        private double _progressPercent;
        private bool _isBusy;
        private bool _hasError;
        private string _errorMessage = string.Empty;
        private bool _isFfmpegRequired;

        public WelcomeViewModel(IFFmpegService ffmpegService)
        {
            _ffmpegService = ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService));
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
        /// Receives the FFmpeg service, target folder, and cancellation token.
        /// Returns true if download succeeded, false otherwise.
        /// </summary>
        public Func<IFFmpegService, string, CancellationToken, Task<bool>>? ShowDownloadDialogAsync { get; set; }

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

            IsFfmpegRequired = !_ffmpegService.IsFFmpegAvailable(musicFolder);
            if (!IsFfmpegRequired)
            {
                IsBusy = false;
                return true;
            }

            // Ask the user before downloading ffmpeg
            if (ConfirmAsync != null)
            {
                bool confirmed = await ConfirmAsync(
                    "FFmpeg Required",
                    "FFmpeg is required for audio conversion and is not installed. Would you like to download it now?");

                if (!confirmed)
                {
                    HasError = true;
                    ErrorMessage = "FFmpeg is required to continue. Restart the app to try again.";
                    IsBusy = false;
                    return false;
                }
            }

            bool success;
            if (ShowDownloadDialogAsync != null)
            {
                success = await ShowDownloadDialogAsync(_ffmpegService, musicFolder, cancellationToken);
            }
            else
            {
                success = await _ffmpegService.EnsureFFmpegAvailableAsync(musicFolder, cancellationToken: cancellationToken);
            }

            if (!success)
            {
                HasError = true;
                ErrorMessage = "FFmpeg download failed. Restart the app to try again.";
            }

            IsBusy = false;
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
