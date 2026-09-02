namespace WeatherCompare.Api.Locations;

/// <summary>
/// A candidate coordinate a name search offered, carrying enough context to tell Bergen in
/// Norway from Bergen in Germany. A Match is not a Location and does not become one by being
/// shown: nothing here is stored, there is no table behind it, and a Match that is never picked
/// leaves no trace (CONTEXT.md). It becomes a Location only by being tracked, and then it is the
/// coordinate that is the fact — that it was found by search rather than typed is not recorded.
/// <para>
/// <c>Admin1</c> is the gazetteer's word for the first-level region (Vestland, Bavaria). It is
/// kept under that name because that is what the field is, not because the domain has a term
/// for it — the domain has no concept of a region at all.
/// </para>
/// </summary>
public sealed record Match(
    string Name,
    string? Admin1,
    string? Country,
    int Elevation,
    decimal Latitude,
    decimal Longitude);

/// <summary>
/// What one search produced. An empty <see cref="Matches"/> with no <see cref="Failure"/> is a
/// search that ran and found nothing, which is a different answer from one that could not run —
/// the page says which, and either way typing a coordinate by hand still works (ADR-0004).
/// </summary>
public sealed record MatchSearch(IReadOnlyList<Match> Matches, string? Failure = null)
{
    public static MatchSearch Found(IReadOnlyList<Match> matches) => new(matches);

    public static MatchSearch Failed(string failure) => new([], failure);
}
