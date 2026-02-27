using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ScDownloader.Models;

namespace ScDownloader.Services
{
    public class DownloadService : IDownloadService
    {
        private static readonly Regex ProgressRegex = new(
            "\\[download\\]\\s+(?<percent>\\d+(\\.\\d+)?)%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DestinationRegex = new(
            "Destination:\\s(?<name>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ConcurrentDictionary<Guid, Process> _activeProcesses = new();

        public bool CancelDownload(Guid downloadId)
        {
            if (_activeProcesses.TryRemove(downloadId, out var process))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore cancellation errors to keep UI responsive.
                }
                finally
                {
                    process.Dispose();
                }

                return true;
            }

            return false;
        }

        public bool IsDownloadActive(Guid downloadId) => _activeProcesses.ContainsKey(downloadId);

        public async Task<DownloadResult> DownloadAsync(
            DownloadItem item,
            string outputFolder,
            string? authToken,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                return new DownloadResult(false, "Output folder is not set.", null);
            }

            if (item.Format == AudioFormat.WAV && string.IsNullOrWhiteSpace(authToken))
            {
                return new DownloadResult(false, "SoundCloud auth token is required for WAV downloads.", null);
            }

            string ytDlpPath = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
            if (!File.Exists(ytDlpPath))
            {
                return new DownloadResult(false, "yt-dlp.exe was not found in the application folder.", null);
            }

            Directory.CreateDirectory(outputFolder);

            var arguments = BuildArguments(item, outputFolder, authToken);
            var startInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(ytDlpPath) ?? AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!_activeProcesses.TryAdd(item.Id, process))
            {
                return new DownloadResult(false, "Download is already running for this item.", null);
            }

            string? outputFileName = null;
            bool wasSkipped = false;
            var errorBuilder = new StringBuilder();

            void HandleLine(string? line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                var progressMatch = ProgressRegex.Match(line);
                bool isProgressLine = progressMatch.Success &&
                    line.StartsWith("[download]", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("Destination:", StringComparison.OrdinalIgnoreCase);

                var update = new DownloadProgress
                {
                    Phase = DownloadPhase.Downloading,
                    Message = isProgressLine ? string.Empty : line
                };

                if (progressMatch.Success &&
                    double.TryParse(progressMatch.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                {
                    update.Percent = percent;
                }

                var destinationMatch = DestinationRegex.Match(line);
                if (destinationMatch.Success)
                {
                    outputFileName = destinationMatch.Groups["name"].Value.Trim();
                }

                if (line.Contains("has already been recorded in the archive", StringComparison.OrdinalIgnoreCase))
                {
                    wasSkipped = true;
                }

                progress?.Report(update);
            }

            process.OutputDataReceived += (_, e) => HandleLine(e.Data);
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                }

                HandleLine(e.Data);
            };

            try
            {
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Downloading,
                    Message = $"CMD: \"{ytDlpPath}\" {arguments}"
                });

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var registration = cancellationToken.Register(() => CancelDownload(item.Id));

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0)
                {
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Complete,
                        Percent = 100,
                        Message = "Download complete."
                    });

                    return new DownloadResult(true, null, outputFileName, wasSkipped);
                }

                string errorMessage = errorBuilder.Length > 0
                    ? errorBuilder.ToString().Trim()
                    : "yt-dlp exited with a non-zero exit code.";

                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Failed,
                    Message = errorMessage
                });

                return new DownloadResult(false, errorMessage, outputFileName);
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Failed,
                    Message = "Download cancelled."
                });

                return new DownloadResult(false, "Download cancelled.", outputFileName);
            }
            finally
            {
                _activeProcesses.TryRemove(item.Id, out _);
            }
        }

        private static string BuildArguments(DownloadItem item, string outputFolder, string? authToken)
        {
            string format = item.Format == AudioFormat.MP3 ? "mp3" : "wav";
            string outputTemplate = Path.Combine(outputFolder, "%(title)s.%(ext)s");

            var builder = new StringBuilder();
            builder.Append("-f ba --extract-audio --audio-format ");
            builder.Append(format);
            builder.Append(' ');
            builder.Append('"').Append(item.Url).Append('"');
            builder.Append(" -o \"").Append(outputTemplate).Append('"');

            if (item.Format == AudioFormat.WAV)
            {
                builder.Append(" --add-header \"Authorization: OAuth ").Append(authToken).Append('"');
            }

            string archivePath = Path.Combine(outputFolder, ".download-archive");
            builder.Append(" --download-archive \"").Append(archivePath).Append('"');
            builder.Append(" --newline --extractor-retries 10 --retry-sleep extractor:300");
            return builder.ToString();
        }
    }
}
