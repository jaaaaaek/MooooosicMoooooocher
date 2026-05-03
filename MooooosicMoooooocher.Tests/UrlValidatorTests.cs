using MooooosicMoooooocher.Services;
using Xunit;

namespace MooooosicMoooooocher.Tests
{
    public class UrlValidatorTests
    {
        private readonly UrlValidator _validator = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n")]
        public void Validate_EmptyOrWhitespace_Fails(string? url)
        {
            var result = _validator.Validate(url!);
            Assert.False(result.IsValid);
            Assert.Equal("URL is empty.", result.Message);
        }

        [Theory]
        [InlineData("not a url")]
        [InlineData("soundcloud.com/foo/bar")]
        public void Validate_NotAbsoluteUrl_Fails(string url)
        {
            var result = _validator.Validate(url);
            Assert.False(result.IsValid);
            Assert.Equal("Invalid URL format.", result.Message);
        }

        [Theory]
        [InlineData("ftp://soundcloud.com/foo/bar")]
        [InlineData("file:///c:/foo")]
        public void Validate_NonHttpScheme_Fails(string url)
        {
            var result = _validator.Validate(url);
            Assert.False(result.IsValid);
            Assert.Equal("URL must start with http or https.", result.Message);
        }

        [Theory]
        [InlineData("https://youtube.com/watch?v=abc")]
        [InlineData("https://example.com/foo/bar")]
        [InlineData("https://soundcloud.example.com/foo/bar")]
        public void Validate_NonSoundCloudHost_Fails(string url)
        {
            var result = _validator.Validate(url);
            Assert.False(result.IsValid);
            Assert.Equal("URL is not supported.", result.Message);
        }

        [Theory]
        [InlineData("https://soundcloud.com/")]
        [InlineData("https://soundcloud.com/justuser")]
        public void Validate_TooFewPathSegments_Fails(string url)
        {
            var result = _validator.Validate(url);
            Assert.False(result.IsValid);
            Assert.Equal("URL is missing a track or set path.", result.Message);
        }

        [Theory]
        [InlineData("https://soundcloud.com/charts/top")]
        [InlineData("https://soundcloud.com/discover/anything")]
        [InlineData("https://soundcloud.com/feed/something")]
        [InlineData("https://soundcloud.com/upload/now")]
        [InlineData("https://soundcloud.com/you/library")]
        [InlineData("https://soundcloud.com/CHARTS/TOP")]
        public void Validate_ReservedPath_Fails(string url)
        {
            var result = _validator.Validate(url);
            Assert.False(result.IsValid);
            Assert.Equal("URL is a system page, not a track or set.", result.Message);
        }

        [Theory]
        [InlineData("https://soundcloud.com/some-user/some-track")]
        [InlineData("https://soundcloud.com/some_user/some_track")]
        [InlineData("https://soundcloud.com/user123/track456")]
        [InlineData("https://soundcloud.com/user/track/")]
        [InlineData("https://soundcloud.com/user/track?in=user/sets/playlist")]
        [InlineData("HTTPS://SOUNDCLOUD.COM/user/track")]
        [InlineData("http://soundcloud.com/user/track")]
        public void Validate_ValidTrackUrl_Passes(string url)
        {
            var result = _validator.Validate(url);
            Assert.True(result.IsValid, $"Expected valid but got: {result.Message}");
            Assert.Equal(string.Empty, result.Message);
        }

        [Theory]
        [InlineData("https://soundcloud.com/user/sets/myplaylist")]
        [InlineData("https://soundcloud.com/user/sets/my-playlist")]
        [InlineData("https://soundcloud.com/user/sets/myplaylist/")]
        [InlineData("https://soundcloud.com/user/sets/myplaylist?si=abc")]
        public void Validate_ValidSetUrl_Passes(string url)
        {
            var result = _validator.Validate(url);
            Assert.True(result.IsValid, $"Expected valid but got: {result.Message}");
        }

