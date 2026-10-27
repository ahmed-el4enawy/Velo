using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Velo.Application;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class MoveRenameWindow : Window
{
    private readonly DownloadItemViewModel _item;
    private readonly IDownloadService _downloadService;

    public MoveRenameWindow(DownloadItemViewModel item, IDownloadService downloadService)
    {
        InitializeComponent();
        _item = item;
        _downloadService = downloadService;

        FileNameBox.Text = _item.Job.DisplayName;
        string? currentFolder = Path.GetDirectoryName(_item.Job.TargetPath);
        FolderPathBox.Text = !string.IsNullOrWhiteSpace(currentFolder) ? currentFolder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    // Designer support
    public MoveRenameWindow() : this(null!, null!)
    {
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Destination Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            FolderPathBox.Text = folders[0].Path.LocalPath;
        }
    }

    private async void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        string newName = (FileNameBox.Text ?? string.Empty).Trim();
        string newFolder = (FolderPathBox.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(newFolder))
        {
            return;
        }

        string oldTarget = _item.Job.TargetPath;
        string oldTemp = _item.Job.TempPath;
        string newTarget = Path.Combine(newFolder, newName);
        string newTemp = newTarget + ".velo.part";

        try
        {
            if (!Directory.Exists(newFolder))
            {
                Directory.CreateDirectory(newFolder);
            }

            // Move target file if exists
            if (File.Exists(oldTarget) && !string.Equals(oldTarget, newTarget, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(oldTarget, newTarget, overwrite: true);
            }

            // Move temp file if exists
            if (File.Exists(oldTemp) && !string.Equals(oldTemp, newTemp, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(oldTemp, newTemp, overwrite: true);
            }

            _item.Job.DisplayName = newName;
            _item.Job.TargetPath = newTarget;
            _item.Job.TempPath = newTemp;
            _item.DisplayName = newName;

            await _downloadService.UpdateJobAsync(_item.Job);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to move/rename file: {ex.Message}");
        }

        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
