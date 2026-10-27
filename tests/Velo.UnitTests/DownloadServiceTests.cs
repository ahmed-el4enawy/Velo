using FluentAssertions;
using Moq;
using Velo.Application;
using Velo.Domain;
using Velo.DownloadEngine;
using Xunit;

namespace Velo.UnitTests;

public class DownloadServiceTests
{
    private readonly Mock<IHttpProbe> _mockProbe = new();
    private readonly Mock<IDownloadEngine> _mockEngine = new();
    private readonly Mock<IDownloadRepository> _mockRepo = new();
    private readonly DownloadService _service;

    public DownloadServiceTests()
    {
        _service = new DownloadService(_mockProbe.Object, _mockEngine.Object, _mockRepo.Object);
    }

    [Fact]
    public async Task CreateDownloadAsync_ValidUrl_ShouldProbeAndSaveJob()
    {
        // Arrange
        string testUrl = "https://example.com/installer.msi";
        _mockProbe.Setup(p => p.ProbeUrlAsync(testUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProbeResult(
                EffectiveUrl: testUrl,
                ContentLength: 50000000,
                ContentDispositionFileName: "installer.msi",
                ETag: "\"etag-123\"",
                LastModified: null,
                RangeSupported: true,
                IsSuccess: true,
                ErrorMessage: null));

        // Act
        var job = await _service.CreateDownloadAsync(testUrl);

        // Assert
        job.Should().NotBeNull();
        job.DisplayName.Should().Be("installer.msi");
        job.ExpectedBytes.Should().Be(50000000);
        job.RangeSupported.Should().BeTrue();
        job.Segments.Should().HaveCount(4);

        _mockRepo.Verify(r => r.AddDownloadAsync(It.Is<DownloadJob>(j => j.Id == job.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PauseDownloadAsync_ShouldSetStateToPaused()
    {
        var jobId = Guid.NewGuid();
        var existingJob = new DownloadJob
        {
            State = JobState.Downloading
        };

        _mockRepo.Setup(r => r.GetDownloadByIdAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingJob);

        await _service.PauseDownloadAsync(jobId);

        existingJob.State.Should().Be(JobState.Paused);
        _mockRepo.Verify(r => r.UpdateDownloadAsync(existingJob, It.IsAny<CancellationToken>()), Times.Once);
    }
}
