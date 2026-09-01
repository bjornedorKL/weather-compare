using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WeatherCompare.Api.Providers;

namespace WeatherCompare.Api.Forecasts;

/// <summary>
/// Reads MET Norway's Locationforecast <c>compact</c> payload into Forecasts. Everything that
/// knows MET's JSON shape lives here; the rest of the read path only knows <see cref="Forecast"/>.
/// </summary>
public sealed class MetNorwayPayloadReader(IOptions<MetNorwayOptions> options) : IForecastPayloadReader
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Provider => options.Value.Name;

    public IReadOnlyList<Forecast> Read(string payload)
    {
        var compact = JsonSerializer.Deserialize<Compact>(payload, ReadOptions)
                      ?? throw new JsonException("MET Norway's payload is empty.");

        var timeseries = compact.Properties?.Timeseries
                         ?? throw new JsonException("MET Norway's payload has no properties.timeseries.");

        return timeseries
            .Where(step => step.Time is not null)
            .Select(ToForecast)
            .OrderBy(forecast => forecast.ValidAt)
            .ToList();
    }

    private static Forecast ToForecast(Timestep step)
    {
        var instant = step.Data?.Instant?.Details;
        var period = ShortestPeriod(step.Data);

        return new Forecast
        {
            ValidAt = step.Time!.Value.ToUniversalTime(),
            TemperatureCelsius = instant?.AirTemperature,
            WindSpeedMetresPerSecond = instant?.WindSpeed,
            WindFromDirectionDegrees = instant?.WindFromDirection,
            PrecipitationMillimetres = period?.PrecipitationMillimetres,

            // MET's own symbol names are our vocabulary, so they pass through untranslated (ADR-0002).
            Symbol = period?.Symbol,
            PeriodHours = period?.Hours,
        };
    }

    /// <summary>
    /// The shortest period MET summarised at this step. Not every step carries
    /// <c>next_1_hours</c>: the far end of the range only has 6- and 12-hour blocks, and the
    /// very last step has none at all, which is a Forecast with an instant and no Symbol.
    /// </summary>
    private static PeriodSummary? ShortestPeriod(StepData? data)
    {
        if (data is null)
        {
            return null;
        }

        (Block? Block, int Hours)[] shortestFirst =
        [
            (data.Next1Hours, 1),
            (data.Next6Hours, 6),
            (data.Next12Hours, 12),
        ];

        foreach (var (block, hours) in shortestFirst)
        {
            if (block?.Summary?.SymbolCode is { Length: > 0 } symbol)
            {
                return new PeriodSummary(symbol, block.Details?.PrecipitationAmount, hours);
            }
        }

        return null;
    }

    /// <summary>What MET says over one period: a Symbol and the precipitation that goes with it.</summary>
    private sealed record PeriodSummary(string Symbol, double? PrecipitationMillimetres, int Hours);

    private sealed record Compact(
        [property: JsonPropertyName("properties")] CompactProperties? Properties);

    private sealed record CompactProperties(
        [property: JsonPropertyName("timeseries")] IReadOnlyList<Timestep>? Timeseries);

    private sealed record Timestep(
        [property: JsonPropertyName("time")] DateTimeOffset? Time,
        [property: JsonPropertyName("data")] StepData? Data);

    private sealed record StepData(
        [property: JsonPropertyName("instant")] Instant? Instant,
        [property: JsonPropertyName("next_1_hours")] Block? Next1Hours,
        [property: JsonPropertyName("next_6_hours")] Block? Next6Hours,
        [property: JsonPropertyName("next_12_hours")] Block? Next12Hours);

    private sealed record Instant(
        [property: JsonPropertyName("details")] InstantDetails? Details);

    private sealed record InstantDetails(
        [property: JsonPropertyName("air_temperature")] double? AirTemperature,
        [property: JsonPropertyName("wind_speed")] double? WindSpeed,
        [property: JsonPropertyName("wind_from_direction")] double? WindFromDirection);

    private sealed record Block(
        [property: JsonPropertyName("summary")] BlockSummary? Summary,
        [property: JsonPropertyName("details")] BlockDetails? Details);

    private sealed record BlockSummary(
        [property: JsonPropertyName("symbol_code")] string? SymbolCode);

    private sealed record BlockDetails(
        [property: JsonPropertyName("precipitation_amount")] double? PrecipitationAmount);
}
