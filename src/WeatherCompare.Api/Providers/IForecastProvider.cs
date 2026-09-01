namespace WeatherCompare.Api.Providers;

/// <summary>
/// A Provider we can ask what the weather will be at a coordinate. Implementations do the
/// asking and nothing else: the response body is handed back verbatim, because a Forecast
/// Snapshot stores what the Provider said, not our reading of it (ADR-0001).
/// </summary>
public interface IForecastProvider
{
    /// <summary>The Provider's name, as stored on a Forecast Snapshot (e.g. "MET Norway").</summary>
    string Name { get; }

    /// <summary>
    /// Asks the Provider for its Forecasts at a coordinate.
    /// </summary>
    /// <param name="latitude">Degrees north; truncated to the precision the Provider accepts.</param>
    /// <param name="longitude">Degrees east; truncated to the precision the Provider accepts.</param>
    /// <param name="altitude">Metres above sea level, if known.</param>
    /// <param name="knownLastModified">
    /// The <c>Last-Modified</c> of the newest Snapshot we hold for this Provider and coordinate,
    /// so the Provider can answer "nothing new" instead of resending what we already have.
    /// </param>
    Task<ForecastFetch> FetchAsync(
        decimal latitude,
        decimal longitude,
        int? altitude,
        DateTimeOffset? knownLastModified,
        CancellationToken cancellationToken = default);
}
