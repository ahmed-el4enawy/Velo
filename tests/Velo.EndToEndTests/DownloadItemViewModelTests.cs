using FluentAssertions;
using Velo.Desktop.ViewModels;
using Velo.Domain;
using Xunit;

namespace Velo.EndToEndTests;

public class DownloadItemViewModelTests
{
    private readonly FakeDownloadService _downloadService = new();

    [Fact]
    public void Properties_ShouldExposePRDDetailsAccurately()
    {
        var job = new DownloadJob
        {
            OriginalUrl = "https://example.com/archive.zip",
            EffectiveUrl = "https://cdn.example.com/archive.zip",
            TargetPath = @"C:\Downloads\archive.zip",
            TempPath = @"C:\Downloads\archive.zip.velo.part",
            DisplayName = "archive.zip",
            Host = "cdn.example.com",
            ExpectedBytes = 100 * 1024 * 1024,
            DownloadedBytes = 50 * 1024 * 1024,
            RangeSupported = true,
            StrongETag = "\"abc123etag\"",
            LastModified = "Wed, 21 Oct 2026 07:28:00 GMT",
            Category = CategoryType.Compressed,
            Priority = JobPriority.High,
            State = JobState.Downloading
        };

        job.Segments =
        [
            new DownloadSegment { Index = 0, StartByte = 0, EndByte = 25_000_000, NextByte = 25_000_001, State = SegmentState.Completed },
            new DownloadSegment { Index = 1, StartByte = 25_000_001, EndByte = 50_000_000, NextByte = 37_500_000, State = SegmentState.Downloading },
            new DownloadSegment { Index = 2, StartByte = 50_000_001, EndByte = 75_000_000, NextByte = 50_000_001, State = SegmentState.Pending },
            new DownloadSegment { Index = 3, StartByte = 75_000_001, EndByte = 100_000_000, NextByte = 75_000_001, State = SegmentState.Pending }
        ];

        var vm = new DownloadItemViewModel(job, _downloadService);

        vm.DisplayName.Should().Be("archive.zip");
        vm.Host.Should().Be("cdn.example.com");
        vm.EffectiveUrl.Should().Be("https://cdn.example.com/archive.zip");
        vm.RangeSupportText.Should().Contain("HTTP 206 Multi-stream");
        vm.EtagText.Should().Be("\"abc123etag\"");
        vm.LastModifiedText.Should().Contain("Wed, 21 Oct 2026");
        vm.CategoryText.Should().Be("Compressed");
        vm.PriorityText.Should().Be("High");
        vm.SegmentsCount.Should().Be(4);
        vm.Segments.Should().HaveCount(4);

        vm.IsDetailsExpanded.Should().BeFalse();
        vm.ToggleDetailsCommand.Execute(null);
        vm.IsDetailsExpanded.Should().BeTrue();
    }

    [Fact]
    public void UpdateProgress_ShouldUpdateSpeedAndEta()
    {
        var job = new DownloadJob
        {
            ExpectedBytes = 10 * 1024 * 1024, // 10 MB
            DownloadedBytes = 0,
            State = JobState.Downloading
        };

        var vm = new DownloadItemViewModel(job, _downloadService);

        // Simulate 5 MB downloaded at 2.5 MB/s
        vm.UpdateProgress(5 * 1024 * 1024, 2.5 * 1024 * 1024);

        vm.ProgressPercentage.Should().BeApproximately(50.0, 0.1);
        vm.SpeedText.Should().Contain("2.5 MB/s");
        vm.EtaText.Should().Contain("2s left");
    }

    [Fact]
    public void ContextMenu_StateProperties_ShouldMatchExpectedStates()
    {
        var jobCompleted = new DownloadJob { State = JobState.Completed };
        var vmCompleted = new DownloadItemViewModel(jobCompleted, _downloadService);
        vmCompleted.CanOpen.Should().BeTrue();
        vmCompleted.CanResume.Should().BeFalse();
        vmCompleted.CanStop.Should().BeFalse();
        vmCompleted.CanRefreshAddress.Should().BeFalse();

        var jobPaused = new DownloadJob { State = JobState.Paused };
        var vmPaused = new DownloadItemViewModel(jobPaused, _downloadService);
        vmPaused.CanResume.Should().BeTrue();
        vmPaused.CanStop.Should().BeFalse();
        vmPaused.CanRefreshAddress.Should().BeTrue();

        var jobDownloading = new DownloadJob { State = JobState.Downloading };
        var vmDownloading = new DownloadItemViewModel(jobDownloading, _downloadService);
        vmDownloading.CanResume.Should().BeFalse();
        vmDownloading.CanStop.Should().BeTrue();
        vmDownloading.CanRefreshAddress.Should().BeTrue();
    }

    [Fact]
    public async Task ContextMenu_QueueOperations_ShouldUpdateQueueState()
    {
        var job = new DownloadJob { State = JobState.Paused };
        var vm = new DownloadItemViewModel(job, _downloadService);

        vm.CanDeleteFromQueue.Should().BeFalse();

        await vm.AddToQueueCommand.ExecuteAsync("Main download queue");
        vm.QueueName.Should().Be("Main download queue");
        vm.Job.State.Should().Be(JobState.Queued);
        vm.CanDeleteFromQueue.Should().BeTrue();

        await vm.DeleteFromQueueCommand.ExecuteAsync(null);
        vm.QueueName.Should().BeNull();
        vm.Job.State.Should().Be(JobState.Paused);
        vm.CanDeleteFromQueue.Should().BeFalse();
    }

    [Fact]
    public void ContextMenu_DoubleClickAction_ShouldUpdatePreferences()
    {
        var job = new DownloadJob();
        var vm = new DownloadItemViewModel(job, _downloadService);

        vm.DoubleClickAction.Should().Be(DoubleClickActionType.OpenProgress);
        vm.IsDoubleClickOpenProgress.Should().BeTrue();

        vm.SetDoubleClickActionCommand.Execute("OpenFile");
        vm.DoubleClickAction.Should().Be(DoubleClickActionType.OpenFile);
        vm.IsDoubleClickOpenFile.Should().BeTrue();
        vm.IsDoubleClickOpenProgress.Should().BeFalse();

        vm.SetDoubleClickActionCommand.Execute("OpenFolder");
        vm.DoubleClickAction.Should().Be(DoubleClickActionType.OpenFolder);
        vm.IsDoubleClickOpenFolder.Should().BeTrue();

        vm.SetDoubleClickActionCommand.Execute("Properties");
        vm.DoubleClickAction.Should().Be(DoubleClickActionType.Properties);
        vm.IsDoubleClickProperties.Should().BeTrue();
    }
}
