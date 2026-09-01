namespace WeatherCompare.Api.Storage;

/// <summary>
/// An immutable record of everything one Provider said about one Location at one moment.
/// Snapshots are appended, never updated or deleted (ADR-0001).
/// </summary>
public class ForecastSnapshot
{
    public long Id { get; init; }

    /// <summary>The Provider that issued the Forecasts, by name (e.g. "MET Norway").</summary>
    public required string Provider { get; init; }

    /// <summary>The Location's coordinate, at the precision the Provider accepts.</summary>
    public required decimal Latitude { get; init; }

    /// <summary>The Location's coordinate, at the precision the Provider accepts.</summary>
    public required decimal Longitude { get; init; }

    /// <summary>The moment this Snapshot was captured from the Provider, in UTC.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>The Provider's response verbatim, gzipped.</summary>
    public required byte[] Payload { get; init; }

    /// <summary>The Provider's HTTP <c>Expires</c> value: when it says to ask again.</summary>
    public DateTimeOffset? Expires { get; init; }

    /// <summary>The Provider's HTTP <c>Last-Modified</c> value: when it last recomputed.</summary>
    public DateTimeOffset? LastModified { get; init; }
}
