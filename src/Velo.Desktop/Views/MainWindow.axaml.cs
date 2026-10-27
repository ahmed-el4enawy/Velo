using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class MainWindow : Window
{
    private bool _isForceExit;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnMainWindowClosing;
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_isForceExit)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void ForceExit()
    {
        _isForceExit = true;
        Close();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            Velo.Desktop.Services.IpcServer.Stop();
            desktop.Shutdown();
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedDownload != null)
        {
            vm.ExecuteDoubleClickAction(vm.SelectedDownload);
        }
    }

    private void OnListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Visual);
        if (point.Properties.IsRightButtonPressed)
        {
            if (e.Source is Visual visual)
            {
                var listBoxItem = visual.FindAncestorOfType<ListBoxItem>();
                if (listBoxItem?.DataContext is DownloadItemViewModel item && DataContext is MainWindowViewModel vm)
                {
                    vm.SelectedDownload = item;
                }
            }
        }
    }
}
