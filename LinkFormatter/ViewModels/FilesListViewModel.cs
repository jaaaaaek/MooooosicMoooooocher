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
            OpenFileCommand = new RelayCommand<FileEntry>(OpenFile);
        }

        public ObservableCollection<FileEntry> Files { get; } = new();

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
        public RelayCommand<FileEntry> OpenFileCommand { get; }

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
                    Files.Add(new FileEntry(resolved));
                }
            }
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
