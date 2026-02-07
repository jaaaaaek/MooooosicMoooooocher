using LinkFormatter.Models;

namespace LinkFormatter.ViewModels
{
    public class DownloadItemViewModel : ViewModelBase
    {
        public DownloadItemViewModel(DownloadItem model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public DownloadItem Model { get; }

        public Guid Id => Model.Id;

        public string Url
        {
            get => Model.Url;
            set
            {
                if (Model.Url != value)
                {
                    Model.Url = value;
                    OnPropertyChanged();
                }
            }
        }

        public DownloadStatus Status
        {
            get => Model.Status;
            set
            {
                if (Model.Status != value)
                {
                    Model.Status = value;
                    OnPropertyChanged();
                }
            }
        }

        public AudioFormat Format
        {
            get => Model.Format;
            set
            {
                if (Model.Format != value)
                {
                    Model.Format = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Progress
        {
            get => Model.Progress;
            set
            {
                if (Math.Abs(Model.Progress - value) > 0.01)
                {
                    Model.Progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => Model.ErrorMessage;
            set
            {
                if (Model.ErrorMessage != value)
                {
                    Model.ErrorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OutputFileName
        {
            get => Model.OutputFileName;
            set
            {
                if (Model.OutputFileName != value)
                {
                    Model.OutputFileName = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
