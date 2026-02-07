using LinkFormatter.Models;
using LinkFormatter.Services;

namespace LinkFormatter.ViewModels
{
    public class UrlInputViewModel : ViewModelBase
    {
        private readonly IUrlValidator _validator;
        private Func<IReadOnlyCollection<string>>? _existingUrlsProvider;
        private string _urlText = string.Empty;
        private string _validationMessage = string.Empty;
        private bool _hasError;
        private AudioFormat _selectedFormat = AudioFormat.MP3;

        public UrlInputViewModel(IUrlValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            AddUrlCommand = new RelayCommand(AddUrl);
        }

        public event Action<DownloadItem>? UrlSubmitted;

        public RelayCommand AddUrlCommand { get; }

        public string UrlText
        {
            get => _urlText;
            set => SetProperty(ref _urlText, value);
        }

        public AudioFormat SelectedFormat
        {
            get => _selectedFormat;
            set => SetProperty(ref _selectedFormat, value);
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set => SetProperty(ref _validationMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        public void SetExistingUrlsProvider(Func<IReadOnlyCollection<string>> provider)
        {
            _existingUrlsProvider = provider;
        }

        private void AddUrl()
        {
            var existingUrls = _existingUrlsProvider?.Invoke();
            var result = _validator.Validate(UrlText, existingUrls);

            if (!result.IsValid)
            {
                HasError = true;
                ValidationMessage = result.Message;
                return;
            }

            var item = new DownloadItem
            {
                Url = UrlText.Trim(),
                Format = SelectedFormat,
                Status = DownloadStatus.Pending
            };

            HasError = false;
            ValidationMessage = string.Empty;
            UrlText = string.Empty;

            UrlSubmitted?.Invoke(item);
        }
    }
}
