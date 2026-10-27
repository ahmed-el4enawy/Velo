using FluentAssertions;
using Velo.DownloadEngine;
using Xunit;

namespace Velo.UnitTests;

public class SegmentPlannerTests
{
    [Fact]
    public void PlanSegments_ShouldCreateNonOverlappingContiguousSegments()
    {
        // Arrange
        var downloadId = Guid.NewGuid();
        long totalBytes = 100 * 1024 * 1024; // 100 MB
        int requestedConnections = 4;

        // Act
        var segments = SegmentPlanner.PlanSegments(downloadId, totalBytes, requestedConnections);

        // Assert
        segments.Should().HaveCount(4);
        segments[0].StartByte.Should().Be(0);
        segments[^1].EndByte.Should().Be(totalBytes - 1);

        for (int i = 0; i < segments.Count - 1; i++)
        {
            segments[i + 1].StartByte.Should().Be(segments[i].EndByte + 1, "Segments must be strictly contiguous without gaps");
        }

        long sumOfSegmentLengths = segments.Sum(s => s.TotalBytes);
        sumOfSegmentLengths.Should().Be(totalBytes, "Sum of all segments must equal the exact file length");
    }

    [Fact]
    public void PlanSegments_SmallFile_ShouldLimitToSingleSegment()
    {
        var downloadId = Guid.NewGuid();
        long totalBytes = 500 * 1024; // 500 KB (less than 1 MB minimum chunk)

        var segments = SegmentPlanner.PlanSegments(downloadId, totalBytes, 8);

        segments.Should().HaveCount(1);
        segments[0].StartByte.Should().Be(0);
        segments[0].EndByte.Should().Be(totalBytes - 1);
    }
}
