using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherCompare.Api.Forecasts;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Providers;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Tests;

public class LocationForecastReaderTests : IDisposable
{
    private const string Catalogue =
        """
        [
          { "name": "Oslo",  "lat": 59.9139, "lon": 10.7522, "altitude": 23 },
          { "name": "Finse", "lat": 60.6022, "lon": 7.5000,  "altitude": 1222 }
        ]
        """;

    private readonly WeatherDbContext _db = new(
        new DbContextOptionsBuilder<WeatherDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Reads_the_newest_snapshot_for_a_location_into_forecasts()
    {
        Append("MET Norway", 59.9139m, 10.7522m, IssuedAt(11, 46), MetNorwayPayload.SavedOsloSnapshot);

        var oslo = (await Reader().ReadAsync()).Single(l => l.Name == "Oslo");

        var snapshot = Assert.Single(oslo.Snapshots);
        Assert.Equal("MET Norway", snapshot.Provider);
        Assert.Equal(IssuedAt(11, 46), snapshot.IssuedAt);
        Assert.Equal(86, snapshot.Forecasts.Count);
        Assert.Equal(17.6, snapshot.Forecasts[0].TemperatureCelsius);
        Assert.Equal("cloudy", snapshot.Forecasts[0].Symbol);
    }

    /// <summary>
    /// Most of the Locations we track have never been asked about. That is a Location with no
    /// Forecasts, not a failure.
    /// </summary>
    [Fact]
    public async Task Reads_a_location_with_no_snapshot_as_empty()
    {
        Append("MET Norway", 59.9139m, 10.7522m, IssuedAt(11, 46), MetNorwayPayload.SavedOsloSnapshot);

        var finse = (await Reader().ReadAsync()).Single(l => l.Name == "Finse");

        Assert.Empty(finse.Snapshots);
        Assert.Equal(60.6022m, finse.Latitude);
        Assert.Equal(1222, finse.Altitude);
    }

    [Fact]
    public async Task Reads_every_location_in_the_catalogue_even_when_nothing_is_stored()
    {
        var locations = await Reader().ReadAsync();

        Assert.Equal(["Oslo", "Finse"], locations.Select(l => l.Name));
        Assert.All(locations, location => Assert.Empty(location.Snapshots));
    }

    /// <summary>A refresh appends, so a Location accumulates Snapshots; the page shows the newest.</summary>
    [Fact]
    public async Task Shows_the_newest_snapshot_when_a_location_has_several()
    {
        Append("MET Norway", 59.9139m, 10.7522m, IssuedAt(9, 0), """{"properties":{"timeseries":[]}}""");
        Append("MET Norway", 59.9139m, 10.7522m, IssuedAt(11, 46), MetNorwayPayload.SavedOsloSnapshot);

        var oslo = (await Reader().ReadAsync()).Single(l => l.Name == "Oslo");

        var snapshot = Assert.Single(oslo.Snapshots);
        Assert.Equal(IssuedAt(11, 46), snapshot.IssuedAt);
        Assert.NotEmpty(snapshot.Forecasts);
    }

    [Fact]
    public async Task Ignores_snapshots_taken_at_coordinates_we_no_longer_track()
    {
        Append("MET Norway", 63.4305m, 10.3951m, IssuedAt(11, 46), MetNorwayPayload.SavedOsloSnapshot);

        var locations = await Reader().ReadAsync();

        Assert.All(locations, location => Assert.Empty(location.Snapshots));
    }

    /// <summary>One unreadable Snapshot must not take the whole page down with it.</summary>
    [Fact]
    public async Task Reads_a_location_whose_newest_snapshot_is_unreadable_as_empty()
    {
        Append("MET Norway", 59.9139m, 10.7522m, IssuedAt(11, 46), "<html>MET is having a bad day</html>");

        var oslo = (await Reader().ReadAsync()).Single(l => l.Name == "Oslo");

        Assert.Empty(oslo.Snapshots);
    }

    [Fact]
    public async Task Skips_snapshots_from_a_provider_nothing_can_read()
    {
        Append("Some Other Provider", 59.9139m, 10.7522m, IssuedAt(11, 46), """{"whatever":true}""");

        var oslo = (await Reader().ReadAsync()).Single(l => l.Name == "Oslo");

        Assert.Empty(oslo.Snapshots);
    }

    private static DateTimeOffset IssuedAt(int hour, int minute) =>
        new(2026, 9, 1, hour, minute, 0, TimeSpan.Zero);

    private void Append(string provider, decimal latitude, decimal longitude, DateTimeOffset issuedAt, string payload)
    {
        _db.ForecastSnapshots.Add(new ForecastSnapshot
        {
            Provider = provider,
            Latitude = latitude,
            Longitude = longitude,
            IssuedAt = issuedAt,
            Payload = GzipPayload.Compress(payload),
        });

        _db.SaveChanges();
    }

    private LocationForecastReader Reader() =>
        new(
            _db,
            LocationCatalogue.Parse(Catalogue),
            [new MetNorwayPayloadReader(Options.Create(new MetNorwayOptions()))],
            NullLogger<LocationForecastReader>.Instance);
}
