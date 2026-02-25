using System.Collections.ObjectModel;
using LinkFormatter.Models;

namespace LinkFormatter.ViewModels
{
    public class DownloadQueueViewModel : ViewModelBase
    {
        public DownloadQueueViewModel()
        {
            RemoveCommand = new RelayCommand<DownloadItemViewModel>(Remove);
            ClearCompletedCommand = new RelayCommand(ClearCompleted);
            ClearAllCommand = new RelayCommand(ClearAll);
            MoveUpCommand = new RelayCommand<DownloadItemViewModel>(MoveUp);
            MoveDownCommand = new RelayCommand<DownloadItemViewModel>(MoveDown);
        }

        public ObservableCollection<DownloadItemViewModel> Items { get; } = new();

        public RelayCommand<DownloadItemViewModel> RemoveCommand { get; }
        public RelayCommand ClearCompletedCommand { get; }
        public RelayCommand ClearAllCommand { get; }
        public RelayCommand<DownloadItemViewModel> MoveUpCommand { get; }
        public RelayCommand<DownloadItemViewModel> MoveDownCommand { get; }

        public DownloadItemViewModel Add(DownloadItem item)
        {
            var viewModel = new DownloadItemViewModel(item);
            Items.Add(viewModel);
            return viewModel;
        }

        public DownloadItemViewModel? GetNextPending()
        {
            return Items.FirstOrDefault(item => item.Status == DownloadStatus.Pending);
        }

        private void Remove(DownloadItemViewModel? item)
        {
            if (item == null)
            {
                return;
            }

            Items.Remove(item);
        }

        private void ClearCompleted()
        {
            var toRemove = Items.Where(item => item.Status is DownloadStatus.Completed or DownloadStatus.Skipped).ToList();
            foreach (var item in toRemove)
            {
                Items.Remove(item);
            }
        }

        private void ClearAll()
        {
            Items.Clear();
        }

        private void MoveUp(DownloadItemViewModel? item)
        {
            if (item == null)
            {
                return;
            }

            int index = Items.IndexOf(item);
            if (index > 0)
            {
                Items.Move(index, index - 1);
            }
        }

        private void MoveDown(DownloadItemViewModel? item)
        {
            if (item == null)
            {
                return;
            }

            int index = Items.IndexOf(item);
            if (index >= 0 && index < Items.Count - 1)
            {
                Items.Move(index, index + 1);
            }
        }
    }
}
