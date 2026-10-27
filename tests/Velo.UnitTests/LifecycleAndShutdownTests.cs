using FluentAssertions;
using Moq;
using Velo.Application;
using Velo.Domain;
using Velo.DownloadEngine;
using Xunit;

namespace Velo.UnitTests;

public class LifecycleAndShutdownTests
{
    [Fact]
    public async Task StopAllDownloadsAsync_CancelsAllActiveTokensGracefully()
    {
        // Arrange
        var probeMock = new Mock<IHttpProbe>();
        var engineMock = new Mock<IDownloadEngine>();
        var repoMock = new Mock<IDownloadRepository>();

        var job = new DownloadJob
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://speed.cloudflare.com/100mb.bin",
            EffectiveUrl = "https://speed.cloudflare.com/100mb.bin",
            TargetPath = "C:\\Downloads\\100mb.bin",
            TempPath = "C:\\Downloads\\100mb.bin.velo.part",
            DisplayName = "100mb.bin",
            Host = "speed.cloudflare.com",
            ExpectedBytes = 104857600,
            RangeSupported = true,
            State = JobState.Queued
        };

        repoMock.Setup(r => r.GetDownloadByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var tcs = new TaskCompletionSource();
        engineMock.Setup(e => e.ExecuteDownloadAsync(job, It.IsAny<IProgress<long>>(), It.IsAny<CancellationToken>()))
            .Returns(async (DownloadJob j, IProgress<long>? p, CancellationToken ct) =>
            {
                using var reg = ct.Register(() => tcs.TrySetResult());
                await tcs.Task;
                ct.ThrowIfCancellationRequested();
            });

        var service = new DownloadService(probeMock.Object, engineMock.Object, repoMock.Object);

        // Act: Start download in background
        _ = service.StartDownloadAsync(job.Id);
        await Task.Delay(100);

        // Act: Stop all downloads on application shutdown
        await service.StopAllDownloadsAsync();

        // Assert
        tcs.Task.IsCompleted.Should().BeTrue();
    }
}
