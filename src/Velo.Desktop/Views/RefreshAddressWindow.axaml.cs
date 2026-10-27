using Avalonia.Controls;
using Avalonia.Interactivity;
using Velo.Application;
using Velo.Desktop.ViewModels;
using Velo.DownloadEngine;

namespace Velo.Desktop.Views;

public partial class RefreshAddressWindow : Window
{
    private readonly DownloadItemViewModel _item;
    private readonly IDownloadService _downloadService;
    private readonly IHttpProbe? _probe;

    public RefreshAddressWindow(DownloadItemViewModel item, IDownloadService downloadService, IHttpProbe? probe = null)
    {
        InitializeComponent();
        _item = item;
        _downloadService = downloadService;
        _probe = probe;

        CurrentUrlBox.Text = _item.Job.OriginalUrl;
        NewUrlBox.Text = _item.Job.OriginalUrl;
    }

    public RefreshAddressWindow() : this(null!, null!, null)
    {
    }

    private async void OnUpdateClick(object? sender, RoutedEventArgs e)
    {
        string newUrl = (NewUrlBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newUrl) || !Uri.TryCreate(newUrl, UriKind.Absolute, out var uri))
        {
            StatusMessage.Text = "Please enter a valid HTTP or HTTPS URL.";
            StatusMessage.Foreground = Avalonia.Media.Brushes.OrangeRed;
            return;
        }

        StatusMessage.Text = "Verifying link...";
        StatusMessage.Foreground = Avalonia.Media.Brushes.SkyBlue;

        try
        {
            if (_probe != null)
            {
                var result = await _probe.ProbeUrlAsync(newUrl);
                if (result.IsSuccess)
                {
                    _item.Job.OriginalUrl = newUrl;
                    _item.Job.EffectiveUrl = result.EffectiveUrl;
                    _item.Job.Host = uri.Host;
                    if (!string.IsNullOrWhiteSpace(result.ETag))
                    {
                        _item.Job.StrongETag = result.ETag;
                    }
                    if (!string.IsNullOrWhiteSpace(result.LastModified))
                    {
                        _item.Job.LastModified = result.LastModified;
                    }

                    await _downloadService.UpdateJobAsync(_item.Job);
                    Close();
                    return;
                }
            }

            // Fallback: update without probe if probe offline
            _item.Job.OriginalUrl = newUrl;
            _item.Job.EffectiveUrl = newUrl;
            _item.Job.Host = uri.Host;
            await _downloadService.UpdateJobAsync(_item.Job);
            Close();
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Error: {ex.Message}";
            StatusMessage.Foreground = Avalonia.Media.Brushes.Red;
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
