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
        sw.ElapsedMilliseconds.Should().BeLessThan(100, "Unlimited throttling should have zero artificial delay");
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
}
