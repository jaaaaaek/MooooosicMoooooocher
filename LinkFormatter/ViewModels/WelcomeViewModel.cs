using LinkFormatter.Models;
using LinkFormatter.Services;

namespace LinkFormatter.ViewModels
{
    public class WelcomeViewModel : ViewModelBase
    {
        private readonly IFFmpegService _ffmpegService;
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

        public RelayCommand ContinueCommand { get; }

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

        public bool CanContinue => !IsBusy && !HasError;

        public void ApplySettings(AppSettings settings)
        {
            SoundCloudToken = settings.SoundCloudToken;
            Mp3Only = settings.PreferredFormat == AudioFormat.MP3;
        }

        public void UpdateSettings(AppSettings settings)
        {
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

            var progress = new Progress<DownloadProgress>(update =>
            {
                StatusMessage = update.Message;
                ProgressPercent = update.PercentComplete;
            });

            bool success = await _ffmpegService.EnsureFFmpegAvailableAsync(musicFolder, progress, cancellationToken);

            if (!success)
            {
                HasError = true;
                ErrorMessage = StatusMessage;
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
