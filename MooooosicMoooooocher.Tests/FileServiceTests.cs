using MooooosicMoooooocher.Services;
using MooooosicMoooooocher.Tests.Helpers;
using Xunit;

namespace MooooosicMoooooocher.Tests
{
    public class FileServiceTests
    {
        private readonly FileService _service = new();

        [Fact]
        public void GetDownloadedFiles_NonExistentFolder_ReturnsEmpty()
        {
            string nonExistent = Path.Combine(Path.GetTempPath(), "mooo-not-here-" + Guid.NewGuid().ToString("N"));
            var files = _service.GetDownloadedFiles(nonExistent);
            Assert.Empty(files);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetDownloadedFiles_EmptyOrNullPath_ReturnsEmpty(string? path)
        {
            var files = _service.GetDownloadedFiles(path!);
            Assert.Empty(files);
        }

        [Fact]
        public void GetDownloadedFiles_EmptyFolder_ReturnsEmpty()
        {
            using var temp = new TempFolder();
            var files = _service.GetDownloadedFiles(temp.Path);
            Assert.Empty(files);
        }

        [Fact]
        public void GetDownloadedFiles_WithFiles_ReturnsAllSortedCaseInsensitively()
        {
            using var temp = new TempFolder();
            File.WriteAllText(Path.Combine(temp.Path, "Charlie.mp3"), "");
            File.WriteAllText(Path.Combine(temp.Path, "alpha.mp3"), "");
            File.WriteAllText(Path.Combine(temp.Path, "Bravo.mp3"), "");

            var files = _service.GetDownloadedFiles(temp.Path);

            Assert.Equal(3, files.Count);
            Assert.EndsWith("alpha.mp3", files[0]);
            Assert.EndsWith("Bravo.mp3", files[1]);
            Assert.EndsWith("Charlie.mp3", files[2]);
        }

        [Fact]
        public void GetDownloadedFiles_DoesNotIncludeSubdirectories()
        {
            using var temp = new TempFolder();
            string subdir = Path.Combine(temp.Path, "subfolder");
            Directory.CreateDirectory(subdir);
            File.WriteAllText(Path.Combine(subdir, "nested.mp3"), "");
            File.WriteAllText(Path.Combine(temp.Path, "top.mp3"), "");

            var files = _service.GetDownloadedFiles(temp.Path);

            Assert.Single(files);
            Assert.EndsWith("top.mp3", files[0]);
        }

        [Fact]
        public void OpenFileLocation_NonExistentFile_DoesNotThrow()
        {
            string nonExistent = Path.Combine(Path.GetTempPath(), "mooo-fake-" + Guid.NewGuid().ToString("N") + ".mp3");
            var ex = Record.Exception(() => _service.OpenFileLocation(nonExistent));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void OpenFileLocation_EmptyPath_DoesNotThrow(string? path)
        {
            var ex = Record.Exception(() => _service.OpenFileLocation(path!));
            Assert.Null(ex);
        }

        [Fact]
        public void OpenFolder_NonExistentFolder_DoesNotThrow()
        {
            string nonExistent = Path.Combine(Path.GetTempPath(), "mooo-fake-folder-" + Guid.NewGuid().ToString("N"));
            var ex = Record.Exception(() => _service.OpenFolder(nonExistent));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void OpenFolder_EmptyPath_DoesNotThrow(string? path)
        {
            var ex = Record.Exception(() => _service.OpenFolder(path!));
            Assert.Null(ex);
        }
    }
}
