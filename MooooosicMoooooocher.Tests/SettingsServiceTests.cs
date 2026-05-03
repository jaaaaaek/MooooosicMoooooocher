using MooooosicMoooooocher.Models;
using MooooosicMoooooocher.Services;
using MooooosicMoooooocher.Tests.Helpers;
using Xunit;

namespace MooooosicMoooooocher.Tests
{
    public class SettingsServiceTests
    {
        [Fact]
        public async Task LoadAsync_NoFile_ReturnsDefaults()
        {
            using var temp = new TempFolder();
            var service = new SettingsService(temp.Path);

            var settings = await service.LoadAsync();

            Assert.NotNull(settings);
            Assert.Equal(AudioFormat.MP3, settings.PreferredFormat);
            Assert.Equal(1200, settings.WindowWidth);
            Assert.Equal(800, settings.WindowHeight);
            Assert.True(settings.IsFirstRun);
            Assert.NotNull(settings.DownloadedFiles);
            Assert.Empty(settings.DownloadedFiles);
            Assert.Equal(string.Empty, settings.AuthToken);
            Assert.Equal(
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                settings.OutputFolder);
        }

        [Fact]
        public async Task SaveAsync_ThenLoadAsync_RoundTripsAllProperties()
        {
            using var temp = new TempFolder();
            var service = new SettingsService(temp.Path);

            var input = new AppSettings
            {
                OutputFolder = @"C:\Music\Out",
                AuthToken = "secret-token-123",
                PreferredFormat = AudioFormat.WAV,
                WindowWidth = 1600,
                WindowHeight = 1000,
                IsFirstRun = false,
                DownloadedFiles = new List<string>
                {
                    "https://soundcloud.com/a/b",
                    "https://soundcloud.com/c/d"
                }
            };

            await service.SaveAsync(input);
            var loaded = await service.LoadAsync();

            Assert.Equal(input.OutputFolder, loaded.OutputFolder);
            Assert.Equal(input.AuthToken, loaded.AuthToken);
            Assert.Equal(input.PreferredFormat, loaded.PreferredFormat);
            Assert.Equal(input.WindowWidth, loaded.WindowWidth);
            Assert.Equal(input.WindowHeight, loaded.WindowHeight);
            Assert.Equal(input.IsFirstRun, loaded.IsFirstRun);
            Assert.Equal(input.DownloadedFiles, loaded.DownloadedFiles);
        }

        [Fact]
        public async Task LoadAsync_MalformedJson_ReturnsDefaults()
        {
            using var temp = new TempFolder();
            string settingsFolder = Path.Combine(temp.Path, "MooooosicMoooooocher");
            Directory.CreateDirectory(settingsFolder);
            await File.WriteAllTextAsync(
                Path.Combine(settingsFolder, "appsettings.json"),
                "this is { not valid json");

            var service = new SettingsService(temp.Path);
            var settings = await service.LoadAsync();

            Assert.NotNull(settings);
            Assert.Equal(AudioFormat.MP3, settings.PreferredFormat);
            Assert.True(settings.IsFirstRun);
        }

        [Fact]
        public async Task SaveAsync_CreatesParentDirectoryIfMissing()
        {
            using var temp = new TempFolder();
            var service = new SettingsService(temp.Path);

            string expectedFolder = Path.Combine(temp.Path, "MooooosicMoooooocher");
            Assert.False(Directory.Exists(expectedFolder));

            await service.SaveAsync(new AppSettings());

            Assert.True(Directory.Exists(expectedFolder));
            Assert.True(File.Exists(service.SettingsPath));
        }

        [Fact]
        public async Task SaveAsync_LeavesNoTempFile()
        {
            using var temp = new TempFolder();
            var service = new SettingsService(temp.Path);

            await service.SaveAsync(new AppSettings());

            string tempPath = service.SettingsPath + ".tmp";
            Assert.False(File.Exists(tempPath));
        }

        [Fact]
        public async Task SaveAsync_OverwritesExistingFile()
        {
            using var temp = new TempFolder();
            var service = new SettingsService(temp.Path);

            await service.SaveAsync(new AppSettings { AuthToken = "first" });
            await service.SaveAsync(new AppSettings { AuthToken = "second" });

            var loaded = await service.LoadAsync();
            Assert.Equal("second", loaded.AuthToken);
        }

        [Fact]
        public async Task LoadAsync_ZeroWidth_NormalizesToDefault()
        {
            using var temp = new TempFolder();
            string settingsFolder = Path.Combine(temp.Path, "MooooosicMoooooocher");
            Directory.CreateDirectory(settingsFolder);
            await File.WriteAllTextAsync(
                Path.Combine(settingsFolder, "appsettings.json"),
                "{ \"WindowWidth\": 0, \"WindowHeight\": -50, \"PreferredFormat\": \"MP3\" }");

            var service = new SettingsService(temp.Path);
            var settings = await service.LoadAsync();

            Assert.Equal(1200, settings.WindowWidth);
            Assert.Equal(800, settings.WindowHeight);
        }

        [Fact]
        public async Task LoadAsync_EmptyOutputFolder_NormalizesToMyMusic()
        {
            using var temp = new TempFolder();
            string settingsFolder = Path.Combine(temp.Path, "MooooosicMoooooocher");
            Directory.CreateDirectory(settingsFolder);
            await File.WriteAllTextAsync(
                Path.Combine(settingsFolder, "appsettings.json"),
                "{ \"OutputFolder\": \"\" }");

            var service = new SettingsService(temp.Path);
            var settings = await service.LoadAsync();

            Assert.Equal(
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                settings.OutputFolder);
        }

        [Fact]
        public async Task LoadAsync_NullDownloadedFiles_NormalizesToEmptyList()
        {
            using var temp = new TempFolder();
            string settingsFolder = Path.Combine(temp.Path, "MooooosicMoooooocher");
            Directory.CreateDirectory(settingsFolder);
            await File.WriteAllTextAsync(
                Path.Combine(settingsFolder, "appsettings.json"),
                "{ \"DownloadedFiles\": null }");

            var service = new SettingsService(temp.Path);
            var settings = await service.LoadAsync();

            Assert.NotNull(settings.DownloadedFiles);
            Assert.Empty(settings.DownloadedFiles);
        }

        [Fact]
        public void SettingsPath_PointsToExpectedSubfolder()
        {
            using var temp = new TempFolder();
            var service = new SettingsService(temp.Path);

            string expected = Path.Combine(temp.Path, "MooooosicMoooooocher", "appsettings.json");
            Assert.Equal(expected, service.SettingsPath);
        }

        [Fact]
        public async Task SaveAsync_PersistsAudioFormatAsString()
        {
            using var temp = new TempFolder();
            var service = new SettingsService(temp.Path);

            await service.SaveAsync(new AppSettings { PreferredFormat = AudioFormat.WAV });
            string content = await File.ReadAllTextAsync(service.SettingsPath);

            Assert.Contains("\"WAV\"", content);
        }
    }
}
