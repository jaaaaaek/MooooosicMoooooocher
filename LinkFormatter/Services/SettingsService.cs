using System.Text.Json;
using System.Text.Json.Serialization;
using LinkFormatter.Models;

namespace LinkFormatter.Services
{
    public class SettingsService : ISettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _settingsPath;

        public SettingsService(string? baseAppDataPath = null)
        {
            string appDataPath = baseAppDataPath ??
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string settingsFolder = Path.Combine(appDataPath, "SoundCloudDownloader");
            _settingsPath = Path.Combine(settingsFolder, "appsettings.json");
        }

        public string SettingsPath => _settingsPath;

        public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return CreateDefaults();
                }

                await using var stream = File.OpenRead(_settingsPath);
                var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);

                return Normalize(settings ?? CreateDefaults());
            }
            catch
            {
                return CreateDefaults();
            }
        }

        public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            string? settingsFolder = Path.GetDirectoryName(_settingsPath);
            if (string.IsNullOrWhiteSpace(settingsFolder))
            {
                throw new InvalidOperationException("Settings folder could not be determined.");
            }

            Directory.CreateDirectory(settingsFolder);

            string tempPath = _settingsPath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, _settingsPath, true);
        }

        private static AppSettings CreateDefaults()
        {
            return Normalize(new AppSettings());
        }

        private static AppSettings Normalize(AppSettings settings)
        {
            if (settings.DownloadedUrls == null)
            {
                settings.DownloadedUrls = new List<string>();
            }

            if (string.IsNullOrWhiteSpace(settings.OutputFolder))
            {
                settings.OutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            }

            if (settings.WindowWidth <= 0)
            {
                settings.WindowWidth = 1200;
            }

            if (settings.WindowHeight <= 0)
            {
                settings.WindowHeight = 800;
            }

            return settings;
        }
    }
}
