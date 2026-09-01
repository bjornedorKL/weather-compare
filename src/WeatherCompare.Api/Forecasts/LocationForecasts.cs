namespace WeatherCompare.Api.Forecasts;

/// <summary>
/// A Location we track, with what each Provider's newest Forecast Snapshot says about it.
/// <see cref="Snapshots"/> is empty for a Location no Provider has been asked about yet.
/// </summary>
public sealed record LocationForecasts(
    string Name,
    decimal Latitude,
    decimal Longitude,
    int Altitude,
    IReadOnlyList<SnapshotForecasts> Snapshots);

/// <summary>
/// The Forecasts held in one Provider's newest Forecast Snapshot for a Location, carrying the
/// Snapshot's Issued At — the moment we asked, not a moment any Forecast describes. Without it
/// the page cannot say how stale what it shows is.
/// </summary>
public sealed record SnapshotForecasts(
    string Provider,
    DateTimeOffset IssuedAt,
    IReadOnlyList<Forecast> Forecasts);
