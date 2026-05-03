using System.Net;
using System.Text;
using MooooosicMoooooocher.Services;
using MooooosicMoooooocher.Tests.Helpers;
using Xunit;

namespace MooooosicMoooooocher.Tests
{
    public class SoundCloudResolverTests
    {
        private const string FakeClientId = "abcdefghijklmnopqrstuvwxyz123456";
        private const string SoundCloudHomepageHtml =
            "<html><head>" +
            "<script src=\"https://a-v2.sndcdn.com/assets/0-bundle.js\"></script>" +
            "<script src=\"https://a-v2.sndcdn.com/assets/49-target.js\"></script>" +
            "</head></html>";

        [Fact]
        public async Task ResolveTrackIdsAsync_EmptyInput_ReturnsEmpty()
        {
            var handler = new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("HTTP should not be called for empty input"));
            var resolver = new SoundCloudResolver(new HttpClient(handler));

            var result = await resolver.ResolveTrackIdsAsync(Array.Empty<long>());

            Assert.Empty(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_HappyPath_ReturnsCanonicalUrlsAndTitles()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("0-bundle.js"))
                {
                    return Ok("// no client_id here");
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"...,client_id:\"{FakeClientId}\",...");
                }
                if (req.RequestUri.Host == "api-v2.soundcloud.com" &&
                    req.RequestUri.AbsolutePath == "/tracks")
                {
                    return Ok("""
                        [
                          {"id": 47816886, "permalink_url": "https://soundcloud.com/skrillex/first-of-the-year-equinox", "title": "First Of The Year (Equinox)"},
                          {"id": 21792829, "permalink_url": "https://soundcloud.com/skrillex/skrillex-kyoto-feat-sirah", "title": "Skrillex - Kyoto feat Sirah"}
                        ]
                        """);
                }
                throw new InvalidOperationException($"Unexpected URL: {req.RequestUri}");
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            var result = await resolver.ResolveTrackIdsAsync(new long[] { 47816886, 21792829 });

