namespace Velo.MediaIntegration;

/// <summary>
/// Strictly optional boundary adapter for external media extractors (yt-dlp/FFmpeg) per PRD Section 10.
/// Disabled by default and decoupled from the core download engine.
/// </summary>
public interface IMediaExtractor
{
    bool IsAvailable { get; }
    Task<string?> ExtractDirectStreamUrlAsync(string webPageUrl, CancellationToken cancellationToken = default);
}
