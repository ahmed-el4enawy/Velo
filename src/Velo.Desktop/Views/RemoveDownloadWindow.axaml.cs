using Avalonia.Controls;
using Avalonia.Interactivity;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class RemoveDownloadWindow : Window
{
    private readonly DownloadItemViewModel? _item;
    private readonly IReadOnlyList<DownloadItemViewModel>? _items;
    private readonly Action<bool>? _onConfirmed;

    public RemoveDownloadWindow(DownloadItemViewModel item, Action<bool>? onConfirmed = null)
    {
        InitializeComponent();
        _item = item;
        _onConfirmed = onConfirmed;

        FileNameText.Text = $"File: {_item?.DisplayName}";
    }

    public RemoveDownloadWindow(IReadOnlyList<DownloadItemViewModel> items, Action<bool>? onConfirmed = null)
    {
        InitializeComponent();
        _items = items;
        _onConfirmed = onConfirmed;

        if (items.Count == 1)
        {
            _item = items[0];
            FileNameText.Text = $"File: {_item.DisplayName}";
        }
        else
        {
            Title = $"Remove {items.Count} Downloads";
            PromptText.Text = $"Are you sure you want to remove these {items.Count} downloads from the list?";
            FileNameText.Text = $"{items.Count} items selected";
        }
    }

    public RemoveDownloadWindow() : this((DownloadItemViewModel)null!, null)
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
