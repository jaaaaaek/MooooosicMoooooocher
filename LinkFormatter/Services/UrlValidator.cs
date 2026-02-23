using System.Text.RegularExpressions;

namespace LinkFormatter.Services
{
    public class UrlValidator : IUrlValidator
    {
        private static readonly HashSet<string> ReservedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "charts", "company", "discover", "feed", "getstarted", "imprint",
            "messages", "notifications", "pages", "terms-of-use", "transparency-reports",
            "upload", "you", "artists", "stations"
        };

        private static readonly Regex TrackRegex = new(
            @"^https?://soundcloud\.com/[\w-]+/[\w-]+/?(\?.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SetRegex = new(
            @"^https?://soundcloud\.com/[\w-]+/sets/[\w-]+/?(\?.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public UrlValidationResult Validate(string url, IReadOnlyCollection<string>? existingUrls = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return new UrlValidationResult(false, "URL is empty.");
            }

            url = url.Trim();

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return new UrlValidationResult(false, "Invalid URL format.");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return new UrlValidationResult(false, "URL must start with http or https.");
            }

            if (!uri.Host.EndsWith("soundcloud.com", StringComparison.OrdinalIgnoreCase))
            {
                return new UrlValidationResult(false, "Only soundcloud.com URLs are supported.");
            }

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return new UrlValidationResult(false, "URL is missing a track or set path.");
            }

            if (ReservedPaths.Contains(segments[0]))
            {
                return new UrlValidationResult(false, "URL is a SoundCloud system page, not a track or set.");
            }

            if (!TrackRegex.IsMatch(url) && !SetRegex.IsMatch(url))
            {
                return new UrlValidationResult(false, "URL does not look like a track or set.");
            }

            if (existingUrls != null && existingUrls.Any(existing =>
                    string.Equals(existing.Trim(), url, StringComparison.OrdinalIgnoreCase)))
            {
                return new UrlValidationResult(false, "URL has already been downloaded.");
            }

            return new UrlValidationResult(true, string.Empty);
        }
    }
}
