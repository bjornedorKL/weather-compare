using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WeatherCompare.Api.Locations;

/// <summary>
/// Names looked up in Open-Meteo's geocoding API, which serves the GeoNames gazetteer and returns
/// an elevation alongside every coordinate — so a Match satisfies the rule that a Location always
/// has an altitude in the same response (ADR-0004).
/// <para>
/// Everything that knows Open-Meteo's twenty-field response shape lives in this file, the way
/// <c>MetNorwayPayloadReader</c> is the only thing that knows MET's. What leaves here is a
/// <see cref="Match"/>, so swapping gazetteers is a change to this file and its registration and
/// nothing else. There is no interface over it: there is one gazetteer, and inventing a second
/// seam for a second implementation that does not exist would hide that rather than help it.
/// </para>
/// <para>
/// A search is best-effort. Open-Meteo being unreachable means a Location cannot be found by
/// name; it must never mean a coordinate cannot be typed, so every failure here comes back as a
/// <see cref="MatchSearch"/> saying so rather than as an exception.
/// </para>
/// </summary>
public sealed class OpenMeteoGazetteer(
    HttpClient http,
    IOptions<GazetteerOptions> options,
    ILogger<OpenMeteoGazetteer> logger)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly GazetteerOptions _options = options.Value;

    public async Task<MatchSearch> SearchAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync(SearchUrl(name), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("The gazetteer answered {Status} to a search", (int)response.StatusCode);

                return MatchSearch.Failed(
                    $"the gazetteer answered {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return Read(body);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "The gazetteer could not be reached");

            return MatchSearch.Failed($"the gazetteer could not be reached: {e.Message}");
        }
    }

    /// <summary>
    /// Trims the response to Matches. A search that matched nothing comes back without a
    /// <c>results</c> array at all rather than with an empty one, which is the same answer and
    /// is read as one.
    /// </summary>
    private MatchSearch Read(string body)
    {
        GazetteerResponse? answer;

        try
        {
            answer = JsonSerializer.Deserialize<GazetteerResponse>(body, ReadOptions);
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "The gazetteer answered 200 with something that is not its JSON");

            return MatchSearch.Failed("the gazetteer answered with something we could not read");
        }

        var results = answer?.Results ?? [];

        return MatchSearch.Found(results.Select(AsMatch).OfType<Match>().ToList());
    }

    /// <summary>
    /// A result without a name, or without an altitude we can use, is dropped rather than patched
    /// up. Altitude is load-bearing for the temperature forecast (ADR-0004), so standing a number
    /// in for a height the gazetteer does not have would be a quiet lie — a zero where the field
    /// is absent, or the field's own contents where they are not a height at all. A Match nobody
    /// can use is better absent than present-and-wrong, and typing the coordinate by hand stays
    /// the route for a place the gazetteer cannot describe.
    /// </summary>
    private static Match? AsMatch(GazetteerResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Name) || Altitude(result.Elevation) is not { } elevation)
        {
            return null;
        }

        return new Match(
            result.Name.Trim(),
            Blank(result.Admin1) ? null : result.Admin1!.Trim(),
            Blank(result.Country) ? null : result.Country!.Trim(),
            // Metres are whole throughout the domain; the gazetteer's decimal fraction of one is
            // noise beside a 90 m elevation model.
            (int)Math.Round(elevation, MidpointRounding.AwayFromZero),
            result.Latitude,
            result.Longitude);
    }

    /// <summary>
    /// The altitude a Match can carry, or nothing at all: one field, one rule. An absent elevation
    /// and an impossible one are the same answer — the gazetteer has no height for this place —
    /// and reading them as one is what keeps the second from arriving through the door the first
    /// is watched at. GeoNames, which this gazetteer serves, says "we do not know" with 9999, a
    /// number the code would otherwise accept and hand on as a Location ten kilometres in the air.
    /// <para>
    /// Possible is -500 m to 9000 m. Below: the Dead Sea shore is around -430 m and is the lowest
    /// land there is. Above: Everest is 8849 m. Both with room to spare, so the range turns away
    /// the sentinel and anything else that is not a height without turning away anywhere a
    /// forecast could be wanted.
    /// </para>
    /// </summary>
    private static double? Altitude(double? elevation) => elevation is >= -500 and <= 9000 ? elevation : null;

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private string SearchUrl(string name) =>
        $"search?name={Uri.EscapeDataString(name)}" +
        $"&count={_options.Count.ToString(CultureInfo.InvariantCulture)}" +
        "&language=en&format=json";

    /// <summary>Open-Meteo's own shape, read no further than the six fields a Match carries.</summary>
    private sealed record GazetteerResponse(List<GazetteerResult>? Results);

    private sealed record GazetteerResult(
        string? Name,
        string? Admin1,
        string? Country,
        double? Elevation,
        decimal Latitude,
        decimal Longitude);
}
