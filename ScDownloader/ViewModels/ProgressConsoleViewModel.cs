using System.Collections.ObjectModel;

namespace ScDownloader.ViewModels
{
    public class ProgressConsoleViewModel : ViewModelBase
    {
        private int _maxLines = 500;
        private string _content = string.Empty;
        private bool _isExpanded;

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
            TrimLines();
            _content = string.Join(Environment.NewLine, Lines);
            OnPropertyChanged(nameof(Content));
        }

        public void Clear()
        {
            Lines.Clear();
            Content = string.Empty;
        }

        public string Content
        {
            get => _content;
            private set => SetProperty(ref _content, value);
        }

        private void TrimLines()
        {
            while (Lines.Count > MaxLines)
            {
                Lines.RemoveAt(0);
            }

            if (Lines.Count != 0)
            {
                _content = string.Join(Environment.NewLine, Lines);
                OnPropertyChanged(nameof(Content));
            }
            else if (!string.IsNullOrEmpty(_content))
            {
                _content = string.Empty;
                OnPropertyChanged(nameof(Content));
            }
        }
    }
}
