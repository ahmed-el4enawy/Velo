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
        try
        {
            var downloadService = App.Services?.GetService(typeof(Velo.Application.IDownloadService)) as Velo.Application.IDownloadService;
            downloadService?.StopAllDownloadsAsync().GetAwaiter().GetResult();
        }
        catch { }

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

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SelectAll();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Delete)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
            }
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
                    // If the clicked item is already part of the multi-selection, preserve the selection
                    if (!vm.SelectedDownloads.Contains(item))
                    {
                        vm.SelectedDownloads.Clear();
                        vm.SelectedDownloads.Add(item);
                        vm.SelectedDownload = item;
                    }
                }
            }
        }
    }
}
