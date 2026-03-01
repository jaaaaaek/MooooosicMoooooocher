namespace ScDownloader.Models
{
    public class AppSettings
    {
        public string OutputFolder { get; set; } = string.Empty;
        public string SoundCloudToken { get; set; } = string.Empty;
        public AudioFormat PreferredFormat { get; set; } = AudioFormat.MP3;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public List<string> DownloadedFiles { get; set; } = new List<string>();
        public bool IsFirstRun { get; set; } = true;
    }
}