        [Theory]
        [InlineData("https://soundcloud.com/user/likes")]
        [InlineData("https://soundcloud.com/user/likes/")]
        [InlineData("https://soundcloud.com/user/likes?foo=bar")]
        public void Validate_ValidLikesUrl_Passes(string url)
        {
            var result = _validator.Validate(url);
            Assert.True(result.IsValid, $"Expected valid but got: {result.Message}");
        }

        [Fact]
        public void Validate_MobileSubdomain_FailsRegexCheck()
        {
            // m.soundcloud.com passes the host check (EndsWith) and the segment-count check,
            // but the track regex requires the canonical "soundcloud.com" host.
            var result = _validator.Validate("https://m.soundcloud.com/user/track");
            Assert.False(result.IsValid);
            Assert.Equal("URL does not look like a track, set, or likes page.", result.Message);
        }

        [Fact]
        public void Validate_ShortLinkSubdomain_FailsSegmentCount()
        {
            // on.soundcloud.com short links have only one path segment, so they fail before
            // reaching the regex check.
            var result = _validator.Validate("https://on.soundcloud.com/abc123");
            Assert.False(result.IsValid);
            Assert.Equal("URL is missing a track or set path.", result.Message);
        }

        [Fact]
        public void Validate_DuplicateAgainstExistingUrls_Fails()
        {
            var existing = new[] { "https://soundcloud.com/user/track" };
            var result = _validator.Validate("https://soundcloud.com/user/track", existing);
            Assert.False(result.IsValid);
            Assert.Equal("URL has already been downloaded.", result.Message);
        }

        [Fact]
        public void Validate_DuplicateCheckIsCaseInsensitive()
        {
            var existing = new[] { "HTTPS://SOUNDCLOUD.COM/user/track" };
            var result = _validator.Validate("https://soundcloud.com/user/track", existing);
            Assert.False(result.IsValid);
            Assert.Equal("URL has already been downloaded.", result.Message);
        }

