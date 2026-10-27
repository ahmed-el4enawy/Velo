using FluentAssertions;
using Moq;
using Velo.Application;
using Velo.Application.Queue;
using Velo.Domain;
using Xunit;

namespace Velo.UnitTests;

public class QueueManagerTests
{
    private readonly Mock<IDownloadRepository> _mockRepo = new();
    private readonly Mock<IDownloadService> _mockDownloadService = new();
    private readonly QueueManager _queueManager;

    public QueueManagerTests()
    {
        _queueManager = new QueueManager(_mockRepo.Object, _mockDownloadService.Object)
        {
            MaxConcurrentDownloads = 2 // Limit to 2 for deterministic testing
        };
    }

    [Fact]
    public async Task EnqueueAsync_UnderLimit_ShouldStartImmediately()
    {
        var job1 = Guid.NewGuid();
        var job2 = Guid.NewGuid();

        await _queueManager.EnqueueAsync(job1);
        await _queueManager.EnqueueAsync(job2);

        _queueManager.GetActiveJobIds().Should().HaveCount(2);
        _queueManager.GetQueuedJobIds().Should().BeEmpty();

        _mockDownloadService.Verify(s => s.StartDownloadAsync(job1, It.IsAny<CancellationToken>()), Times.Once);
        _mockDownloadService.Verify(s => s.StartDownloadAsync(job2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnqueueAsync_OverLimit_ShouldQueueAndSetState()
    {
        var job1 = Guid.NewGuid();
        var job2 = Guid.NewGuid();
        var job3 = Guid.NewGuid();

        var job3Entity = new DownloadJob { Id = job3, State = JobState.Created };
        _mockRepo.Setup(r => r.GetDownloadByIdAsync(job3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job3Entity);

        await _queueManager.EnqueueAsync(job1);
        await _queueManager.EnqueueAsync(job2);
        await _queueManager.EnqueueAsync(job3);

        _queueManager.GetActiveJobIds().Should().HaveCount(2);
        _queueManager.GetQueuedJobIds().Should().ContainSingle().Which.Should().Be(job3);

        job3Entity.State.Should().Be(JobState.Queued);
        _mockRepo.Verify(r => r.UpdateDownloadAsync(job3Entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyJobStateChangedAsync_WhenActiveFinishes_ShouldPromoteQueuedJob()
    {
        var job1 = Guid.NewGuid();
        var job2 = Guid.NewGuid();
        var job3 = Guid.NewGuid();

        _mockRepo.Setup(r => r.GetDownloadByIdAsync(job3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadJob { Id = job3, State = JobState.Created });

        await _queueManager.EnqueueAsync(job1);
        await _queueManager.EnqueueAsync(job2);
        await _queueManager.EnqueueAsync(job3);

        // Job 1 completes/finishes
        await _queueManager.NotifyJobStateChangedAsync(job1);

        _queueManager.GetActiveJobIds().Should().Contain(job3);
        _queueManager.GetQueuedJobIds().Should().BeEmpty();

        _mockDownloadService.Verify(s => s.StartDownloadAsync(job3, It.IsAny<CancellationToken>()), Times.Once);
    }
}