            Assert.Equal(2, result.Count);
            Assert.Equal("https://soundcloud.com/skrillex/first-of-the-year-equinox", result[47816886].PermalinkUrl);
            Assert.Equal("First Of The Year (Equinox)", result[47816886].Title);
            Assert.Equal("https://soundcloud.com/skrillex/skrillex-kyoto-feat-sirah", result[21792829].PermalinkUrl);
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_HomepageFails_ReportsUpdateNeeded()
        {
            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var resolver = new SoundCloudResolver(new HttpClient(handler));

            var phases = new List<DownloadProgress>();
            var progress = new Progress<DownloadProgress>(p => phases.Add(p));

            var result = await resolver.ResolveTrackIdsAsync(new long[] { 1, 2, 3 }, progress);
            await Task.Delay(50);

            Assert.Empty(result);
            Assert.Contains(phases, p =>
                p.Phase == DownloadPhase.Failed &&
                p.Message.Contains("update needed", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_NoClientIdInAnyScript_ReportsUpdateNeeded()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                // All script bundles contain no client_id
                return Ok("var foo = 1;");
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));

            var phases = new List<DownloadProgress>();
            var progress = new Progress<DownloadProgress>(p => phases.Add(p));

            var result = await resolver.ResolveTrackIdsAsync(new long[] { 1 }, progress);
            await Task.Delay(50);

            Assert.Empty(result);
            Assert.Contains(phases, p =>
                p.Phase == DownloadPhase.Failed &&
                p.Message.Contains("update needed", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_ApiCallReturns401_RetriesWithFreshClientId()
        {
            int homepageCalls = 0;
            int apiCallNumber = 0;
            const string secondClientId = "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";

            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    homepageCalls++;
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    // first scrape returns the original client_id; second scrape returns the new one
                    string id = homepageCalls == 1 ? FakeClientId : secondClientId;
                    return Ok(req.RequestUri.AbsoluteUri.Contains("49-target.js")
                        ? $"client_id:\"{id}\""
                        : "// none");
                }
                if (req.RequestUri.Host == "api-v2.soundcloud.com")
                {
                    apiCallNumber++;
                    if (apiCallNumber == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    }
                    Assert.Contains($"client_id={secondClientId}", req.RequestUri.AbsoluteUri);
                    return Ok("[{\"id\": 1, \"permalink_url\": \"https://soundcloud.com/u/t\", \"title\": \"T\"}]");
                }
                throw new InvalidOperationException();
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            var result = await resolver.ResolveTrackIdsAsync(new long[] { 1 });

            Assert.Single(result);
            Assert.Equal("https://soundcloud.com/u/t", result[1].PermalinkUrl);
            Assert.Equal(2, apiCallNumber);
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_BatchesLargeIdLists()
        {
            int batchCallCount = 0;
            var allRequestedIds = new List<long>();

            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                if (req.RequestUri.Host == "api-v2.soundcloud.com")
                {
                    batchCallCount++;
                    var query = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
                    string idsParam = query["ids"] ?? string.Empty;
                    var ids = idsParam.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(long.Parse).ToList();
                    allRequestedIds.AddRange(ids);

                    var sb = new StringBuilder("[");
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        sb.Append($"{{\"id\":{ids[i]},\"permalink_url\":\"https://soundcloud.com/u/t{ids[i]}\",\"title\":\"T{ids[i]}\"}}");
                    }
                    sb.Append("]");
                    return Ok(sb.ToString());
                }
                throw new InvalidOperationException();
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            // 120 IDs should split into 3 batches (50 + 50 + 20)
            var inputIds = Enumerable.Range(1, 120).Select(i => (long)i).ToList();
            var result = await resolver.ResolveTrackIdsAsync(inputIds);

            Assert.Equal(120, result.Count);
            Assert.Equal(3, batchCallCount);
            Assert.Equal(120, allRequestedIds.Count);
            Assert.Equal(120, allRequestedIds.Distinct().Count());
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_PartialBatchFailure_KeepsSuccessfulOnes()
        {
            int batchCallCount = 0;

            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                if (req.RequestUri.Host == "api-v2.soundcloud.com")
                {
                    batchCallCount++;
                    if (batchCallCount == 1)
                    {
                        return Ok("[{\"id\":1,\"permalink_url\":\"https://soundcloud.com/u/a\",\"title\":\"A\"}]");
                    }
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }
                throw new InvalidOperationException();
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));

            var phases = new List<DownloadProgress>();
            var progress = new Progress<DownloadProgress>(p => phases.Add(p));

            // 60 IDs → 2 batches
            var inputIds = Enumerable.Range(1, 60).Select(i => (long)i).ToList();
            var result = await resolver.ResolveTrackIdsAsync(inputIds, progress);
            await Task.Delay(50);

            Assert.Single(result);
            Assert.True(result.ContainsKey(1));
            Assert.Contains(phases, p =>
                p.Phase == DownloadPhase.Failed &&
                p.Message.Contains("update needed", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_MalformedJson_ReportsUpdateNeeded()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                return Ok("this is { not valid json");
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));

            var phases = new List<DownloadProgress>();
            var progress = new Progress<DownloadProgress>(p => phases.Add(p));

            var result = await resolver.ResolveTrackIdsAsync(new long[] { 1 }, progress);
            await Task.Delay(50);

            Assert.Empty(result);
            Assert.Contains(phases, p =>
                p.Phase == DownloadPhase.Failed &&
                p.Message.Contains("update needed", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_TrackMissingPermalink_IsSkipped()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                return Ok("""
                    [
                      {"id": 1, "permalink_url": "https://soundcloud.com/u/a", "title": "A"},
                      {"id": 2, "title": "B"},
                      {"id": 3, "permalink_url": null, "title": "C"}
                    ]
                    """);
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            var result = await resolver.ResolveTrackIdsAsync(new long[] { 1, 2, 3 });

            Assert.Single(result);
            Assert.True(result.ContainsKey(1));
            Assert.False(result.ContainsKey(2));
            Assert.False(result.ContainsKey(3));
        }

        [Fact]
        public async Task ResolveTrackIdsAsync_CachesClientIdAcrossCalls()
        {
            int homepageHits = 0;

            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    homepageHits++;
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                return Ok("[{\"id\":1,\"permalink_url\":\"https://soundcloud.com/u/t\",\"title\":\"T\"}]");
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            await resolver.ResolveTrackIdsAsync(new long[] { 1 });
            await resolver.ResolveTrackIdsAsync(new long[] { 2 });
            await resolver.ResolveTrackIdsAsync(new long[] { 3 });

            Assert.Equal(1, homepageHits);
        }

        [Fact]
        public async Task GetPlaylistOrLikesCountAsync_PlaylistUrl_ReturnsTrackCount()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                if (req.RequestUri.AbsolutePath == "/resolve")
                {
                    string queryUrl = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query)["url"]!;
                    Assert.Equal("https://soundcloud.com/user/sets/playlist", queryUrl);
                    return Ok("""{"kind": "playlist", "id": 123, "track_count": 18}""");
                }
                throw new InvalidOperationException();
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            int? count = await resolver.GetPlaylistOrLikesCountAsync(
                "https://soundcloud.com/user/sets/playlist");

            Assert.Equal(18, count);
        }

        [Fact]
        public async Task GetPlaylistOrLikesCountAsync_LikesUrl_ResolvesUserAndReturnsLikesCount()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                if (req.RequestUri.AbsolutePath == "/resolve")
                {
                    string queryUrl = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query)["url"]!;
                    Assert.Equal("https://soundcloud.com/jhiba", queryUrl);
                    return Ok("""{"kind": "user", "id": 999, "likes_count": 3700}""");
                }
                throw new InvalidOperationException();
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            int? count = await resolver.GetPlaylistOrLikesCountAsync(
                "https://soundcloud.com/jhiba/likes");