        [Fact]
        public void Validate_DuplicateCheckTrimsWhitespace()
        {
            var existing = new[] { "  https://soundcloud.com/user/track  " };
            var result = _validator.Validate("https://soundcloud.com/user/track", existing);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_NotADuplicate_Passes()
        {
            var existing = new[] { "https://soundcloud.com/other/track" };
            var result = _validator.Validate("https://soundcloud.com/user/track", existing);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_TrimsLeadingTrailingWhitespace()
        {
            var result = _validator.Validate("  https://soundcloud.com/user/track  ");
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("https://soundcloud.com/user/likes", true)]
        [InlineData("https://soundcloud.com/user/likes/", true)]
        [InlineData("HTTP://SOUNDCLOUD.COM/user/likes", true)]
        [InlineData("https://soundcloud.com/user/likes?si=abc", true)]
        [InlineData("https://soundcloud.com/user/sets/playlist", true)]
        [InlineData("https://soundcloud.com/user/sets/playlist/", true)]
        [InlineData("https://soundcloud.com/user/sets/my-playlist?si=abc", true)]
        [InlineData("https://soundcloud.com/statefromjaaaaakefarm/sets/helo", true)]
        [InlineData("https://soundcloud.com/user/track", false)]
        [InlineData("https://soundcloud.com/user", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("   ", false)]
        public void IsResolvableUrl_TrueForLikesAndSets(string? url, bool expected)
        {
            Assert.Equal(expected, _validator.IsResolvableUrl(url!));
        }

        [Theory]
        [InlineData("https://soundcloud.com/user", "https://soundcloud.com/user/likes")]
        [InlineData("http://soundcloud.com/user", "http://soundcloud.com/user/likes")]
        [InlineData("HTTPS://SOUNDCLOUD.COM/user", "https://soundcloud.com/user/likes")]
        [InlineData("https://soundcloud.com/user/", "https://soundcloud.com/user/likes")]
        [InlineData("https://soundcloud.com/user-name_123", "https://soundcloud.com/user-name_123/likes")]
        [InlineData("https://soundcloud.com/user?si=abc", "https://soundcloud.com/user/likes?si=abc")]
        public void Normalize_BareProfileUrlWithScheme_AppendsLikes(string input, string expected)
        {
            Assert.Equal(expected, _validator.Normalize(input));
        }

        [Theory]
        [InlineData("soundcloud.com/user", "https://soundcloud.com/user/likes")]
        [InlineData("soundcloud.com/user/likes", "https://soundcloud.com/user/likes")]
        [InlineData("soundcloud.com/user/track", "https://soundcloud.com/user/track")]
        [InlineData("soundcloud.com/user/sets/playlist", "https://soundcloud.com/user/sets/playlist")]
        [InlineData("  soundcloud.com/user  ", "https://soundcloud.com/user/likes")]
        public void Normalize_SchemelessSoundCloudUrl_PrependsHttpsThenAppliesRules(string input, string expected)
        {
            Assert.Equal(expected, _validator.Normalize(input));
        }

        [Theory]
        [InlineData("https://soundcloud.com/user/track")]
        [InlineData("https://soundcloud.com/user/sets/playlist")]
        [InlineData("https://soundcloud.com/user/likes")]
        public void Normalize_AlreadyValidUrl_ReturnsUnchanged(string input)
        {
            Assert.Equal(input, _validator.Normalize(input));
        }

        [Theory]
        [InlineData("https://soundcloud.com/charts")]
        [InlineData("https://soundcloud.com/discover")]
        [InlineData("https://soundcloud.com/feed")]
        [InlineData("https://soundcloud.com/upload")]
        [InlineData("https://soundcloud.com/you")]
        public void Normalize_ReservedPath_DoesNotAppendLikes(string input)
        {
            Assert.Equal(input, _validator.Normalize(input));
        }

        [Theory]
        [InlineData("https://soundcloud.com")]
        [InlineData("https://soundcloud.com/")]
        public void Normalize_NoPathSegments_ReturnsUnchanged(string input)
        {
            Assert.Equal(input, _validator.Normalize(input));
        }

        [Fact]
        public void Normalize_NonSoundCloudHost_PrependsSchemeButNoOtherChange()
        {
            Assert.Equal("https://youtube.com/watch?v=abc", _validator.Normalize("youtube.com/watch?v=abc"));
            Assert.Equal("https://example.com/foo", _validator.Normalize("https://example.com/foo"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Normalize_EmptyOrWhitespace_ReturnsAsIs(string? input)
        {
            string result = _validator.Normalize(input!);
            Assert.True(string.IsNullOrWhiteSpace(result) || result == input);
        }

        [Fact]
        public void Normalize_ThenValidate_AcceptsBareProfileUrl()
        {
            string normalized = _validator.Normalize("soundcloud.com/jhiba");
            var result = _validator.Validate(normalized);
            Assert.True(result.IsValid, $"Expected valid but got: {result.Message}");
            Assert.Equal("https://soundcloud.com/jhiba/likes", normalized);
        }

        [Fact]
        public void Normalize_ThenIsResolvableUrl_RecognizesNormalizedProfile()
        {
            string normalized = _validator.Normalize("https://soundcloud.com/jhiba");
            Assert.True(_validator.IsResolvableUrl(normalized));
        }

        [Fact]
        public void Normalize_ThenValidate_StillRejectsReservedPath()
        {
            // A single-segment reserved path (e.g. soundcloud.com/charts) is left
            // untouched by Normalize, so Validate's segment-count check fires first.
            string normalized = _validator.Normalize("soundcloud.com/charts");
            var result = _validator.Validate(normalized);
            Assert.False(result.IsValid);

            // A multi-segment reserved path is rejected with the system-page message.
            string normalized2 = _validator.Normalize("https://soundcloud.com/charts/top");
            var result2 = _validator.Validate(normalized2);
            Assert.False(result2.IsValid);
            Assert.Equal("URL is a system page, not a track or set.", result2.Message);
        }
    }
}
