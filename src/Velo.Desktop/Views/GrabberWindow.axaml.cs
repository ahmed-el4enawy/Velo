using Avalonia.Controls;
using Avalonia.Interactivity;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class GrabberWindow : Window
{
    private readonly MainWindowViewModel? _mainVm;

    public GrabberWindow(MainWindowViewModel? mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
    }

    public GrabberWindow() : this(null)
    {
    }

    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        string url = (UrlBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            StatusMessage.Text = "Please enter a valid website address starting with http:// or https://";
            StatusMessage.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }

        Close();
        _mainVm?.OpenAddDownloadDialog(url);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
