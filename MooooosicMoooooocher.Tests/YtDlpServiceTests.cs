using System.Net;
using System.Security.Cryptography;
using System.Text;
using MooooosicMoooooocher.Services;
using MooooosicMoooooocher.Tests.Helpers;
using Xunit;

namespace MooooosicMoooooocher.Tests
{
    public class YtDlpServiceTests
    {
        private static string ExeName => OperatingSystem.IsMacOS() ? "yt-dlp" : "yt-dlp.exe";
        private static string ChecksumFileName => OperatingSystem.IsMacOS() ? "yt-dlp_macos" : "yt-dlp.exe";

        [Fact]
        public void GetYtDlpPath_ReturnsExpectedPath()
        {
            var service = new YtDlpService();
            using var temp = new TempFolder();

            string path = service.GetYtDlpPath(temp.Path);

            Assert.Equal(Path.Combine(temp.Path, ExeName), path);
        }

        [Fact]
        public void IsYtDlpAvailable_FileExistsInFolder_ReturnsTrue()
        {
            var service = new YtDlpService();
            using var temp = new TempFolder();
            File.WriteAllText(Path.Combine(temp.Path, ExeName), "stub");

            Assert.True(service.IsYtDlpAvailable(temp.Path));
        }

        [Fact]
        public async Task EnsureYtDlpAvailableAsync_AlreadyPresent_ShortCircuits()
        {
            using var temp = new TempFolder();
            File.WriteAllText(Path.Combine(temp.Path, ExeName), "stub");

            var handler = new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("HTTP should not have been called"));
            var service = new YtDlpService(new HttpClient(handler));

            bool result = await service.EnsureYtDlpAvailableAsync(temp.Path);

            Assert.True(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task EnsureYtDlpAvailableAsync_DownloadsAndVerifies()
        {
            using var temp = new TempFolder();

            // Skip the download path if yt-dlp is on PATH (rare on dev machines, but possible).
            if (new YtDlpService().IsYtDlpAvailable(temp.Path))
            {
                bool ok = await new YtDlpService().EnsureYtDlpAvailableAsync(temp.Path);
                Assert.True(ok);
                return;
            }

            byte[] fakeBinary = Encoding.UTF8.GetBytes("fake yt-dlp content");
            string expectedHash = Convert.ToHexString(SHA256.HashData(fakeBinary));
            string checksumContent =
                $"{expectedHash}  {ChecksumFileName}\n" +
                $"{new string('0', 64)}  some_other_file\n";

            var handler = new StubHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                if (req.RequestUri!.AbsoluteUri.Contains("SHA2-256SUMS"))
                {
                    response.Content = new StringContent(checksumContent);
                }
                else
                {
                    response.Content = new ByteArrayContent(fakeBinary);
                }
                return response;
            });

            var service = new YtDlpService(new HttpClient(handler));
            bool result = await service.EnsureYtDlpAvailableAsync(temp.Path);

            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(temp.Path, ExeName)));
        }

        [Fact]
        public async Task EnsureYtDlpAvailableAsync_BadChecksum_FailsAndDeletesFile()
        {
            using var temp = new TempFolder();

            if (new YtDlpService().IsYtDlpAvailable(temp.Path))
            {
                return;
            }

            byte[] fakeBinary = Encoding.UTF8.GetBytes("real content");
            string wrongHash = new string('b', 64);
            string checksumContent = $"{wrongHash}  {ChecksumFileName}\n";

            var handler = new StubHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                if (req.RequestUri!.AbsoluteUri.Contains("SHA2-256SUMS"))
                {
                    response.Content = new StringContent(checksumContent);
                }
                else
                {
                    response.Content = new ByteArrayContent(fakeBinary);
                }
                return response;
            });

            var service = new YtDlpService(new HttpClient(handler));
            bool result = await service.EnsureYtDlpAvailableAsync(temp.Path);

            Assert.False(result);
            Assert.False(File.Exists(Path.Combine(temp.Path, ExeName)));
        }

        [Fact]
        public async Task EnsureYtDlpAvailableAsync_ChecksumMissingFromSumsFile_Fails()
        {
            using var temp = new TempFolder();

            if (new YtDlpService().IsYtDlpAvailable(temp.Path))
            {
                return;
            }

            byte[] fakeBinary = Encoding.UTF8.GetBytes("content");
            string checksumContent = $"{new string('c', 64)}  unrelated_file\n";

            var handler = new StubHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                if (req.RequestUri!.AbsoluteUri.Contains("SHA2-256SUMS"))
                {
                    response.Content = new StringContent(checksumContent);
                }
                else
                {
                    response.Content = new ByteArrayContent(fakeBinary);
                }
                return response;
            });

            var service = new YtDlpService(new HttpClient(handler));
            bool result = await service.EnsureYtDlpAvailableAsync(temp.Path);

            Assert.False(result);
        }

        [Fact]
        public async Task EnsureYtDlpAvailableAsync_HttpFailure_ReturnsFalse()
        {
            using var temp = new TempFolder();

            if (new YtDlpService().IsYtDlpAvailable(temp.Path))
            {
                return;
            }

            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("nope")
                });

            var service = new YtDlpService(new HttpClient(handler));
            bool result = await service.EnsureYtDlpAvailableAsync(temp.Path);

            Assert.False(result);
        }

        [Fact]
        public void YtDlpExecutable_MatchesPlatform()
        {
            string expected = OperatingSystem.IsMacOS() ? "yt-dlp" : "yt-dlp.exe";
            Assert.Equal(expected, YtDlpService.YtDlpExecutable);
        }
    }
}
