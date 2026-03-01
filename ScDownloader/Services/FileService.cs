using System.Diagnostics;

namespace ScDownloader.Services
{
    public class FileService : IFileService
    {
        public IReadOnlyList<string> GetDownloadedFiles(string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder) || !Directory.Exists(outputFolder))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(outputFolder)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void OpenFileLocation(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            if (PlatformHelper.IsMacOS)
            {
                Process.Start("open", $"-R \"{filePath}\"");
            }
            else
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
            }
        }

        public void OpenFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            if (PlatformHelper.IsMacOS)
            {
                Process.Start("open", $"\"{folderPath}\"");
            }
            else
            {
                Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true });
            }
        }
    }
}
