namespace WeatherCompare.Api.Locations;

/// <summary>
/// A new label for a Location we already know. The name and nothing else: a Location <em>is</em>
/// its coordinate, so renaming one changes nothing about which Location it is (CONTEXT.md), while
/// changing the coordinate would make it a different Location holding the old one's history.
/// Altitude is left out for a different reason — it is not a label, it changes what the forecast
/// says (ADR-0004).
/// </summary>
public sealed record RenameLocationRequest(string? Name)
{
    /// <summary>
    /// Reads the request as a name, or says what is wrong with it. The rule is
    /// <see cref="TrackLocationRequest.ReadName"/>'s, called rather than copied. Nothing here
    /// looks for a Location already called this: two Locations may both be "Home", and refusing
    /// that would quietly make the name part of identity, which CONTEXT.md denies outright.
    /// </summary>
    public NameDescription Describe()
    {
        var errors = new Dictionary<string, string[]>();
        var name = TrackLocationRequest.ReadName(Name, errors);

        return new NameDescription(name, errors);
    }
}

/// <summary>
/// A request read as a name, or the reasons it was not one. <see cref="Name"/> is null exactly
/// when <see cref="Errors"/> has something in it.
/// </summary>
public sealed record NameDescription(
    string? Name,
    Dictionary<string, string[]> Errors);
