namespace WeatherCompare.Api.Locations;

/// <summary>
/// Where names are looked up and how we identify ourselves doing it. Configuration rather than
/// constants in a request for the same reason MET's is (<c>MetNorwayOptions</c>): a
/// <c>User-Agent</c> naming the application and where to complain about it is what an external
/// service is owed, and it changes without a rebuild.
/// </summary>
public class GazetteerOptions
{
    public const string Section = "Gazetteer";

    public string BaseAddress { get; set; } = "https://geocoding-api.open-meteo.com/v1/";

    public string UserAgent { get; set; } = "weather-compare/0.1 github.com/bjornedorKL/weather-compare";

    /// <summary>How many Matches to ask for. Enough to separate the four Bergens, few enough to read.</summary>
    public int Count { get; set; } = 10;
}
