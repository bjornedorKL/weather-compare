namespace WeatherCompare.Api.Forecasts;

/// <summary>
/// A statement about what the weather will be at a Location at one future moment. This is the
/// common shape every Provider is read into; nothing here is MET Norway's, except the Symbol
/// vocabulary, which is MET's on purpose (ADR-0002).
/// </summary>
public sealed record Forecast
{
    /// <summary>The future moment this Forecast describes, in UTC.</summary>
    public required DateTimeOffset ValidAt { get; init; }

    /// <summary>Air temperature in degrees Celsius.</summary>
    public double? TemperatureCelsius { get; init; }

    /// <summary>Wind speed in metres per second.</summary>
    public double? WindSpeedMetresPerSecond { get; init; }

    /// <summary>The direction the wind blows from, in degrees clockwise from north.</summary>
    public double? WindFromDirectionDegrees { get; init; }

    /// <summary>Precipitation in millimetres over <see cref="PeriodHours"/>.</summary>
    public double? PrecipitationMillimetres { get; init; }

    /// <summary>
    /// What the weather looks like, in MET Norway's symbol vocabulary (ADR-0002):
    /// <c>clearsky_day</c>, <c>partlycloudy_night</c>, <c>sleet</c>. Passed through verbatim.
    /// </summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// The length in hours of the period the <see cref="Symbol"/> and precipitation describe.
    /// Providers summarise over a window, and 0.5 mm over one hour is not 0.5 mm over six —
    /// so the window travels with the numbers rather than being guessed at by the reader.
    /// Null when the Provider said nothing beyond the instant.
    /// </summary>
    public int? PeriodHours { get; init; }
}
