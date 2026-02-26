using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using LinkFormatter.ViewModels;

namespace LinkFormatter.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContextChanged += (_, _) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.PropertyChanged += OnViewModelPropertyChanged;
                    UpdateConsoleLayout(vm.IsConsoleExpanded);
                }
            };
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsConsoleExpanded) &&
                DataContext is MainWindowViewModel vm)
            {
                UpdateConsoleLayout(vm.IsConsoleExpanded);
            }
        }

        private void UpdateConsoleLayout(bool expanded)
        {
            if (ConsoleView is null) return;

            if (expanded)
            {
                Grid.SetRow(ConsoleView, 0);
                Grid.SetRowSpan(ConsoleView, 3);
            }
            else
            {
                Grid.SetRow(ConsoleView, 2);
                Grid.SetRowSpan(ConsoleView, 1);
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (FocusManager?.GetFocusedElement() is TextBox && e.Source is not TextBox)
            {
                FocusManager?.ClearFocus();
            }
        }
    }
}
