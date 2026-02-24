using System.Collections.ObjectModel;
using LinkFormatter.Services;

namespace LinkFormatter.ViewModels
{
    public class FilesListViewModel : ViewModelBase
    {
        private readonly IFileService _fileService;
        private readonly List<string> _sessionFiles = new();
        private string _outputFolder = string.Empty;
        private Func<IReadOnlyCollection<string>>? _downloadedFilesProvider;
        private Func<IReadOnlyCollection<string>>? _downloadedUrlsProvider;
        private bool _showingUrls;
        private string _filesText = string.Empty;

        public FilesListViewModel(IFileService fileService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            RefreshCommand = new RelayCommand(Refresh);
            OpenFileCommand = new RelayCommand<FileEntry>(OpenFile);
            ToggleUrlsCommand = new RelayCommand(ToggleUrls);
        }

        public ObservableCollection<FileEntry> Files { get; } = new();

        public string FilesText
        {
            get => _filesText;
            private set => SetProperty(ref _filesText, value);
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (SetProperty(ref _outputFolder, value))
                {
                    Refresh();
                }
            }
        }

        public bool ShowingUrls
        {
            get => _showingUrls;
            private set => SetProperty(ref _showingUrls, value);
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand<FileEntry> OpenFileCommand { get; }
        public RelayCommand ToggleUrlsCommand { get; }

        public void SetDownloadedFilesProvider(Func<IReadOnlyCollection<string>> provider)
        {
            _downloadedFilesProvider = provider;
        }

        public void SetDownloadedUrlsProvider(Func<IReadOnlyCollection<string>> provider)
        {
            _downloadedUrlsProvider = provider;
        }

        private void ToggleUrls()
        {
            ShowingUrls = !ShowingUrls;
            if (ShowingUrls)
            {
                var urls = _downloadedUrlsProvider?.Invoke() ?? Array.Empty<string>();
                FilesText = string.Join(Environment.NewLine, urls.Select(FormatUrlDisplay));
            }
            else
            {
                Refresh();
            }
        }

        private static string FormatUrlDisplay(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            string trimmed = url.Trim().TrimEnd('/');
            int lastSlash = trimmed.LastIndexOf('/');
            if (lastSlash < 0 || lastSlash == trimmed.Length - 1)
            {
                return trimmed;
            }

            return trimmed[(lastSlash + 1)..].Replace('-', ' ');
        }

        public void AddFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            string resolved = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(OutputFolder, filePath);

            _sessionFiles.Add(resolved);
            Refresh();
        }

        public void Refresh()
        {
            Files.Clear();
            foreach (var file in _sessionFiles)
            {
                if (File.Exists(file))
                {
                    Files.Add(new FileEntry(file));
                }
            }

            FilesText = string.Join(Environment.NewLine, Files.Select(f => f.DisplayName));
        }

        public void ClearSession()
        {
            _sessionFiles.Clear();
            Refresh();
        }

        private void OpenFile(FileEntry? fileEntry)
        {
            if (fileEntry == null)
            {
                return;
            }

            _fileService.OpenFileLocation(fileEntry.FullPath);
        }

        public sealed class FileEntry
        {
            public FileEntry(string fullPath)
            {
                FullPath = fullPath;
                DisplayName = Path.GetFileName(fullPath);
            }

            public string FullPath { get; }
            public string DisplayName { get; }
        }
    }
}
