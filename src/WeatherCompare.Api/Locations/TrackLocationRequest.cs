using WeatherCompare.Api.Providers;

namespace WeatherCompare.Api.Locations;

/// <summary>
/// A Location as the page submits it: a coordinate, a human label and an altitude. Nothing here
/// looks a name up. A coordinate can now be <em>found</em> — <c>GET /api/locations/search</c>
/// offers Matches and the page fills these four fields from the one picked (ADR-0004) — but it
/// arrives here as four numbers either way, and this type cannot tell a picked Match from a
/// typed coordinate. Nothing records the difference: the coordinate is the fact, and how it was
/// arrived at is not.
/// </summary>
public sealed record TrackLocationRequest(
    string? Name,
    decimal? Latitude,
    decimal? Longitude,
    int? Altitude)
{
    /// <summary>The <c>name</c> column holds this much and no more.</summary>
    public const int LongestName = 100;

    /// <summary>Below the Dead Sea shore and above Everest is a typo, not a Location.</summary>
    public const int LowestAltitude = -500;

    public const int HighestAltitude = 9000;

    /// <summary>
    /// Reads the request as a Location, or says what is wrong with it. A coordinate finer than
    /// the Provider accepts is <em>truncated</em> here, not refused: this is a coordinate typed
    /// or pasted by a person, and rejecting a phone's seven-decimal reading would be hostile.
    /// The hand-written seed file refuses the same coordinate, because silently moving an entry
    /// someone wrote down would change which Location it is. The asymmetry is deliberate (ADR-0003).
    /// </summary>
    public LocationDescription Describe()
    {
        var errors = new Dictionary<string, string[]>();

        var name = ReadName(Name, errors);
        var latitude = ReadCoordinate(Latitude, nameof(Latitude), -90m, 90m, errors);
        var longitude = ReadCoordinate(Longitude, nameof(Longitude), -180m, 180m, errors);
        var altitude = ReadAltitude(errors);

        if (errors.Count > 0)
        {
            return new LocationDescription(null, errors);
        }

        return new LocationDescription(
            new Location
            {
                Name = name!,
                Latitude = latitude!.Value,
                Longitude = longitude!.Value,
                Altitude = altitude!.Value,
            },
            errors);
    }

    /// <summary>
    /// The name rule, shared rather than restated: what is left after trimming, non-empty and no
    /// longer than the column holds. <see cref="RenameLocationRequest"/> calls this, so a name a
    /// Location can be tracked under is exactly a name it can be renamed to.
    /// </summary>
    public static string? ReadName(string? typed, Dictionary<string, string[]> errors)
    {
        var name = typed?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            errors[nameof(Name)] = ["Give the Location a name to show it by."];
            return null;
        }

        if (name.Length > LongestName)
        {
            errors[nameof(Name)] = [$"A Location's name is at most {LongestName} characters."];
            return null;
        }

        return name;
    }

    private static decimal? ReadCoordinate(
        decimal? value,
        string field,
        decimal min,
        decimal max,
        Dictionary<string, string[]> errors)
    {
        if (value is not { } degrees)
        {
            errors[field] = [$"Give a {field.ToLowerInvariant()} in degrees."];
            return null;
        }

        if (degrees < min || degrees > max)
        {
            errors[field] = [$"{degrees} is outside {min}..{max} degrees."];
            return null;
        }

        return CoordinatePrecision.Truncate(degrees);
    }

    private int? ReadAltitude(Dictionary<string, string[]> errors)
    {
        if (Altitude is not { } altitude)
        {
            errors[nameof(Altitude)] =
                ["Give an altitude in whole metres above sea level; it changes the temperature forecast."];

            return null;
        }

        if (altitude is < LowestAltitude or > HighestAltitude)
        {
            errors[nameof(Altitude)] =
                [$"{altitude} metres is outside {LowestAltitude}..{HighestAltitude}."];

            return null;
        }

        return altitude;
    }
}

/// <summary>
/// A request read as a Location, or the reasons it could not be. <see cref="Location"/> is null
/// exactly when <see cref="Errors"/> has something in it.
/// </summary>
public sealed record LocationDescription(
    Location? Location,
    Dictionary<string, string[]> Errors);
