using System.Diagnostics;

namespace Velo.DownloadEngine;

/// <summary>
/// High-precision token bucket rate limiter enforcing aggregate download speed caps (PRD Section 6 & FR-15).
/// Thread-safe and non-spinning; pauses workers cooperatively without CPU burn.
/// </summary>
public sealed class TokenBucketThrottler : ISpeedThrottler
{
    private readonly Lock _lock = new();
    private double _availableTokens;
    private long _lastRefillTimestamp;
    private long? _speedLimitBytesPerSecond;

    public long? SpeedLimitBytesPerSecond
    {
        get => _speedLimitBytesPerSecond;
        set
        {
            lock (_lock)
            {
                _speedLimitBytesPerSecond = value is > 0 ? value : null;
                _availableTokens = _speedLimitBytesPerSecond ?? 0;
                _lastRefillTimestamp = Stopwatch.GetTimestamp();
            }
        }
    }

    public TokenBucketThrottler(long? speedLimitBytesPerSecond = null)
    {
        SpeedLimitBytesPerSecond = speedLimitBytesPerSecond;
        _lastRefillTimestamp = Stopwatch.GetTimestamp();
    }

    public async ValueTask ThrottleAsync(int bytesRequested, CancellationToken cancellationToken = default)
    {
        if (_speedLimitBytesPerSecond == null || bytesRequested <= 0)
        {
            return;
        }

        TimeSpan delay = TimeSpan.Zero;

        lock (_lock)
        {
            if (_speedLimitBytesPerSecond == null) return;

            long currentTimestamp = Stopwatch.GetTimestamp();
            double elapsedSeconds = (double)(currentTimestamp - _lastRefillTimestamp) / Stopwatch.Frequency;
            _lastRefillTimestamp = currentTimestamp;

            double rate = _speedLimitBytesPerSecond.Value;
            double maxCapacity = rate; // Burst buffer equivalent to 1 second
            _availableTokens = Math.Min(maxCapacity, _availableTokens + (elapsedSeconds * rate));

            // Deduct tokens directly to track debt across concurrent threads
            _availableTokens -= bytesRequested;

            if (_availableTokens < 0)
            {
                double secondsNeeded = -_availableTokens / rate;
                // Cap single sleep to 5s, and clamp debt to max 5s so workers don't freeze permanently
                delay = TimeSpan.FromSeconds(Math.Min(secondsNeeded, 5.0));
                if (_availableTokens < -rate * 5.0)
                {
                    _availableTokens = -rate * 5.0;
                }
            }
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }
}
