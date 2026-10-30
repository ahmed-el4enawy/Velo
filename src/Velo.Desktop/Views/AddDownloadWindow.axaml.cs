using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class AddDownloadWindow : Window
{
    public AddDownloadWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            Topmost = true;
            Activate();
            BringToForegroundWin32();
        };
    }

    private void BringToForegroundWin32()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var handle = TryGetPlatformHandle()?.Handle;
                if (handle.HasValue && handle.Value != IntPtr.Zero)
                {
                    IntPtr hWnd = handle.Value;
                    IntPtr foreWnd = GetForegroundWindow();
                    uint foreThread = GetWindowThreadProcessId(foreWnd, IntPtr.Zero);
                    uint currentThread = GetCurrentThreadId();

                    if (foreThread != currentThread)
                    {
                        AttachThreadInput(currentThread, foreThread, true);
                    }

                    ShowWindow(hWnd, SW_SHOW);
                    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    SetForegroundWindow(hWnd);

                    if (foreThread != currentThread)
                    {
                        AttachThreadInput(currentThread, foreThread, false);
                    }
                }
            }
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const int SW_SHOW = 5;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public AddDownloadWindow(AddDownloadWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.DownloadCreated += _ => Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddDownloadWindowViewModel vm) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File As",
            SuggestedFileName = vm.FileName
        });

        if (file != null)
        {
            string localPath = file.Path.LocalPath;
            vm.SaveAsPath = localPath;
            vm.FileName = System.IO.Path.GetFileName(localPath);
            string? dir = System.IO.Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir))
            {
                vm.DestinationFolder = dir;
            }
        }
    }
}
