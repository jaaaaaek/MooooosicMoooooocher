using System.Collections.ObjectModel;
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
        private bool _showErrorDetails;
        private AudioFormat _selectedFormat = AudioFormat.MP3;

        public UrlInputViewModel(IUrlValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            AddUrlCommand = new RelayCommand(AddUrl);
            ToggleErrorDetailsCommand = new RelayCommand(() => ShowErrorDetails = !ShowErrorDetails);
        }

        public event Action<DownloadItem>? UrlSubmitted;

        public RelayCommand AddUrlCommand { get; }
        public RelayCommand ToggleErrorDetailsCommand { get; }
        public IReadOnlyList<AudioFormat> Formats { get; } = Enum.GetValues<AudioFormat>();
        public ObservableCollection<string> ErrorDetails { get; } = new();

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

        public bool ShowErrorDetails
        {
            get => _showErrorDetails;
            set => SetProperty(ref _showErrorDetails, value);
        }

        public bool HasMultipleErrors => ErrorDetails.Count > 0;

        public void SetExistingUrlsProvider(Func<IReadOnlyCollection<string>> provider)
        {
            _existingUrlsProvider = provider;
        }

        private void AddUrl()
        {
            var existingUrls = _existingUrlsProvider?.Invoke() ?? Array.Empty<string>();
            var added = new List<DownloadItem>();
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var lines = UrlText
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            foreach (var line in lines)
            {
                if (!seen.Add(line))
                {
                    errors.Add($"Duplicate in list: {line}");
                    continue;
                }

                var result = _validator.Validate(line, existingUrls);
                if (!result.IsValid)
                {
                    errors.Add($"{line} ({result.Message})");
                    continue;
                }

                added.Add(new DownloadItem
                {
                    Url = line,
                    Format = SelectedFormat,
                    Status = DownloadStatus.Pending
                });
            }

            ErrorDetails.Clear();
            ShowErrorDetails = false;

            if (added.Count == 0)
            {
                HasError = true;
                ValidationMessage = errors.Count > 0
                    ? $"Skipped {errors.Count}. No valid URLs found."
                    : "No valid URLs found.";
                foreach (var e in errors) ErrorDetails.Add(e);
                OnPropertyChanged(nameof(HasMultipleErrors));
                return;
            }

            foreach (var item in added)
            {
                UrlSubmitted?.Invoke(item);
            }

            UrlText = string.Empty;

            if (errors.Count > 0)
            {
                HasError = true;
                ValidationMessage = $"Added {added.Count}. Skipped {errors.Count}.";
                foreach (var e in errors) ErrorDetails.Add(e);
            }
            else
            {
                HasError = false;
                ValidationMessage = string.Empty;
            }
            OnPropertyChanged(nameof(HasMultipleErrors));
        }
    }
}
