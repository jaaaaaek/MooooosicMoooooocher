namespace ScDownloader.Services
{
    public class YtDlpService : IYtDlpService
    {
        private const string YTDLP_DOWNLOAD_URL = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const string YTDLP_EXECUTABLE = "yt-dlp.exe";

        private readonly HttpClient _httpClient;

        public YtDlpService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        public bool IsYtDlpAvailable(string targetFolder)
        {
            string ytDlpPath = GetYtDlpPath(targetFolder);
            if (File.Exists(ytDlpPath))
                return true;

            return IsOnPath(YTDLP_EXECUTABLE);
        }

        private static bool IsOnPath(string executable)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(startInfo);
                process?.WaitForExit(3000);
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public string GetYtDlpPath(string targetFolder)
        {
            return Path.Combine(targetFolder, YTDLP_EXECUTABLE);
        }

        public async Task<bool> EnsureYtDlpAvailableAsync(
            string targetFolder,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Checking,
                    Message = "Checking for yt-dlp..."
                });

                if (IsYtDlpAvailable(targetFolder))
                {
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Complete,
                        Message = "yt-dlp is already available"
                    });
                    return true;
                }

                Directory.CreateDirectory(targetFolder);

                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Downloading,
                    Message = "Downloading yt-dlp..."
                });

                string destinationPath = GetYtDlpPath(targetFolder);

                using (var response = await _httpClient.GetAsync(YTDLP_DOWNLOAD_URL,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;

                    using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create,
                        FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalBytesRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0,
                            buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            totalBytesRead += bytesRead;

                            progress?.Report(new DownloadProgress
                            {
                                Phase = DownloadPhase.Downloading,
                                BytesDownloaded = totalBytesRead,
                                TotalBytes = totalBytes,
                                Message = $"Downloading yt-dlp... {FormatBytes(totalBytesRead)}" +
                                    (totalBytes.HasValue ? $" / {FormatBytes(totalBytes.Value)}" : "")
                            });
                        }
                    }
                }

                if (File.Exists(destinationPath))
                {
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Complete,
                        Message = "yt-dlp downloaded and ready!"
                    });
                    return true;
                }
                else
                {
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Failed,
                        Message = "yt-dlp download failed - file not found after download"
                    });
                    return false;
                }
            }
            catch (Exception ex)
            {
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Failed,
                    Message = $"Failed to download yt-dlp: {ex.Message}"
                });
                return false;
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int suffixIndex = 0;
            double size = bytes;
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            return $"{size:F2} {suffixes[suffixIndex]}";
        }
    }
}
