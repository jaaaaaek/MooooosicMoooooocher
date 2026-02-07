using System.Collections.ObjectModel;
using LinkFormatter.Services;

namespace LinkFormatter.ViewModels
{
    public class FilesListViewModel : ViewModelBase
    {
        private readonly IFileService _fileService;
        private string _outputFolder = string.Empty;

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

        public void Refresh()
        {
            Files.Clear();
            foreach (var file in _fileService.GetDownloadedFiles(OutputFolder))
            {
                Files.Add(file);
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
