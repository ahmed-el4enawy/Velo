using Avalonia.Controls;
using Avalonia.Interactivity;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class RemoveDownloadWindow : Window
{
    private readonly DownloadItemViewModel _item;
    private readonly Action<bool>? _onConfirmed;

    public RemoveDownloadWindow(DownloadItemViewModel item, Action<bool>? onConfirmed = null)
    {
        InitializeComponent();
        _item = item;
        _onConfirmed = onConfirmed;

        FileNameText.Text = $"File: {_item.DisplayName}";
    }

    public RemoveDownloadWindow() : this(null!, null)
    {
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        bool deleteFiles = DeleteFilesCheck.IsChecked == true;
        _onConfirmed?.Invoke(deleteFiles);
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
