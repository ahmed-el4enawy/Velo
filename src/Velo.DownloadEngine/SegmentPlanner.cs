using Velo.Domain;

namespace Velo.DownloadEngine;

/// <summary>
/// Allocates non-overlapping byte ranges for parallel download segments as defined in PRD Section 5.2.
/// </summary>
public static class SegmentPlanner
{
    private const long MinimumSegmentSize = 1024 * 1024; // 1 MB minimum chunk to avoid micro-splitting

    public static List<DownloadSegment> PlanSegments(Guid downloadId, long totalBytes, int requestedConnections)
    {
        if (totalBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalBytes), "Total bytes must be positive to plan segments.");

        int safeConnectionCount = Math.Clamp(requestedConnections, 1, 16);

        // Adjust connection count if the file is smaller than MinimumSegmentSize * connectionCount
        if (totalBytes < MinimumSegmentSize * safeConnectionCount && safeConnectionCount > 1)
        {
            safeConnectionCount = Math.Max(1, (int)(totalBytes / MinimumSegmentSize));
        }

        long chunkSize = totalBytes / safeConnectionCount;
        var segments = new List<DownloadSegment>(safeConnectionCount);

        long currentStart = 0;
        for (int i = 0; i < safeConnectionCount; i++)
        {
            long currentEnd = (i == safeConnectionCount - 1) ? totalBytes - 1 : currentStart + chunkSize - 1;

            segments.Add(new DownloadSegment
            {
                DownloadId = downloadId,
                Index = i,
                StartByte = currentStart,
                EndByte = currentEnd,
                NextByte = currentStart,
                State = SegmentState.Pending
            });

            currentStart = currentEnd + 1;
        }

        return segments;
    }
}
