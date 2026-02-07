using LinkFormatter.Models;

namespace LinkFormatter.ViewModels
{
    public class SettingsPanelViewModel : ViewModelBase
    {
        private bool _suppressChanges;
        private string _outputFolder = string.Empty;
        private string _soundCloudToken = string.Empty;
        private AudioFormat _preferredFormat = AudioFormat.MP3;

        public event Action? SettingsChanged;
        public IReadOnlyList<AudioFormat> Formats { get; } = Enum.GetValues<AudioFormat>();

        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (SetProperty(ref _outputFolder, value) && !_suppressChanges)
                {
                    SettingsChanged?.Invoke();
                }
            }
        }

        public string SoundCloudToken
        {
            get => _soundCloudToken;
            set
            {
                if (SetProperty(ref _soundCloudToken, value) && !_suppressChanges)
                {
                    SettingsChanged?.Invoke();
                }
            }
        }

        public AudioFormat PreferredFormat
        {
            get => _preferredFormat;
            set
            {
                if (SetProperty(ref _preferredFormat, value) && !_suppressChanges)
                {
                    SettingsChanged?.Invoke();
                }
            }
        }

        public void ApplySettings(AppSettings settings)
        {
            _suppressChanges = true;

            OutputFolder = settings.OutputFolder;
            SoundCloudToken = settings.SoundCloudToken;
            PreferredFormat = settings.PreferredFormat;

            _suppressChanges = false;
        }

        public void UpdateSettings(AppSettings settings)
        {
            settings.OutputFolder = OutputFolder;
            settings.SoundCloudToken = SoundCloudToken;
            settings.PreferredFormat = PreferredFormat;
        }
    }
}
