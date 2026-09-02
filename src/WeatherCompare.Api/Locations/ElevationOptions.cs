namespace WeatherCompare.Api.Locations;

/// <summary>
/// Where an elevation is looked up and how we identify ourselves doing it. Separate from
/// <see cref="GazetteerOptions"/> even though Open-Meteo serves both: it is a different host with
/// a different response, and ADR-0004 keeps each behind its own endpoint of ours so either can be
/// replaced alone. One vendor for both is a convenience, not a principle, and a single options
/// class would make it look like a principle.
/// </summary>
public class ElevationOptions
{
    public const string Section = "Elevation";

    public string BaseAddress { get; set; } = "https://api.open-meteo.com/v1/";

    public string UserAgent { get; set; } = "weather-compare/0.1 github.com/bjornedorKL/weather-compare";
}
