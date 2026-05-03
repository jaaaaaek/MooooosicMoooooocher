using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using MooooosicMoooooocher.Services;
using MooooosicMoooooocher.Tests.Helpers;
using Xunit;

namespace MooooosicMoooooocher.Tests
{
    public class FFmpegServiceTests
    {
        private static string ExeName => OperatingSystem.IsMacOS() ? "ffmpeg" : "ffmpeg.exe";

        [Fact]
        public void GetFFmpegPath_ReturnsExpectedPath()
        {
            var service = new FFmpegService();
            using var temp = new TempFolder();

            string path = service.GetFFmpegPath(temp.Path);

            Assert.Equal(Path.Combine(temp.Path, ExeName), path);
        }

        [Fact]
        public void IsFFmpegAvailable_FileExistsInFolder_ReturnsTrue()
        {
            var service = new FFmpegService();
            using var temp = new TempFolder();
            File.WriteAllText(Path.Combine(temp.Path, ExeName), "stub");

            Assert.True(service.IsFFmpegAvailable(temp.Path));
        }

        [Fact]
        public async Task EnsureFFmpegAvailableAsync_AlreadyPresent_ShortCircuits()
        {
            using var temp = new TempFolder();
            File.WriteAllText(Path.Combine(temp.Path, ExeName), "stub");

            var handler = new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("HTTP should not have been called"));
            var service = new FFmpegService(new HttpClient(handler));

            bool result = await service.EnsureFFmpegAvailableAsync(temp.Path);

            Assert.True(result);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task EnsureFFmpegAvailableAsync_DownloadsExtractsAndVerifies()
        {
            using var temp = new TempFolder();

            // The download is short-circuited if ffmpeg is on PATH. If a system-wide
            // ffmpeg is installed on the test machine, this test path becomes a no-op.
            // We still call and assert success in that case (contract still holds).
            if (new FFmpegService().IsFFmpegAvailable(temp.Path))
            {
                bool ok = await new FFmpegService().EnsureFFmpegAvailableAsync(temp.Path);
                Assert.True(ok);
                return;
            }

            byte[] zipBytes = BuildFakeZip("ffmpeg-release-essentials/bin/" + ExeName, "fake binary content");
            string expectedHash = Convert.ToHexString(SHA256.HashData(zipBytes));

            var handler = new StubHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                if (req.RequestUri!.AbsoluteUri.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                {
                    response.Content = new StringContent(expectedHash);
                }
                else
                {
                    response.Content = new ByteArrayContent(zipBytes);
                }
                return response;
            });

            var service = new FFmpegService(new HttpClient(handler));
            bool result = await service.EnsureFFmpegAvailableAsync(temp.Path);

            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(temp.Path, ExeName)));
        }

        [Fact]
        public async Task EnsureFFmpegAvailableAsync_BadChecksum_Fails()
        {
            using var temp = new TempFolder();

            if (new FFmpegService().IsFFmpegAvailable(temp.Path))
            {
                // ffmpeg already available on this system; cannot exercise the bad-checksum path.
                return;
            }

            byte[] zipBytes = BuildFakeZip("bin/" + ExeName, "real content");
            string wrongHash = new string('a', 64);

            var handler = new StubHttpMessageHandler(req =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                if (req.RequestUri!.AbsoluteUri.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                {
                    response.Content = new StringContent(wrongHash);
                }
                else
                {
                    response.Content = new ByteArrayContent(zipBytes);
                }
                return response;
            });

            var service = new FFmpegService(new HttpClient(handler));
            bool result = await service.EnsureFFmpegAvailableAsync(temp.Path);

            // On non-mac, checksum is verified — bad hash should fail.
            // On macOS the checksum URL is null and verification is skipped, so it succeeds.
            if (OperatingSystem.IsMacOS())
            {
                Assert.True(result);
            }
            else
            {
                Assert.False(result);
                Assert.False(File.Exists(Path.Combine(temp.Path, ExeName)));
            }
        }

        [Fact]
        public async Task EnsureFFmpegAvailableAsync_HttpFailure_ReturnsFalse()
        {
            using var temp = new TempFolder();

            if (new FFmpegService().IsFFmpegAvailable(temp.Path))
            {
                return;
            }

            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("oops")
                });

            var service = new FFmpegService(new HttpClient(handler));
            bool result = await service.EnsureFFmpegAvailableAsync(temp.Path);

            Assert.False(result);
        }

        [Fact]
        public async Task EnsureFFmpegAvailableAsync_ReportsProgressPhases()
        {
            using var temp = new TempFolder();
            File.WriteAllText(Path.Combine(temp.Path, ExeName), "stub");

            // ConcurrentBag because Progress<T> callbacks may fire on different
            // thread-pool workers when there's no SynchronizationContext (as in
            // unit tests) and a plain List<T>.Add isn't thread-safe.
            var phases = new System.Collections.Concurrent.ConcurrentBag<DownloadPhase>();
            var progress = new Progress<DownloadProgress>(p => phases.Add(p.Phase));

            var service = new FFmpegService();
            await service.EnsureFFmpegAvailableAsync(temp.Path, progress);

            // Allow posted progress callbacks to flush.
            await Task.Delay(100);

            Assert.Contains(DownloadPhase.Checking, phases);
            Assert.Contains(DownloadPhase.Complete, phases);
        }

        private static byte[] BuildFakeZip(string entryPath, string content)
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(entryPath);
                using var es = entry.Open();
                byte[] data = Encoding.UTF8.GetBytes(content);
                es.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
    }
}
