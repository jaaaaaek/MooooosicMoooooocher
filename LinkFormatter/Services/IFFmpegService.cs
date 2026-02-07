namespace LinkFormatter.Services
{
    /// <summary>
    /// Service for managing FFmpeg binary availability
    /// </summary>
    public interface IFFmpegService
    {
        /// <summary>
        /// Ensures FFmpeg is available in the specified folder. Downloads if missing.
        /// </summary>
        /// <param name="targetFolder">Folder where ffmpeg.exe should be located</param>
        /// <param name="progress">Optional progress reporter for download status</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if ffmpeg is available, false if download failed</returns>
        Task<bool> EnsureFFmpegAvailableAsync(
            string targetFolder,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if FFmpeg exists at the specified path
        /// </summary>
        /// <param name="targetFolder">Folder to check</param>
        /// <returns>True if ffmpeg.exe exists</returns>
        bool IsFFmpegAvailable(string targetFolder);

        /// <summary>
        /// Gets the expected path to ffmpeg.exe
        /// </summary>
        /// <param name="targetFolder">Base folder</param>
        /// <returns>Full path to ffmpeg.exe</returns>
        string GetFFmpegPath(string targetFolder);
    }

    /// <summary>
    /// Progress information for FFmpeg download
    /// </summary>
    public class DownloadProgress
    {
        public long BytesDownloaded { get; set; }
        public long? TotalBytes { get; set; }
        public double? Percent { get; set; }
        public double PercentComplete => Percent ?? (TotalBytes.HasValue && TotalBytes.Value > 0
            ? (double)BytesDownloaded / TotalBytes.Value * 100
            : 0);
        public string Message { get; set; } = string.Empty;
        public DownloadPhase Phase { get; set; }
    }

    /// <summary>
    /// Phases of the download process
    /// </summary>
    public enum DownloadPhase
    {
        Checking,
        Downloading,
        Extracting,
        Complete,
        Failed
    }
}
