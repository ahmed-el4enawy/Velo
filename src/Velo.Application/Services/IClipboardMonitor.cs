using Velo.Domain;

namespace Velo.Application.Services;

/// <summary>
/// Passive, opt-in clipboard URL detection service (PRD Section 3 FR-17).
/// Detects downloadable URLs copied to the OS clipboard without auto-launching requests.
/// </summary>
public interface IClipboardMonitor
{
    bool IsEnabled { get; set; }
    event Action<string, CategoryType>? DownloadableUrlDetected;
    bool TryInspectText(string? text, out string validUrl, out CategoryType category);
}
