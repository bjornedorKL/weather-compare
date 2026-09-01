namespace WeatherCompare.Api.Forecasts;

/// <summary>
/// Reads one Provider's stored Forecast Snapshot payload into Forecasts. This is the seam a
/// second Provider arrives through: normalisation lives on read, in code, so a mapping bug is
/// fixed by changing an implementation and reading the Snapshots again (ADR-0001).
/// </summary>
public interface IForecastPayloadReader
{
    /// <summary>The Provider whose payloads this reads, by the name stored on a Snapshot.</summary>
    string Provider { get; }

    /// <summary>
    /// Reads a Snapshot's payload — the Provider's response verbatim, already decompressed —
    /// into Forecasts ordered by the moment they describe.
    /// </summary>
    IReadOnlyList<Forecast> Read(string payload);
}
