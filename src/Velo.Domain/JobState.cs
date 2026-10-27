namespace Velo.Domain;

/// <summary>
/// Explicit state machine transitions for download jobs as defined in Velo PRD v2.0 Section 7.2.
/// </summary>
public enum JobState
{
    Created = 0,
    Probing = 1,
    Queued = 2,
    Downloading = 3,
    Paused = 4,
    Retrying = 5,
    Verifying = 6,
    Completed = 7,
    Failed = 8,
    Canceled = 9,
    Interrupted = 10
}

public enum JobPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum SegmentState
{
    Pending = 0,
    Downloading = 1,
    Completed = 2,
    Failed = 3
}
