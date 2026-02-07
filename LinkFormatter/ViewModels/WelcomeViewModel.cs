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

        public WelcomeViewModel(IFFmpegService ffmpegService)
        {
            _ffmpegService = ffmpegService ?? throw new ArgumentNullException(nameof(ffmpegService));
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
            private set => SetProperty(ref _isBusy, value);
        }

        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public void ApplySettings(AppSettings settings)
        {
            SoundCloudToken = settings.SoundCloudToken;
            Mp3Only = settings.PreferredFormat == AudioFormat.MP3;
        }

        public async Task<bool> EnsureDependenciesAsync(string musicFolder, CancellationToken cancellationToken = default)
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            ProgressPercent = 0;

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
    }
}
