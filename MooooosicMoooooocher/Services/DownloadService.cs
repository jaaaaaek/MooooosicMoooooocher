using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MooooosicMoooooocher.Models;

namespace MooooosicMoooooocher.Services
{
    public class DownloadService : IDownloadService
    {
        private static readonly Regex ProgressRegex = new(
            "\\[download\\]\\s+(?<percent>\\d+(\\.\\d+)?)%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DestinationRegex = new(
            "Destination:\\s(?<name>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ApiV2TrackRegex = new(
            @"^https?://api-v2\.soundcloud\.com/tracks/(?<id>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ConcurrentDictionary<Guid, Process> _activeProcesses = new();
        private readonly ISoundCloudResolver? _resolver;

        public DownloadService(ISoundCloudResolver? resolver = null)
        {
            _resolver = resolver;
        }

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
                return new DownloadResult(false, "Auth token is required for WAV downloads.", null);
            }

            string ytDlpPath = Path.Combine(AppContext.BaseDirectory, YtDlpService.YtDlpExecutable);
            if (!File.Exists(ytDlpPath))
            {
                return new DownloadResult(false, $"{YtDlpService.YtDlpExecutable} was not found in the application folder.", null);
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
            // See note in ResolvePlaylistAsync: forces line-flushing on yt-dlp's
            // stdout/stderr so download progress lines stream in real time
            // instead of appearing in chunks every few seconds.
            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

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
                if (string.IsNullOrWhiteSpace(e.Data))
                {
                    return;
                }

                errorBuilder.AppendLine(e.Data);

                // yt-dlp prints "ERROR:" lines to stderr (often twice - once during
                // extraction, once on exit). Suppress per-line forwarding to the
                // console so the user sees only the consolidated "FAILED: <url> -
                // <message>" summary that MainWindowViewModel emits. The error text
                // is still captured in errorBuilder and surfaces via that summary.
                // Other stderr lines (status, [soundcloud], [download], WARNING:)
                // continue to forward as before.
                if (e.Data.TrimStart().StartsWith("ERROR", StringComparison.Ordinal))
                {
                    return;
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

                // Don't progress.Report the captured error here - MainWindowViewModel
                // already emits a single consolidated "FAILED: <url> - <error>" line
                // from result.ErrorMessage. Forwarding it again would echo the ERROR
                // text a second time underneath the FAILED summary.
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Failed,
                    Message = string.Empty
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

        public async Task<IReadOnlyList<string>> ResolvePlaylistAsync(
            string url,
            string? authToken,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string ytDlpPath = Path.Combine(AppContext.BaseDirectory, YtDlpService.YtDlpExecutable);
            if (!File.Exists(ytDlpPath))
            {
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Failed,
                    Message = $"{YtDlpService.YtDlpExecutable} was not found."
                });
                return [];
            }

            // Try to get the total count up-front via SoundCloud's /resolve endpoint
            // (~300ms). If it succeeds we can show "X/N resolved..." live updates
            // during yt-dlp's slow flat-playlist enumeration; if it fails we fall
            // back to the same updater but with "Found X tracks so far..." (no Y).
            int? totalCount = null;
            if (_resolver != null)
            {
                totalCount = await _resolver.GetPlaylistOrLikesCountAsync(url, cancellationToken);
                if (totalCount.HasValue)
                {
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Checking,
                        Message = $"Found {totalCount.Value} songs."
                    });

                    // Emit the live counter line AT ZERO immediately so the user
                    // sees it from t=0 rather than waiting 1-2s for yt-dlp's
                    // process startup + first page fetch before any progress
                    // appears. Subsequent updates replace this line in place.
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Checking,
                        Message = $"0/{totalCount.Value} resolved...",
                        LiveKey = "enumeration"
                    });
                }
            }

            var args = new StringBuilder();
            args.Append("--flat-playlist --print url ");
            args.Append('"').Append(url).Append('"');

            if (!string.IsNullOrWhiteSpace(authToken))
            {
                args.Append(" --add-header \"Authorization: OAuth ").Append(authToken).Append('"');
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = args.ToString(),
                WorkingDirectory = Path.GetDirectoryName(ytDlpPath) ?? AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // Force yt-dlp's embedded Python to flush stdout/stderr per line.
            // Without this, Python block-buffers when stdio is redirected to pipes
            // (our case), so URLs print()ed during pagination sit in yt-dlp's
            // 4-8KB buffer until full -> the live counter stays at 0/N for many
            // seconds even though yt-dlp is processing tracks just fine.
            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

            // Visible "we're working" message during the cold-start delay (process
            // spawn + Python init + yt-dlp's own /resolve + first page fetch).
            // Appears below the live counter; counter ticks up at its tracked
            // index above this line.
            progress?.Report(new DownloadProgress
            {
                Phase = DownloadPhase.Checking,
                Message = "Working in the background (large libraries may take a minute)..."
            });

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            var urls = new List<string>();
            var errorBuilder = new StringBuilder();

            // Time-throttle the per-URL progress so libraries with thousands of
            // tracks don't flood the UI thread. Updates happen in place via the
            // "enumeration" LiveKey rather than appending each tick. 100ms (~10fps)
            // looks real-time to humans (can't perceive faster than ~10fps as
            // discrete updates) and bounds UI work regardless of yt-dlp's burst
            // pagination pattern.
            DateTime lastProgressEmit = DateTime.MinValue;
            TimeSpan progressMinInterval = TimeSpan.FromMilliseconds(100);
            int capturedTotal = totalCount ?? -1;

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                {
                    return;
                }

                // Just collect URLs here - per-track "Found:" messages are emitted
                // AFTER the batch resolution step below so the user sees real names
                // instead of "track #47816886" for api-v2 entries.
                urls.Add(e.Data.Trim());

                var now = DateTime.UtcNow;
                if (now - lastProgressEmit >= progressMinInterval)
                {
                    lastProgressEmit = now;
                    string msg = capturedTotal > 0
                        ? $"{urls.Count}/{capturedTotal} resolved..."
                        : $"Found {urls.Count} tracks so far...";
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Checking,
                        Message = msg,
                        LiveKey = "enumeration"
                    });
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    progress?.Report(new DownloadProgress
                    {
                        Phase = DownloadPhase.Checking,
                        Message = e.Data
                    });
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string errorSummary = errorBuilder.Length > 0
                    ? errorBuilder.ToString().Trim()
                    : $"yt-dlp exited with code {process.ExitCode}.";

                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Failed,
                    Message = $"Failed to resolve {url}: {errorSummary}"
                });

                return [];
            }

            // Settle the live enumeration line on its final value (X/N or "Found N
            // tracks.") so it doesn't get stuck at a stale throttled mid-count.
            string finalEnumerationMsg = capturedTotal > 0
                ? $"{urls.Count}/{capturedTotal} resolved."
                : $"Found {urls.Count} tracks.";
            progress?.Report(new DownloadProgress
            {
                Phase = DownloadPhase.Checking,
                Message = finalEnumerationMsg,
                LiveKey = "enumeration"
            });

            // Replace bare api-v2 track-ID URLs with their canonical permalink URLs so
            // the queue cards can show readable slug-derived names instead of raw IDs.
            await ReplaceApiV2UrlsAsync(urls, progress, cancellationToken);

            progress?.Report(new DownloadProgress
            {
                Phase = DownloadPhase.Complete,
                Message = $"Resolved {urls.Count} track(s)."
            });

            return urls;
        }

        // Batch-lookup the canonical URL for any api-v2.soundcloud.com/tracks/<id>
        // entries in the resolved URL list and replace them in place. No-op when the
        // resolver isn't wired in or when the playlist had no api-v2 entries.
        private async Task ReplaceApiV2UrlsAsync(
            List<string> urls,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (_resolver == null || urls.Count == 0)
            {
                return;
            }

            var indicesByTrackId = new Dictionary<long, List<int>>();
            for (int i = 0; i < urls.Count; i++)
            {
                var match = ApiV2TrackRegex.Match(urls[i]);
                if (match.Success && long.TryParse(match.Groups["id"].Value, out long id))
                {
                    if (!indicesByTrackId.TryGetValue(id, out var list))
                    {
                        indicesByTrackId[id] = list = new List<int>();
                    }
                    list.Add(i);
                }
            }

            if (indicesByTrackId.Count == 0)
            {
                return;
            }

            // The resolver itself emits per-batch "Looking up names: X/Y..."
            // progress messages, so no separate kickoff line here.

            var resolved = await _resolver.ResolveTrackIdsAsync(
                indicesByTrackId.Keys.ToList(),
                progress,
                cancellationToken);

            int replaced = 0;
            foreach (var (id, indices) in indicesByTrackId)
            {
                if (resolved.TryGetValue(id, out var track) &&
                    !string.IsNullOrEmpty(track.PermalinkUrl))
                {
                    foreach (int idx in indices)
                    {
                        urls[idx] = track.PermalinkUrl;
                    }
                    replaced++;
                }
            }

            if (replaced > 0)
            {
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Checking,
                    Message = $"Resolved names for {replaced}/{indicesByTrackId.Count} track(s)."
                });
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

            // Only pin --ffmpeg-location when ffmpeg.exe is actually next to the app.
            // Otherwise yt-dlp treats the path as authoritative and stops looking on
            // PATH, which breaks downloads on machines where the user already has a
            // system-wide ffmpeg install.
            string ffmpegDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string ffmpegExeName = OperatingSystem.IsMacOS() ? "ffmpeg" : "ffmpeg.exe";
            if (File.Exists(Path.Combine(ffmpegDir, ffmpegExeName)))
            {
                builder.Append(" --ffmpeg-location \"").Append(ffmpegDir).Append('"');
            }

            string archivePath = Path.Combine(outputFolder, ".download-archive");
            builder.Append(" --download-archive \"").Append(archivePath).Append('"');
            builder.Append(" --newline --extractor-retries 10 --retry-sleep extractor:300");
            return builder.ToString();
        }
    }
}