            Assert.Equal(3700, count);
        }

        [Fact]
        public async Task GetPlaylistOrLikesCountAsync_ResolveReturns404_ReturnsNull()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                if (req.RequestUri.AbsolutePath == "/resolve")
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }
                throw new InvalidOperationException();
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            int? count = await resolver.GetPlaylistOrLikesCountAsync(
                "https://soundcloud.com/user/sets/missing");

            Assert.Null(count);
        }

        [Fact]
        public async Task GetPlaylistOrLikesCountAsync_NoClientId_ReturnsNull()
        {
            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            int? count = await resolver.GetPlaylistOrLikesCountAsync(
                "https://soundcloud.com/foo/sets/bar");

            Assert.Null(count);
        }

        [Fact]
        public async Task GetPlaylistOrLikesCountAsync_MissingFieldInResponse_ReturnsNull()
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.Host == "soundcloud.com")
                {
                    return Ok(SoundCloudHomepageHtml);
                }
                if (req.RequestUri.AbsoluteUri.Contains("49-target.js"))
                {
                    return Ok($"client_id:\"{FakeClientId}\"");
                }
                if (req.RequestUri.AbsoluteUri.Contains(".js"))
                {
                    return Ok("// none");
                }
                if (req.RequestUri.AbsolutePath == "/resolve")
                {
                    return Ok("""{"kind": "playlist", "id": 123}""");
                }
                throw new InvalidOperationException();
            });

            var resolver = new SoundCloudResolver(new HttpClient(handler));
            int? count = await resolver.GetPlaylistOrLikesCountAsync(
                "https://soundcloud.com/user/sets/foo");

            Assert.Null(count);
        }

        private static HttpResponseMessage Ok(string body) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain")
            };
    }
}
