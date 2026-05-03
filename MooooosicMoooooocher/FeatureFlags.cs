namespace MooooosicMoooooocher
{
    /// <summary>
    /// Compile-time feature flags. Flip a value here and rebuild to opt into
    /// development-only or experimental behavior. These exist as <c>const</c> fields
    /// (rather than <c>#if DEBUG</c> branches) so the same code paths run in both
    /// Debug and Release builds, and so the C# compiler eliminates dead code for
    /// disabled flags automatically.
    /// </summary>
    public static class FeatureFlags
    {
        /// <summary>
        /// When true, the welcome panel appears on every launch (treats both FFmpeg
        /// and yt-dlp as missing so the dependency-install flow runs). The flow
        /// still does a real install if the binary is genuinely missing from the
        /// app folder; if it's already there, it just plays the simulated animation
        /// to avoid hammering GitHub/gyan.dev on every iteration.
        ///
        /// Useful while iterating on the welcome panel UI without having to clear
        /// %APPDATA%\MooooosicMoooooocher\appsettings.json before every rebuild.
        /// </summary>
        public const bool AlwaysShowWelcomeOnLaunch = false;
    }
}
