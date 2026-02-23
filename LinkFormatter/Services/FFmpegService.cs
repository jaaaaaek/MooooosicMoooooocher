     using System.IO.Compression;

namespace LinkFormatter.Services
{
    /// <summary>
    /// Service for downloading and managing FFmpeg binary
    /// </summary>
    public class FFmpegService : IFFmpegService
    {
        // Using gyan.dev's essentials build - smaller and optimized for common use cases
        // Alternative: https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip
        private const string FFMPEG_DOWNLOAD_URL = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        private const string FFMPEG_EXECUTABLE = "ffmpeg.exe";

        private readonly HttpClient _httpClient;

        public FFmpegService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10) // Allow time for large download
            };
        }

        public bool IsFFmpegAvailable(string targetFolder)
        {
            string ffmpegPath = GetFFmpegPath(targetFolder);
            return File.Exists(ffmpegPath);
        }

        public string GetFFmpegPath(string targetFolder)
        {
            return Path.Combine(targetFolder, FFMPEG_EXECUTABLE);
        }

        public async Task<bool> EnsureFFmpegAvailableAsync(
            string targetFolder,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if already exists
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Checking,
                    Message = "Checking for FFmpeg..."
                });

                if (IsFFmpegAvailable(targetFolder))
                {
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Complete,
                        Message = "FFmpeg is already available"
                    });
                    return true;
                }

                // Ensure target folder exists
                Directory.CreateDirectory(targetFolder);

                // Download FFmpeg
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Downloading,
                    Message = "Downloading FFmpeg (this may take a few minutes)..."
                });

                string tempZipPath = Path.Combine(Path.GetTempPath(), "ffmpeg-download.zip");

                try
                {
                    // Download with progress
                    using (var response = await _httpClient.GetAsync(FFMPEG_DOWNLOAD_URL, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();

                        long? totalBytes = response.Content.Headers.ContentLength;

                        using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                        using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalBytesRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                totalBytesRead += bytesRead;

                                progress?.Report(new DownloadProgress
                                {
                                    Phase = DownloadPhase.Downloading,
                                    BytesDownloaded = totalBytesRead,
                                    TotalBytes = totalBytes,
                                    Message = $"Downloading FFmpeg... {FormatBytes(totalBytesRead)}" +
                                             (totalBytes.HasValue ? $" / {FormatBytes(totalBytes.Value)}" : "")
                                });
                            }
                        }
                    }

                    // Extract FFmpeg
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Extracting,
                        Message = "Extracting FFmpeg..."
                    });

                    await ExtractFFmpegFromZip(tempZipPath, targetFolder, cancellationToken);

                    // Verify extraction
                    if (IsFFmpegAvailable(targetFolder))
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Phase = DownloadPhase.Complete,
                            Message = "FFmpeg downloaded and ready!"
                        });
                        return true;
                    }
                    else
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Phase = DownloadPhase.Failed,
                            Message = "FFmpeg extraction failed - file not found after extraction"
                        });
                        return false;
                    }
                }
                finally
                {
                    // Cleanup temp file
                    if (File.Exists(tempZipPath))
                    {
                        try { File.Delete(tempZipPath); } catch { /* Ignore cleanup errors */ }
                    }
                }
            }
            catch (Exception ex)
            {
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Failed,
                    Message = $"Failed to download FFmpeg: {ex.Message}"
                });
                return false;
            }
        }

        private async Task ExtractFFmpegFromZip(string zipPath, string targetFolder, CancellationToken cancellationToken)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                // Find ffmpeg.exe in the archive (it's usually in a bin/ subfolder)
                var ffmpegEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));

                if (ffmpegEntry == null)
                {
                    throw new FileNotFoundException("ffmpeg.exe not found in downloaded archive");
                }

                string destinationPath = GetFFmpegPath(targetFolder);

                // Extract to target location
                ffmpegEntry.ExtractToFile(destinationPath, overwrite: true);
            }

            await Task.CompletedTask; // For async consistency
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
