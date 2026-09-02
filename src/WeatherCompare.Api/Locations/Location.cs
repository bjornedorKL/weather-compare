namespace WeatherCompare.Api.Locations;

/// <summary>
/// A point on earth we track weather for, identified by its coordinate at the precision
/// the Provider accepts (four decimals). The name is a human label for display only —
/// two Locations with the same coordinate are the same Location whatever they are called.
/// A row in <c>locations</c>; the ones with <see cref="Tracked"/> set are the Catalogue.
/// </summary>
public sealed record Location
{
    /// <summary>
    /// Surrogate key, so a Location can be named in a URL without spelling its coordinate out.
    /// Identity is still the coordinate — a unique index over it says so (ADR-0003).
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Human label for display. Not part of the Location's identity, which is why it is settable:
    /// renaming a Location changes nothing about which Location it is (CONTEXT.md), and is a
    /// deliberate act of its own rather than a side effect of tracking (ADR-0004).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>Latitude in degrees, at most four decimals.</summary>
    public required decimal Latitude { get; init; }

    /// <summary>Longitude in degrees, at most four decimals.</summary>
    public required decimal Longitude { get; init; }

    /// <summary>Height above sea level in whole metres; it materially affects forecast temperature.</summary>
    public required int Altitude { get; init; }

    /// <summary>
    /// Whether this Location is in the Catalogue. Untracking clears it: Providers stop being
    /// asked, the Snapshots already recorded survive, and the row stays so it can be tracked
    /// again without being described afresh (ADR-0003). Settable — Locations are not append-only.
    /// </summary>
    public bool Tracked { get; set; } = true;

    /// <summary>The coordinate that identifies this Location.</summary>
    public (decimal Latitude, decimal Longitude) Coordinate => (Latitude, Longitude);
}
