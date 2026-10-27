namespace Velo.DownloadEngine;

/// <summary>
/// Controls aggregate bandwidth consumption across all active segments (PRD v2.0 FR-15).
/// </summary>
public interface ISpeedThrottler
{
    long? SpeedLimitBytesPerSecond { get; set; }
    ValueTask ThrottleAsync(int bytesRequested, CancellationToken cancellationToken = default);
}
