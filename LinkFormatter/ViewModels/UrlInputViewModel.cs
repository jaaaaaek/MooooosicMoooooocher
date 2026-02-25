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
        public event Action<AudioFormat>? SelectedFormatChanged;

        public RelayCommand AddUrlCommand { get; }
        public RelayCommand ToggleErrorDetailsCommand { get; }
        public IReadOnlyList<AudioFormat> Formats { get; } = Enum.GetValues<AudioFormat>();
        public ObservableCollection<AddUrlDetail> Details { get; } = new();

        public string UrlText
        {
            get => _urlText;
            set => SetProperty(ref _urlText, value);
        }

        public AudioFormat SelectedFormat
        {
            get => _selectedFormat;
            set
            {
                if (SetProperty(ref _selectedFormat, value))
                {
                    SelectedFormatChanged?.Invoke(value);
                }
            }
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

        public bool HasMultipleErrors => Details.Count > 0;

        public void ClearValidation()
        {
            HasError = false;
            ValidationMessage = string.Empty;
            Details.Clear();
            ShowErrorDetails = false;
            OnPropertyChanged(nameof(HasMultipleErrors));
        }

        public void SetExistingUrlsProvider(Func<IReadOnlyCollection<string>> provider)
        {
            _existingUrlsProvider = provider;
        }

        private void AddUrl()
        {
            var existingUrls = _existingUrlsProvider?.Invoke() ?? Array.Empty<string>();
            List<DownloadItem> added = [];
            List<AddUrlDetail> orderedDetails = [];
            int skippedCount = 0;
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            var lines = UrlText
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            foreach (var line in lines)
            {
                if (!seen.Add(line))
                {
                    skippedCount++;
                    orderedDetails.Add(new AddUrlDetail($"Skipped: Duplicate in list: {line}", isError: true));
                    continue;
                }

                var result = _validator.Validate(line, existingUrls);
                if (!result.IsValid)
                {
                    skippedCount++;
                    orderedDetails.Add(new AddUrlDetail($"Skipped: {line} ({result.Message})", isError: true));
                    continue;
                }

                var item = new DownloadItem
                {
                    Url = line,
                    Format = SelectedFormat,
                    Status = DownloadStatus.Pending
                };

                added.Add(item);
                orderedDetails.Add(new AddUrlDetail($"Added: {item.Url}", isError: false));
            }

            Details.Clear();
            ShowErrorDetails = false;

            if (added.Count == 0)
            {
                HasError = true;
                ValidationMessage = skippedCount > 0
                    ? $"Skipped {skippedCount}. No valid URLs found."
                    : "No valid URLs found.";
                foreach (var detail in orderedDetails)
                {
                    Details.Add(detail);
                }
                OnPropertyChanged(nameof(HasMultipleErrors));
                return;
            }

            foreach (var item in added)
            {
                UrlSubmitted?.Invoke(item);
            }

            UrlText = string.Empty;

            if (skippedCount > 0)
            {
                HasError = true;
                ValidationMessage = $"Added {added.Count}. Skipped {skippedCount}.";
                foreach (var detail in orderedDetails)
                {
                    Details.Add(detail);
                }
            }
            else
            {
                HasError = false;
                ValidationMessage = string.Empty;
            }
            OnPropertyChanged(nameof(HasMultipleErrors));
        }

        public sealed class AddUrlDetail(string message, bool isError)
        {
            public string Message { get; } = message;
            public bool IsError { get; } = isError;
        }
    }
}
