namespace ScDownloader.Models
{
    public class DownloadItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Url { get; set; } = string.Empty;
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
        public AudioFormat Format { get; set; } = AudioFormat.MP3;
        public double Progress { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string OutputFileName { get; set; } = string.Empty;
    }
}
