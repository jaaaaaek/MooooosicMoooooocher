namespace ScDownloader.Services
{
    public interface IYtDlpService
    {
        Task<bool> EnsureYtDlpAvailableAsync(
            string targetFolder,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default);

        bool IsYtDlpAvailable(string targetFolder);

        string GetYtDlpPath(string targetFolder);
    }
}
