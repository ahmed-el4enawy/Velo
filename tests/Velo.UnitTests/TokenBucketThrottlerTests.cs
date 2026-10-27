using System.Diagnostics;
using FluentAssertions;
using Velo.DownloadEngine;
using Xunit;

namespace Velo.UnitTests;

public class TokenBucketThrottlerTests
{
    [Fact]
    public async Task ThrottleAsync_WhenUnlimited_ShouldCompleteInstantly()
    {
        var throttler = new TokenBucketThrottler(null);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 100; i++)
        {
            await throttler.ThrottleAsync(64 * 1024);
        }

        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeLessThan(1000, "Unlimited throttling should have zero artificial delay");
    }

    [Fact]
    public async Task ThrottleAsync_WhenCapExceeded_ShouldEnforceProportionalDelay()
    {
        // 100 KB/s cap
        long limitBytesPerSec = 100 * 1024;
        var throttler = new TokenBucketThrottler(limitBytesPerSec);

        // First consume available tokens
        await throttler.ThrottleAsync((int)limitBytesPerSec);

        // Next request for 50 KB should take ~500ms
        var sw = Stopwatch.StartNew();
        await throttler.ThrottleAsync(50 * 1024);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeInRange(350, 800, "50KB deficit at 100KB/s should delay ~500ms");
    }

    [Fact]
    public async Task ThrottleAsync_WhenMultipleWorkersConcurrent_ShouldLimitAggregateRate()
    {
        // 200 KB/s cap
        long limitBytesPerSec = 200 * 1024;
        var throttler = new TokenBucketThrottler(limitBytesPerSec);

        // First consume available burst tokens
        await throttler.ThrottleAsync((int)limitBytesPerSec);

        // Run 4 concurrent workers, each downloading 50 KB (total 200 KB)
        // 200 KB at 200 KB/s should take ~1000ms (+/- 300ms) across all workers combined
        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 4).Select(_ => throttler.ThrottleAsync(50 * 1024).AsTask());
        await Task.WhenAll(tasks);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeInRange(700, 1600,
            "Aggregate download of 200KB across 4 workers capped at 200KB/s must enforce aggregate delay");
    }
}

