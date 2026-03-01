namespace ScDownloader.Services
{
    public interface IFileService
    {
        IReadOnlyList<string> GetDownloadedFiles(string outputFolder);
        void OpenFileLocation(string filePath);
        void OpenFolder(string folderPath);
    }
}
