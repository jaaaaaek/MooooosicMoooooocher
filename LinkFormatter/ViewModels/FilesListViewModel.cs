using System.Collections.ObjectModel;
using LinkFormatter.Services;

namespace LinkFormatter.ViewModels
{
    public class FilesListViewModel : ViewModelBase
    {
        private readonly IFileService _fileService;
        private string _outputFolder = string.Empty;
        private Func<IReadOnlyCollection<string>>? _downloadedFilesProvider;

        public FilesListViewModel(IFileService fileService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            RefreshCommand = new RelayCommand(Refresh);
            OpenFileCommand = new RelayCommand<string>(OpenFile);
        }

        public ObservableCollection<string> Files { get; } = new();

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

        public RelayCommand RefreshCommand { get; }
        public RelayCommand<string> OpenFileCommand { get; }

        public void SetDownloadedFilesProvider(Func<IReadOnlyCollection<string>> provider)
        {
            _downloadedFilesProvider = provider;
        }

        public void Refresh()
        {
            Files.Clear();
            var downloadedFiles = _downloadedFilesProvider?.Invoke() ?? Array.Empty<string>();
            foreach (var file in downloadedFiles)
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    continue;
                }

                string resolved = Path.IsPathRooted(file)
                    ? file
                    : Path.Combine(OutputFolder, file);

                if (File.Exists(resolved))
                {
                    Files.Add(resolved);
                }
            }
        }

        private void OpenFile(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _fileService.OpenFileLocation(filePath);
        }
    }
}
