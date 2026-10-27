using Avalonia.Controls;
using Avalonia.Interactivity;
using Velo.Desktop.ViewModels;

namespace Velo.Desktop.Views;

public partial class PropertiesWindow : Window
{
    private readonly DownloadItemViewModel _item;

    public PropertiesWindow(DownloadItemViewModel item)
    {
        InitializeComponent();
        _item = item;

        Title = $"Properties — {_item.DisplayName}";
        FileNameText.Text = _item.DisplayName;
        FileIconText.Text = _item.FileIconEmoji;
        FileSizeStatusText.Text = $"{_item.FormattedSizeOnly} — {_item.StatusDisplay}";

        UrlBox.Text = _item.Job.EffectiveUrl;
        HostText.Text = _item.Host;
        StatusText.Text = _item.StatusDisplay;
        SavePathBox.Text = _item.Job.TargetPath;
        DownloadedSizeText.Text = _item.SizeInfo;
        ResumeSupportText.Text = _item.RangeSupportText;
        EtagText.Text = _item.EtagText;
        CategoryText.Text = _item.CategoryText;
        CreatedAtText.Text = _item.CreatedAtText;
        FinishedAtText.Text = _item.FinishedAtText;
    }

    public PropertiesWindow() : this(null!)
    {
    }

    private void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        _item.OpenFileCommand.Execute(null);
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        _item.OpenFolderCommand.Execute(null);
    }

    private async void OnCopyUrlClick(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null && !string.IsNullOrEmpty(UrlBox.Text))
        {
            await Clipboard.SetTextAsync(UrlBox.Text);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
