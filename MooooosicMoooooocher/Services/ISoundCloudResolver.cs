namespace MooooosicMoooooocher.Services
{
    /// <summary>
    /// Resolves SoundCloud track IDs (the bare numeric IDs that appear as
    /// <c>api-v2.soundcloud.com/tracks/&lt;id&gt;</c> entries in playlist enumeration)
    /// to their canonical web URLs and titles via SoundCloud's batch tracks endpoint.
    /// </summary>
    public interface ISoundCloudResolver
    {
        Task<IReadOnlyDictionary<long, ResolvedTrack>> ResolveTrackIdsAsync(
            IReadOnlyCollection<long> trackIds,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Quickly fetches the total track count for a playlist URL or the likes count
        /// for a profile/likes URL via SoundCloud's /resolve endpoint. Returns null if
        /// the URL can't be resolved or the response shape changed (caller should fall
        /// back to "X so far..." progress without a known total).
        /// </summary>
        Task<int?> GetPlaylistOrLikesCountAsync(
            string url,
            CancellationToken cancellationToken = default);
    }

    public sealed record ResolvedTrack(long Id, string PermalinkUrl, string Title);
}
