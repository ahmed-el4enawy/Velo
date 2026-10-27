using Avalonia.Controls;
using Avalonia.Input;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OpenDownloadProgressDialog();
        }
    }
}
