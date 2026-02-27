     using System.IO.Compression;
     using System.Security.Cryptography;

namespace ScDownloader.Services
{
    /// <summary>
    /// Service for downloading and managing FFmpeg binary
    /// </summary>
    public class FFmpegService : IFFmpegService
    {
        // Using gyan.dev's essentials build - smaller and optimized for common use cases
        // Alternative: https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip
        private const string FFMPEG_DOWNLOAD_URL = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        private const string FFMPEG_CHECKSUM_URL = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256";
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
            // Check the app folder first
            string ffmpegPath = GetFFmpegPath(targetFolder);
            if (File.Exists(ffmpegPath))
                return true;

            // Check if ffmpeg is on the system PATH
            return IsOnPath(FFMPEG_EXECUTABLE);
        }

        private static bool IsOnPath(string executable)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "-version",
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

                    // Verify checksum
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Verifying,
                        Message = "Verifying FFmpeg checksum..."
                    });

                    if (!await VerifyChecksumAsync(tempZipPath, cancellationToken))
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Phase = DownloadPhase.Failed,
                            Message = "FFmpeg checksum verification failed - file may be corrupted or tampered with"
                        });
                        return false;
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
                            Message = "FFmpeg downloaded and verified!"
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

        private async Task<bool> VerifyChecksumAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                string expectedHash = await FetchExpectedChecksumAsync(cancellationToken);
                if (string.IsNullOrEmpty(expectedHash))
                    return false;

                string actualHash = await ComputeSHA256Async(filePath, cancellationToken);
                return string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> FetchExpectedChecksumAsync(CancellationToken cancellationToken)
        {
            // gyan.dev .sha256 file contains just the hash string
            string checksumContent = await _httpClient.GetStringAsync(FFMPEG_CHECKSUM_URL, cancellationToken);
            return checksumContent.Trim().Split(' ')[0];
        }

        private static async Task<string> ComputeSHA256Async(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
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
