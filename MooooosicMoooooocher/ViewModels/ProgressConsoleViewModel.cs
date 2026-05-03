using System.Collections.ObjectModel;

namespace MooooosicMoooooocher.ViewModels
{
    public class ProgressConsoleViewModel : ViewModelBase
    {
        private int _maxLines = 500;
        private string _content = string.Empty;
        private bool _isExpanded;

        // Tracks the location of the most recent "live" line (the one most recently
        // written via UpdateOrAppendLine). We track by INDEX rather than "last
        // position" so unrelated AppendLine calls between updates don't push the
        // live line off the tail. Cleared/shifted by TrimLines and Clear.
        private int? _liveLineIndex;
        private string? _liveLineKey;

        public ProgressConsoleViewModel()
        {
            ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        }

        public RelayCommand ToggleExpandCommand { get; }
        public ObservableCollection<string> Lines { get; } = new();

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public int MaxLines
        {
            get => _maxLines;
            set
            {
                if (SetProperty(ref _maxLines, value))
                {
                    TrimLines();
                }
            }
        }

        public void AppendLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            Lines.Add(line);
            // Intentionally don't touch _liveLineIndex - the live line stays at
            // its original index even when other lines append below it. This way
            // a periodic counter stays anchored while interleaved status messages
            // accumulate underneath.
            TrimLines();
            UpdateContent();
        }

        /// <summary>
        /// In-place line update keyed by <paramref name="key"/>. The first call with
        /// a given key appends a new line and tags its index. Subsequent calls with
        /// the SAME key replace the line at that tracked index (regardless of what
        /// other content has appended after it). Calls with a DIFFERENT key append
        /// a new line at the bottom and re-target tracking to that new line.
        /// </summary>
        public void UpdateOrAppendLine(string line, string key)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (_liveLineKey == key
                && _liveLineIndex.HasValue
                && _liveLineIndex.Value >= 0
                && _liveLineIndex.Value < Lines.Count)
            {
                Lines[_liveLineIndex.Value] = line;
                UpdateContent();
                return;
            }

            Lines.Add(line);
            _liveLineIndex = Lines.Count - 1;
            _liveLineKey = key;
            TrimLines();
            UpdateContent();
        }

        public void Clear()
        {
            Lines.Clear();
            _liveLineIndex = null;
            _liveLineKey = null;
            Content = string.Empty;
        }

        public string Content
        {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        private void TrimLines()
        {
            int removed = 0;
            while (Lines.Count > MaxLines)
            {
                Lines.RemoveAt(0);
                removed++;
            }

            if (removed > 0 && _liveLineIndex.HasValue)
            {
                int shifted = _liveLineIndex.Value - removed;
                if (shifted < 0)
                {
                    // Live line scrolled out of the buffer entirely; drop tracking.
                    _liveLineIndex = null;
                    _liveLineKey = null;
                }
                else
                {
                    _liveLineIndex = shifted;
                }
            }

            UpdateContent();
        }

        private void UpdateContent()
        {
            _content = Lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, Lines);
            OnPropertyChanged(nameof(Content));
        }
    }
}
