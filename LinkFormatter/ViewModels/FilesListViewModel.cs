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
        private string _filesText = string.Empty;

        public FilesListViewModel(IFileService fileService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            RefreshCommand = new RelayCommand(Refresh);
            OpenFileCommand = new RelayCommand<FileEntry>(OpenFile);
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
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

        public RelayCommand RefreshCommand { get; }
        public RelayCommand<FileEntry> OpenFileCommand { get; }
        public RelayCommand OpenOutputFolderCommand { get; }

        public void SetDownloadedFilesProvider(Func<IReadOnlyCollection<string>> provider)
        {
            _downloadedFilesProvider = provider;
        }

        private void OpenOutputFolder()
        {
            _fileService.OpenFolder(OutputFolder);
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
