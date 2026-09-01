using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherCompare.Api.Forecasts;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Polling;
using WeatherCompare.Api.Providers;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Tests;

/// <summary>
/// The read the append-only store exists for: what successive Forecast Snapshots said about one
/// future moment. Every other read takes the newest Snapshot; this one walks them all.
/// <para>
/// Nothing here compares a Forecast to what the weather actually did. That would need an
/// Observation, which this system does not have and CONTEXT.md deliberately does not define.
/// </para>
/// </summary>
public class ForecastHistoryReaderTests : IDisposable
{
    private const string Catalogue =
        """
        [
          { "name": "Oslo",  "lat": 59.9139, "lon": 10.7522, "altitude": 23 },
          { "name": "Finse", "lat": 60.6022, "lon": 7.5000,  "altitude": 1222 }
        ]
        """;

    /// <summary>The moment every test asks about: 18:00 on the day the Snapshots were Issued.</summary>
    private static readonly DateTimeOffset Moment = new(2026, 9, 1, 18, 0, 0, TimeSpan.Zero);

    private readonly WeatherDbContext _db = new(
        new DbContextOptionsBuilder<WeatherDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public ForecastHistoryReaderTests()
    {
        _db.Locations.AddRange(LocationSeedFile.Parse(Catalogue));
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>The whole point: a Provider disagreeing with its own earlier self.</summary>
    [Fact]
    public async Task Reads_what_successive_snapshots_said_about_one_moment()
    {
        AppendOslo(At(11, 46), Says(18.2));
        AppendOslo(At(12, 20), Says(16.9));
        AppendOslo(At(12, 56), Says(15.4));

        var history = await Read();

        Assert.Equal("Oslo", history.Name);
        Assert.Equal(Moment, history.ValidAt);
        Assert.Equal(3, history.SnapshotsRead);

        var provider = Assert.Single(history.Providers);
        Assert.Equal("MET Norway", provider.Provider);
        Assert.Equal([At(11, 46), At(12, 20), At(12, 56)], provider.Points.Select(p => p.IssuedAt));
        Assert.Equal([18.2, 16.9, 15.4], provider.Points.Select(p => p.Forecast!.TemperatureCelsius));
    }

    /// <summary>
    /// A Forecast for 12:00 is a statement about 12:00. Offering it as an answer about 18:00
    /// because it is the nearest one would be inventing what the Provider said.
    /// </summary>
    [Fact]
    public async Task Reads_a_snapshot_that_described_no_forecast_for_the_moment_as_silent()
    {
        AppendOslo(At(11, 46), Payload([(Hour(12), 21.0), (Hour(0), 11.0)]));
        AppendOslo(At(12, 20), Says(16.9));

        var provider = Assert.Single((await Read()).Providers);

        Assert.Null(provider.Points[0].Forecast);
        Assert.Equal(16.9, provider.Points[1].Forecast!.TemperatureCelsius);
    }

    /// <summary>A Snapshot taken after the moment had passed was not predicting it.</summary>
    [Fact]
    public async Task Leaves_out_snapshots_issued_after_the_moment()
    {
        AppendOslo(At(12, 20), Says(16.9));
        AppendOslo(At(19, 30), Says(15.0));

        var history = await Read();

        Assert.Equal(1, history.SnapshotsRead);
        Assert.Equal([At(12, 20)], Assert.Single(history.Providers).Points.Select(p => p.IssuedAt));
    }

    /// <summary>
    /// The poller runs locally, so the record only covers the periods the machine was awake. A
    /// Snapshot that came due and never arrived is a gap, and a gap can never be backfilled.
    /// </summary>
    [Fact]
    public async Task Names_a_stretch_where_a_snapshot_was_due_and_none_was_recorded()
    {
        AppendOslo(At(11, 46), Says(18.2), expires: At(12, 17));
        AppendOslo(At(12, 20), Says(16.9), expires: At(12, 52));
        AppendOslo(At(14, 19), Says(15.4), expires: At(14, 49));

        var provider = Assert.Single((await Read()).Providers);

        var gap = Assert.Single(provider.Gaps);
        Assert.Equal(At(12, 20), gap.FromIssuedAt);
        Assert.Equal(At(14, 19), gap.ToIssuedAt);
        Assert.Equal(At(12, 52), gap.DueAt);
    }

    /// <summary>
    /// A Provider answering <c>304 Not Modified</c> appends nothing, and a Snapshot coming due
    /// mid-sweep is picked up by the next one. Neither is a hole in the record.
    /// </summary>
    [Fact]
    public async Task Names_no_gap_when_snapshots_arrive_on_the_providers_own_cadence()
    {
        AppendOslo(At(11, 46), Says(18.2), expires: At(12, 17));
        AppendOslo(At(12, 20), Says(16.9), expires: At(12, 52));
        AppendOslo(At(12, 56), Says(15.4), expires: At(13, 28));

        Assert.Empty(Assert.Single((await Read()).Providers).Gaps);
    }

    /// <summary>Without an <c>Expires</c> the Provider is assumed due after the configured freshness.</summary>
    [Fact]
    public async Task Falls_back_to_the_assumed_freshness_when_the_provider_never_said_when_to_ask_again()
    {
        AppendOslo(At(11, 46), Says(18.2));
        AppendOslo(At(13, 30), Says(15.4));

        var gap = Assert.Single(Assert.Single((await Read()).Providers).Gaps);

        Assert.Equal(At(12, 16), gap.DueAt);
    }

    /// <summary>
    /// A newly tracked Location has one Snapshot and nothing to compare it with. One answer is
    /// not a Forecast moving, and the read must not dress it up as one.
    /// </summary>
    [Fact]
    public async Task Reads_a_location_with_a_single_snapshot_as_one_point_and_no_gaps()
    {
        AppendOslo(At(12, 20), Says(16.9));

        var history = await Read();

        Assert.Equal(1, history.SnapshotsRead);

        var provider = Assert.Single(history.Providers);
        Assert.Single(provider.Points);
        Assert.Empty(provider.Gaps);
    }

    [Fact]
    public async Task Reads_a_location_nothing_has_been_recorded_for_as_no_providers_at_all()
    {
        var history = await Read();

        Assert.Equal(0, history.SnapshotsRead);
        Assert.Empty(history.Providers);
    }

    [Fact]
    public async Task Reads_nothing_for_a_location_we_do_not_know()
    {
        Assert.Null(await Reader().ReadAsync(9999, Moment));
    }

    /// <summary>
    /// Untracking freezes a Location's history rather than deleting it, but it is out of the
    /// Catalogue, and the Catalogue is all the page ever reads (ADR-0003).
    /// </summary>
    [Fact]
    public async Task Reads_nothing_for_an_untracked_location_whose_snapshots_survive()
    {
        AppendOslo(At(12, 20), Says(16.9));
        _db.Locations.Single(l => l.Name == "Oslo").Tracked = false;
        await _db.SaveChangesAsync();

        Assert.Null(await Reader().ReadAsync(IdOf("Oslo"), Moment));
        Assert.Equal(1, await _db.ForecastSnapshots.CountAsync());
    }

    /// <summary>
    /// This is the first read that touches more than one row per (Provider, Location), so what
    /// it costs is bounded on the way in: the newest Snapshots are kept and the rest never leave
    /// the database.
    /// </summary>
    [Fact]
    public async Task Never_reads_more_snapshots_than_it_was_asked_for()
    {
        for (var minute = 0; minute < 10; minute++)
        {
            AppendOslo(At(12, minute), Says(16 + minute));
        }

        var history = await Reader().ReadAsync(IdOf("Oslo"), Moment, limit: 3);

        Assert.Equal(3, history!.SnapshotsRead);
        Assert.Equal([At(12, 7), At(12, 8), At(12, 9)], history.Providers[0].Points.Select(p => p.IssuedAt));
    }

    [Fact]
    public async Task Reads_only_the_snapshots_taken_at_this_locations_coordinate()
    {
        AppendOslo(At(12, 20), Says(16.9));
        Append("MET Norway", 60.6022m, 7.5000m, At(12, 20), Says(3.1));

        var history = await Read();

        Assert.Equal(1, history.SnapshotsRead);
        Assert.Equal(16.9, history.Providers[0].Points[0].Forecast!.TemperatureCelsius);
    }

    /// <summary>One unreadable Snapshot is a Snapshot that says nothing, not a failed read.</summary>
    [Fact]
    public async Task Reads_an_unreadable_snapshot_as_silent()
    {
        AppendOslo(At(11, 46), "<html>MET is having a bad day</html>");
        AppendOslo(At(12, 20), Says(16.9));

        var provider = Assert.Single((await Read()).Providers);

        Assert.Null(provider.Points[0].Forecast);
        Assert.Equal(16.9, provider.Points[1].Forecast!.TemperatureCelsius);
    }

    /// <summary>
    /// The Provider's own payload reader does the normalising, exactly as the newest-Snapshot
    /// read does — there is no second parser for history (ADR-0001).
    /// </summary>
    [Fact]
    public async Task Reads_a_real_met_norway_payload_with_the_providers_own_reader()
    {
        AppendOslo(At(11, 46), MetNorwayPayload.SavedOsloSnapshot);

        var forecast = Assert.Single((await Read()).Providers).Points[0].Forecast;

        Assert.NotNull(forecast);
        Assert.Equal(Moment, forecast.ValidAt);
        Assert.NotNull(forecast.Symbol);
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 9, 1, hour, minute, 0, TimeSpan.Zero);

    /// <summary>An ISO instant on the day under test, or the next day for hours already past.</summary>
    private static string Hour(int hour) =>
        hour >= 11
            ? $"2026-09-01T{hour:00}:00:00Z"
            : $"2026-09-02T{hour:00}:00:00Z";

    /// <summary>A payload whose only Forecast is for the moment every test asks about.</summary>
    private static string Says(double celsius) => Payload([(Hour(18), celsius)]);

    /// <summary>MET's <c>compact</c> shape, cut down to what the read actually looks at.</summary>
    private static string Payload((string Time, double Celsius)[] steps)
    {
        var json = new StringBuilder("""{"properties":{"timeseries":[""");

        for (var i = 0; i < steps.Length; i++)
        {
            json.Append(i == 0 ? string.Empty : ",")
                .Append("{\"time\":\"").Append(steps[i].Time).Append("\",")
                .Append("\"data\":{\"instant\":{\"details\":{\"air_temperature\":")
                .Append(steps[i].Celsius.ToString(CultureInfo.InvariantCulture))
                .Append(",\"wind_speed\":3.2}},")
                .Append("\"next_1_hours\":{\"summary\":{\"symbol_code\":\"cloudy\"},")
                .Append("\"details\":{\"precipitation_amount\":0.0}}}}");
        }

        return json.Append("]}}").ToString();
    }

    private long IdOf(string name) => _db.Locations.Single(l => l.Name == name).Id;

    /// <summary>Oslo's history for the moment under test. Every test but two asks exactly this.</summary>
    private async Task<ForecastHistory> Read()
    {
        var history = await Reader().ReadAsync(IdOf("Oslo"), Moment);

        Assert.NotNull(history);

        return history;
    }

    private void AppendOslo(DateTimeOffset issuedAt, string payload, DateTimeOffset? expires = null) =>
        Append("MET Norway", 59.9139m, 10.7522m, issuedAt, payload, expires);

    private void Append(
        string provider,
        decimal latitude,
        decimal longitude,
        DateTimeOffset issuedAt,
        string payload,
        DateTimeOffset? expires = null)
    {
        _db.ForecastSnapshots.Add(new ForecastSnapshot
        {
            Provider = provider,
            Latitude = latitude,
            Longitude = longitude,
            IssuedAt = issuedAt,
            Payload = GzipPayload.Compress(payload),
            Expires = expires,
        });

        _db.SaveChanges();
    }

    private ForecastHistoryReader Reader() =>
        new(
            _db,
            new LocationCatalogue(_db),
            [new MetNorwayPayloadReader(Options.Create(new MetNorwayOptions()))],
            Options.Create(new ForecastPollingOptions()),
            NullLogger<ForecastHistoryReader>.Instance);
}
