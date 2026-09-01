using System.Text.Json;
using Microsoft.Extensions.Options;
using WeatherCompare.Api.Forecasts;
using WeatherCompare.Api.Providers;

namespace WeatherCompare.Tests;

public class MetNorwayPayloadReaderTests
{
    [Fact]
    public void Reads_a_saved_snapshot_payload_into_forecasts()
    {
        var forecasts = Reader().Read(MetNorwayPayload.SavedOsloSnapshot);

        Assert.Equal(86, forecasts.Count);

        var first = forecasts[0];
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero), first.ValidAt);
        Assert.Equal(17.6, first.TemperatureCelsius);
        Assert.Equal(2.3, first.WindSpeedMetresPerSecond);
        Assert.Equal(201.0, first.WindFromDirectionDegrees);
        Assert.Equal(0.0, first.PrecipitationMillimetres);
        Assert.Equal("cloudy", first.Symbol);
        Assert.Equal(1, first.PeriodHours);
    }

    [Fact]
    public void Reads_every_forecast_in_utc_ordered_by_the_moment_it_describes()
    {
        var forecasts = Reader().Read(MetNorwayPayload.SavedOsloSnapshot);

        Assert.All(forecasts, forecast => Assert.Equal(TimeSpan.Zero, forecast.ValidAt.Offset));
        Assert.Equal(forecasts.OrderBy(forecast => forecast.ValidAt), forecasts);
        Assert.Equal(new DateTimeOffset(2026, 9, 11, 6, 0, 0, TimeSpan.Zero), forecasts[^1].ValidAt);
    }

    /// <summary>
    /// The far end of MET's range has no <c>next_1_hours</c> block, only 6- and 12-hour ones.
    /// The Forecast takes the shortest period that is actually there, and says which it was.
    /// </summary>
    [Fact]
    public void Falls_back_to_a_longer_period_when_the_step_has_no_next_1_hours()
    {
        var forecasts = Reader().Read(MetNorwayPayload.SavedOsloSnapshot);

        var sixHourly = forecasts.Single(f => f.ValidAt == new DateTimeOffset(2026, 9, 3, 18, 0, 0, TimeSpan.Zero));

        Assert.Equal("fair_night", sixHourly.Symbol);
        Assert.Equal(6, sixHourly.PeriodHours);
        Assert.Equal(0.0, sixHourly.PrecipitationMillimetres);
        Assert.Equal(18.1, sixHourly.TemperatureCelsius);
    }

    /// <summary>The very last step carries an instant and no period at all.</summary>
    [Fact]
    public void Reads_a_step_with_no_period_at_all_as_a_forecast_without_a_symbol()
    {
        var last = Reader().Read(MetNorwayPayload.SavedOsloSnapshot)[^1];

        Assert.Equal(10.0, last.TemperatureCelsius);
        Assert.Equal(2.3, last.WindSpeedMetresPerSecond);
        Assert.Null(last.Symbol);
        Assert.Null(last.PrecipitationMillimetres);
        Assert.Null(last.PeriodHours);
    }

    [Fact]
    public void Passes_met_norways_symbol_codes_through_untranslated()
    {
        var symbols = Reader()
            .Read(MetNorwayPayload.SavedOsloSnapshot)
            .Select(forecast => forecast.Symbol)
            .Where(symbol => symbol is not null)
            .Distinct()
            .ToList();

        Assert.NotEmpty(symbols);
        Assert.All(symbols, symbol => Assert.Matches("^[a-z_]+$", symbol!));
        Assert.Contains("cloudy", symbols);
    }

    [Fact]
    public void Reads_a_payload_with_no_timeseries_as_no_forecasts()
    {
        var forecasts = Reader().Read("""{"type":"Feature","properties":{"timeseries":[]}}""");

        Assert.Empty(forecasts);
    }

    [Fact]
    public void Refuses_a_payload_that_is_not_a_forecast_at_all()
    {
        Assert.Throws<JsonException>(() => Reader().Read("""{"type":"Feature"}"""));
    }

    private static MetNorwayPayloadReader Reader() => new(Options.Create(new MetNorwayOptions()));
}

/// <summary>A Forecast Snapshot payload as MET Norway really sent it, taken out of the store.</summary>
internal static class MetNorwayPayload
{
    public static string SavedOsloSnapshot { get; } = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "met-norway-oslo-compact.json"));
}
