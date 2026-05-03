using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MooooosicMoooooocher.Services
{
    /// <summary>
    /// Talks to SoundCloud's private api-v2 endpoint to batch-resolve track IDs into
    /// canonical permalink URLs + titles. Requires a client_id that we scrape from
    /// soundcloud.com's JS bundles (mirrors what yt-dlp does internally).
    ///
    /// If SoundCloud changes its bundle layout or rotates client_ids unexpectedly,
    /// callers will see <see cref="UpdateNeededMessage"/> in the progress stream.
    /// </summary>
    public class SoundCloudResolver : ISoundCloudResolver
    {
        public const string UpdateNeededMessage =
            "SoundCloud track name lookup failed - update needed, please contact support.";

        private const int BatchSize = 50;
        private const string SoundCloudHomepage = "https://soundcloud.com/";

        private static readonly Regex ScriptSrcRegex = new(
            @"<script[^>]+\bsrc=""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ClientIdRegex = new(
            @"client_id\s*[:=]\s*[""']([0-9a-zA-Z]{32})[""']",
            RegexOptions.Compiled);

        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _clientIdLock = new(1, 1);
        private string? _cachedClientId;

        public SoundCloudResolver(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }
        }

        public async Task<int?> GetPlaylistOrLikesCountAsync(
            string url,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            string? clientId = await GetClientIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(clientId))
            {
                return null;
            }

            try
            {
                // /likes URLs aren't directly resolvable - resolve the user instead and
                // read likes_count off the user object.
                bool isLikes = url.EndsWith("/likes", StringComparison.OrdinalIgnoreCase) ||
                               url.Contains("/likes?", StringComparison.OrdinalIgnoreCase) ||
                               url.EndsWith("/likes/", StringComparison.OrdinalIgnoreCase);
                string resolveTarget = url;
                if (isLikes)
                {
                    int idx = url.IndexOf("/likes", StringComparison.OrdinalIgnoreCase);
                    if (idx > 0)
                    {
                        resolveTarget = url.Substring(0, idx);
                    }
                }

                string apiUrl = $"https://api-v2.soundcloud.com/resolve?url={Uri.EscapeDataString(resolveTarget)}&client_id={clientId}";
                using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (isLikes)
                {
                    if (root.TryGetProperty("likes_count", out var likes) && likes.TryGetInt32(out int n))
                    {
                        return n;
                    }
                    return null;
                }

                if (root.TryGetProperty("track_count", out var trackCount) && trackCount.TryGetInt32(out int tc))
                {
                    return tc;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IReadOnlyDictionary<long, ResolvedTrack>> ResolveTrackIdsAsync(
            IReadOnlyCollection<long> trackIds,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<long, ResolvedTrack>();
            if (trackIds.Count == 0)
            {
                return result;
            }

            string? clientId = await GetClientIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(clientId))
            {
                progress?.Report(new DownloadProgress
                {
                    Phase = DownloadPhase.Failed,
                    Message = UpdateNeededMessage + " (could not retrieve API client_id)"
                });
                return result;
            }

            int batchIndex = 0;
            int totalBatches = (trackIds.Count + BatchSize - 1) / BatchSize;
            int doneCount = 0;

            // Time-throttle the per-batch progress so libraries with hundreds of
            // batches don't flood the console. Always emit on the final batch
            // regardless of throttle so the user sees the completion line.
            DateTime lastProgressEmit = DateTime.MinValue;
            TimeSpan progressMinInterval = TimeSpan.FromMilliseconds(1500);

            foreach (var chunk in trackIds.Chunk(BatchSize))
            {
                batchIndex++;
                cancellationToken.ThrowIfCancellationRequested();

                bool retried = false;
                while (true)
                {
                    try
                    {
                        string idsParam = string.Join(",", chunk);
                        string apiUrl = $"https://api-v2.soundcloud.com/tracks?ids={idsParam}&client_id={clientId}";

                        using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);

                        if (response.StatusCode == HttpStatusCode.Unauthorized && !retried)
                        {
                            // client_id likely rotated since we scraped it - drop the cache and try once more
                            _cachedClientId = null;
                            string? fresh = await GetClientIdAsync(cancellationToken);
                            if (string.IsNullOrEmpty(fresh))
                            {
                                progress?.Report(new DownloadProgress
                                {
                                    Phase = DownloadPhase.Failed,
                                    Message = UpdateNeededMessage + " (API client_id rejected and could not be refreshed)"
                                });
                                return result;
                            }
                            clientId = fresh;
                            retried = true;
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        ParseTracksResponse(stream, result);
                        doneCount += chunk.Length;

                        var now = DateTime.UtcNow;
                        bool isLast = batchIndex == totalBatches;
                        if (isLast || now - lastProgressEmit >= progressMinInterval)
                        {
                            lastProgressEmit = now;
                            progress?.Report(new DownloadProgress
                            {
                                Phase = DownloadPhase.Checking,
                                Message = $"Looking up names: {doneCount}/{trackIds.Count}...",
                                LiveKey = "lookup"
                            });
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        progress?.Report(new DownloadProgress
                        {
                            Phase = DownloadPhase.Failed,
                            Message = UpdateNeededMessage + $" (batch {batchIndex}/{totalBatches}: {ex.Message})"
                        });
                        break;
                    }
                }
            }

            return result;
        }

        private static void ParseTracksResponse(Stream stream, Dictionary<long, ResolvedTrack> destination)
        {
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("id", out var idEl) || !idEl.TryGetInt64(out long id))
                {
                    continue;
                }

                string permalink = element.TryGetProperty("permalink_url", out var p)
                    ? p.GetString() ?? string.Empty
                    : string.Empty;
                string title = element.TryGetProperty("title", out var t)
                    ? t.GetString() ?? string.Empty
                    : string.Empty;

                if (!string.IsNullOrEmpty(permalink))
                {
                    destination[id] = new ResolvedTrack(id, permalink, title);
                }
            }
        }

        private async Task<string?> GetClientIdAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_cachedClientId))
            {
                return _cachedClientId;
            }

            await _clientIdLock.WaitAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrEmpty(_cachedClientId))
                {
                    return _cachedClientId;
                }

                string scraped = await ScrapeClientIdAsync(cancellationToken);
                _cachedClientId = string.IsNullOrEmpty(scraped) ? null : scraped;
                return _cachedClientId;
            }
            finally
            {
                _clientIdLock.Release();
            }
        }

        private async Task<string> ScrapeClientIdAsync(CancellationToken cancellationToken)
        {
            try
            {
                string html = await _httpClient.GetStringAsync(SoundCloudHomepage, cancellationToken);

                var scripts = ScriptSrcRegex.Matches(html)
                    .Select(m => m.Groups[1].Value)
                    .Where(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                (u.Contains("sndcdn.com", StringComparison.OrdinalIgnoreCase) ||
                                 u.Contains("soundcloud.com", StringComparison.OrdinalIgnoreCase)))
                    .Distinct()
                    .ToList();

                // The client_id is usually in one of the later (numbered) JS bundles.
                for (int i = scripts.Count - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        string js = await _httpClient.GetStringAsync(scripts[i], cancellationToken);
                        var match = ClientIdRegex.Match(js);
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                    catch
                    {
                        // try next script
                    }
                }
            }
            catch
            {
                // Fall through to empty result
            }

            return string.Empty;
        }
    }
}
