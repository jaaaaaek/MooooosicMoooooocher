namespace MooooosicMoooooocher.Services
{
    public interface IUrlValidator
    {
        UrlValidationResult Validate(string url, IReadOnlyCollection<string>? existingUrls = null);
        bool IsResolvableUrl(string url);

        /// <summary>
        /// Coerces lenient user input into a canonical SoundCloud URL:
        /// prepends https:// when missing, and rewrites a bare profile URL
        /// (single-segment path) into the user's likes page.
        /// Returns the input unchanged if no rewriting applies.
        /// </summary>
        string Normalize(string url);
    }

    public readonly record struct UrlValidationResult(bool IsValid, string Message);
}
