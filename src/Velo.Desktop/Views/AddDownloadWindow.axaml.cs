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
