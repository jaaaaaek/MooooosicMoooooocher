using System.Collections.ObjectModel;

namespace LinkFormatter.ViewModels
{
    public class ProgressConsoleViewModel : ViewModelBase
    {
        private int _maxLines = 500;

        public ObservableCollection<string> Lines { get; } = new();

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
        }

        public void Clear()
        {
            Lines.Clear();
        }

        private void TrimLines()
        {
            while (Lines.Count > MaxLines)
            {
                Lines.RemoveAt(0);
            }
        }
    }
}
