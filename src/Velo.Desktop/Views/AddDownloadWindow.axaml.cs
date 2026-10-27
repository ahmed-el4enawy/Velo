using Avalonia.Controls;
using Avalonia.Interactivity;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class AddDownloadWindow : Window
{
    public AddDownloadWindow()
    {
        InitializeComponent();
    }

    public AddDownloadWindow(AddDownloadWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.DownloadCreated += _ => Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
