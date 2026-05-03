using MooooosicMoooooocher.Models;
using MooooosicMoooooocher.Services;
using MooooosicMoooooocher.Tests.Helpers;
using Xunit;

namespace MooooosicMoooooocher.Tests
{
    public class DownloadServiceTests
    {
        [Fact]
        public async Task DownloadAsync_NullItem_Throws()
        {
            var service = new DownloadService();
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                service.DownloadAsync(null!, "C:\\out", null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DownloadAsync_EmptyOutputFolder_Fails(string? outputFolder)
        {
            var service = new DownloadService();
            var item = new DownloadItem
            {
                Url = "https://soundcloud.com/user/track",
                Format = AudioFormat.MP3
            };

            var result = await service.DownloadAsync(item, outputFolder!, null);

            Assert.False(result.Success);
            Assert.Equal("Output folder is not set.", result.ErrorMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DownloadAsync_WavWithoutAuthToken_Fails(string? authToken)
        {
            using var temp = new TempFolder();
            var service = new DownloadService();
            var item = new DownloadItem
            {
                Url = "https://soundcloud.com/user/track",
                Format = AudioFormat.WAV
            };

            var result = await service.DownloadAsync(item, temp.Path, authToken);

            Assert.False(result.Success);
            Assert.Equal("Auth token is required for WAV downloads.", result.ErrorMessage);
        }

        [Fact]
        public async Task DownloadAsync_MissingYtDlpBinary_Fails()
        {
            // The test runner's AppContext.BaseDirectory does not have yt-dlp.exe
            // (the main project doesn't mark it as Content, so it isn't copied to bin/).
            using var temp = new TempFolder();
            var service = new DownloadService();
            var item = new DownloadItem
            {
                Url = "https://soundcloud.com/user/track",
                Format = AudioFormat.MP3
            };

            var result = await service.DownloadAsync(item, temp.Path, null);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("was not found", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void IsDownloadActive_UnknownId_ReturnsFalse()
        {
            var service = new DownloadService();
            Assert.False(service.IsDownloadActive(Guid.NewGuid()));
        }

        [Fact]
        public void CancelDownload_UnknownId_ReturnsFalse()
        {
            var service = new DownloadService();
            Assert.False(service.CancelDownload(Guid.NewGuid()));
        }

        [Fact]
        public async Task ResolvePlaylistAsync_MissingYtDlpBinary_ReturnsEmpty()
        {
            var service = new DownloadService();
            var result = await service.ResolvePlaylistAsync(
                "https://soundcloud.com/user/likes", null);

            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
