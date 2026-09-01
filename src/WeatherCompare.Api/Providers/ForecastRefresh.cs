namespace WeatherCompare.Api.Providers;

public enum ForecastRefreshOutcome
{
    /// <summary>A new Forecast Snapshot was appended.</summary>
    SnapshotAppended,

    /// <summary>The Provider had nothing new to say; nothing was written.</summary>
    NotModified,

    /// <summary>The Provider could not be asked, or answered unusably; nothing was written.</summary>
    Failed,
}

/// <summary>What one refresh of one Provider at one coordinate produced.</summary>
public sealed record ForecastRefreshResult(
    ForecastRefreshOutcome Outcome,
    long? SnapshotId = null,
    int CompressedBytes = 0,
    string? Failure = null)
{
    /// <summary>True only when a Forecast Snapshot was actually appended.</summary>
    public bool SnapshotWritten => Outcome is ForecastRefreshOutcome.SnapshotAppended;
}
