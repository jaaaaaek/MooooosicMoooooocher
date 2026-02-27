using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using ScDownloader.Services;

namespace ScDownloader.Views
{
    public partial class DownloadDialog : Window
    {
        // Speed/ETA tracking
        private DateTime _downloadStartTime;
        private long _lastBytes;
        private DateTime _lastSpeedUpdate;
        private double _currentSpeed; // bytes per second

        private string _targetFolder = "";

        public DownloadDialog()
        {
            InitializeComponent();
        }

        public void SetTargetFolder(string folder)
        {
            _targetFolder = folder;
            DestinationText.Text = folder;
        }


        public void UpdateProgress(DownloadProgress progress)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText.Text = progress.Phase switch
                {
                    DownloadPhase.Checking => "Saving: ffmpeg.exe (checking...)",
                    DownloadPhase.Downloading => progress.TotalBytes.HasValue
                        ? $"Saving: ffmpeg.exe ({FormatBytes(progress.BytesDownloaded)} of {FormatBytes(progress.TotalBytes.Value)})"
                        : $"Saving: ffmpeg.exe ({FormatBytes(progress.BytesDownloaded)})",
                    DownloadPhase.Extracting => "Saving: ffmpeg.exe (extracting...)",
                    DownloadPhase.Complete => "Saving: ffmpeg.exe (complete!)",
                    DownloadPhase.Failed => "Download failed.",
                    _ => progress.Message
                };

                if (progress.Phase == DownloadPhase.Downloading)
                {
                    // Initialize timing on first download update
                    if (_downloadStartTime == default)
                    {
                        _downloadStartTime = DateTime.UtcNow;
                        _lastSpeedUpdate = DateTime.UtcNow;
                        _lastBytes = 0;
                    }

                    // Calculate speed (update every 500ms to avoid jitter)
                    var now = DateTime.UtcNow;
                    var timeSinceLastUpdate = (now - _lastSpeedUpdate).TotalSeconds;
                    if (timeSinceLastUpdate >= 0.5)
                    {
                        var bytesDelta = progress.BytesDownloaded - _lastBytes;
                        _currentSpeed = bytesDelta / timeSinceLastUpdate;
                        _lastBytes = progress.BytesDownloaded;
                        _lastSpeedUpdate = now;
                    }

                    // Transfer rate
                    SpeedText.Text = _currentSpeed > 0 ? $"{FormatBytes((long)_currentSpeed)}/Sec" : "";

                    // Estimated time left
                    if (_currentSpeed > 0 && progress.TotalBytes.HasValue)
                    {
                        var remainingBytes = progress.TotalBytes.Value - progress.BytesDownloaded;
                        var secondsLeft = remainingBytes / _currentSpeed;
                        EtaText.Text = FormatTime(secondsLeft);
                    }
                    else
                    {
                        EtaText.Text = "Calculating...";
                    }

                    // Update chunky progress blocks
                    DrawProgressBlocks(progress.PercentComplete);
                }

                if (progress.Phase == DownloadPhase.Extracting)
                {
                    DrawProgressBlocks(100);
                    SpeedText.Text = "";
                    EtaText.Text = "Almost done...";
                }

                if (progress.Phase == DownloadPhase.Failed)
                {
                    ErrorText.Text = progress.Message;
                    ErrorText.IsVisible = true;
                }
            });
        }

        private void DrawProgressBlocks(double percent)
        {
            ProgressCanvas.Children.Clear();

            var canvasWidth = ProgressCanvas.Bounds.Width;
            var canvasHeight = ProgressCanvas.Bounds.Height;

            if (canvasWidth <= 0 || canvasHeight <= 0) return;

            const double blockWidth = 12;
            const double blockGap = 2;
            const double blockStep = blockWidth + blockGap;

            int totalBlocks = (int)((canvasWidth + blockGap) / blockStep);
            int filledBlocks = (int)(totalBlocks * percent / 100.0);

            for (int i = 0; i < filledBlocks; i++)
            {
                var block = new Rectangle
                {
                    Width = blockWidth,
                    Height = canvasHeight,
                    Fill = new SolidColorBrush(Color.Parse("#D97757")) // ClaudeOrange
                };
                Canvas.SetLeft(block, i * blockStep);
                Canvas.SetTop(block, 0);
                ProgressCanvas.Children.Add(block);
            }
        }

        public void OnDownloadComplete(bool success)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (success)
                {
                    Close(true);
                }
            });
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:F2} {suffixes[suffixIndex]}";
        }

        private static string FormatTime(double totalSeconds)
        {
            if (totalSeconds < 0 || double.IsInfinity(totalSeconds) || double.IsNaN(totalSeconds))
                return "Calculating...";

            var ts = TimeSpan.FromSeconds(totalSeconds);

            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours} hr {ts.Minutes} min";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes} min {ts.Seconds} sec";
            return $"{ts.Seconds} sec";
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
