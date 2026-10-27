using Velo.Domain;

namespace Velo.Application.Services;

public sealed class ClipboardMonitorService : IClipboardMonitor
{
    private string? _lastProcessedText;

    public bool IsEnabled { get; set; } = false; // Default opt-in per PRD FR-17

    public event Action<string, CategoryType>? DownloadableUrlDetected;

    public bool TryInspectText(string? text, out string validUrl, out CategoryType category)
    {
        validUrl = string.Empty;
        category = CategoryType.Others;

        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        if (trimmed.Equals(_lastProcessedText, StringComparison.Ordinal))
        {
            return false; // Avoid duplicate triggers on same copied text
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        string fileName = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
        {
            return false; // Bare domains or pathless URLs are ignored by passive sniffer
        }

        category = FileCategoryClassifier.Classify(fileName);
        _lastProcessedText = trimmed;
        validUrl = trimmed;

        if (IsEnabled)
        {
            DownloadableUrlDetected?.Invoke(validUrl, category);
        }

        return true;
    }
}
