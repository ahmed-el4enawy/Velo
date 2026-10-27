using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Velo.Desktop.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
