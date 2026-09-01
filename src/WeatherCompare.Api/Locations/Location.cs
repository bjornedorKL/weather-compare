namespace WeatherCompare.Api.Locations;

/// <summary>
/// A point on earth we track weather for, identified by its coordinate at the precision
/// the Provider accepts (four decimals). The name is a human label for display only —
/// two Locations with the same coordinate are the same Location whatever they are called.
/// </summary>
public sealed record Location
{
    /// <summary>Human label for display. Not part of the Location's identity.</summary>
    public required string Name { get; init; }

    /// <summary>Latitude in degrees, at most four decimals.</summary>
    public required decimal Latitude { get; init; }

    /// <summary>Longitude in degrees, at most four decimals.</summary>
    public required decimal Longitude { get; init; }

    /// <summary>Height above sea level in whole metres; it materially affects forecast temperature.</summary>
    public required int Altitude { get; init; }

    /// <summary>The coordinate that identifies this Location.</summary>
    public (decimal Latitude, decimal Longitude) Coordinate => (Latitude, Longitude);
}
