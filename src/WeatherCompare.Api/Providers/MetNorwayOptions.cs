namespace WeatherCompare.Api.Providers;

/// <summary>
/// How we identify ourselves to MET Norway and where we ask. MET blocks applications that do
/// not send an identifying <c>User-Agent</c>, so it is configuration, not a constant buried in
/// a request.
/// </summary>
public class MetNorwayOptions
{
    public const string Section = "Providers:MetNorway";

    /// <summary>The Provider's name, as stored on a Forecast Snapshot.</summary>
    public string Name { get; set; } = "MET Norway";

    public string BaseAddress { get; set; } = "https://api.met.no/weatherapi/locationforecast/2.0/";

    /// <summary>Mandatory: an application name and a way to contact whoever runs it.</summary>
    public string UserAgent { get; set; } = "weather-compare/0.1 github.com/bjornedorKL/weather-compare";
}
