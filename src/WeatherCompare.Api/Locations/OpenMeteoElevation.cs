using System.Globalization;
using System.Text.Json;
using WeatherCompare.Api.Providers;

namespace WeatherCompare.Api.Locations;

/// <summary>
/// The height of a coordinate, from Open-Meteo's elevation API over Copernicus DEM GLO-90. This
/// is the altitude for the route where the browser hands us a coordinate and nothing else usable:
/// <c>GeolocationCoordinates.altitude</c> is height above the WGS84 ellipsoid — some 40 m out in
/// Norway, and null on any device positioning by wi-fi — so it is never read (ADR-0004).
/// <para>
/// A separate client from <see cref="OpenMeteoGazetteer"/>, against a separate host, with its own
/// options and registration. Nothing is shared between them and no interface spans them: they
/// answer different questions and either could be replaced without touching the other.
/// </para>
/// <para>
/// Like a name search, a lookup is best-effort. Open-Meteo being unreachable must not stop a
/// Location being tracked — the altitude can still be typed — so every failure comes back as an
/// <see cref="ElevationLookup"/> that says what went wrong rather than as an exception.
/// </para>
/// </summary>
public sealed class OpenMeteoElevation(HttpClient http, ILogger<OpenMeteoElevation> logger)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// The height at a coordinate, asked for at the four decimals a Location is identified by
    /// (<see cref="CoordinatePrecision"/>) — so the height that comes back belongs to the point
    /// that would be tracked, and not to a more precise one nothing will ever store. A 90 m
    /// elevation model cannot tell the two apart anyway.
    /// </summary>
    public async Task<ElevationLookup> AtAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync(ElevationUrl(latitude, longitude), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "The elevation model answered {Status} to a lookup", (int)response.StatusCode);

                return ElevationLookup.Failed(
                    $"the elevation model answered {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return Read(body);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "The elevation model could not be reached");

            return ElevationLookup.Failed($"the elevation model could not be reached: {e.Message}");
        }
    }

    /// <summary>
    /// Reads the one number out of <c>{"elevation":[38.0]}</c>. An answer carrying no elevation is
    /// a failure and not a height of zero: the sea is at zero metres and Finse is at 1222, so a
    /// stand-in there would be indistinguishable from a real reading and would quietly wrong the
    /// temperature forecast.
    /// </summary>
    private ElevationLookup Read(string body)
    {
        ElevationResponse? answer;

        try
        {
            answer = JsonSerializer.Deserialize<ElevationResponse>(body, ReadOptions);
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "The elevation model answered 200 with something that is not its JSON");

            return ElevationLookup.Failed("the elevation model answered with something we could not read");
        }

        if (answer?.Elevation is not [var metres, ..])
        {
            logger.LogWarning("The elevation model answered 200 with no elevation in it");

            return ElevationLookup.Failed("the elevation model has no height for that coordinate");
        }

        // Metres are whole throughout the domain; the model's decimal fraction of one is noise
        // beside a 90 m grid.
        return ElevationLookup.Found((int)Math.Round(metres, MidpointRounding.AwayFromZero));
    }

    private static string ElevationUrl(decimal latitude, decimal longitude) =>
        $"elevation?latitude={Degrees(latitude)}&longitude={Degrees(longitude)}";

    private static string Degrees(decimal value) =>
        CoordinatePrecision.Truncate(value).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Open-Meteo's own shape. The elevation comes back as an array because the API takes a list
    /// of coordinates; we ask about one, and read the first.
    /// </summary>
    private sealed record ElevationResponse(List<double>? Elevation);
}
