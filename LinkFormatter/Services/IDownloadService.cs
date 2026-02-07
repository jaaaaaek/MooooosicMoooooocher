using LinkFormatter.Models;

namespace LinkFormatter.Services
{
    public interface IDownloadService
    {
        Task<DownloadResult> DownloadAsync(
            DownloadItem item,
            string outputFolder,
            string? authToken,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default);

        bool CancelDownload(Guid downloadId);
        bool IsDownloadActive(Guid downloadId);
    }

    public record DownloadResult(bool Success, string? ErrorMessage, string? OutputFileName);
}
