namespace WeatherCompare.Api.Locations;

/// <summary>
/// What one elevation lookup produced: metres above sea level, or the reason there are none.
/// Exactly one of the two is set — there is no third answer, because a coordinate the elevation
/// model has no height for is a lookup that did not work rather than a Location at zero metres.
/// Altitude is load-bearing for the temperature forecast (ADR-0004), so it is either looked up
/// or typed, never defaulted.
/// </summary>
public sealed record ElevationLookup(int? Metres, string? Failure = null)
{
    public static ElevationLookup Found(int metres) => new(metres);

    public static ElevationLookup Failed(string failure) => new(null, failure);
}

/// <summary>
/// The height at one coordinate, as the page reads it. Not a Location and not a Match: nothing
/// is stored by asking, and the answer only fills the altitude field of the track form, which is
/// still submitted by hand.
/// </summary>
public sealed record CoordinateElevation(int Elevation);
