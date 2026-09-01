using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Tests;

/// <summary>
/// The store's one rule and its one exception. A Forecast Snapshot is what a Provider told us at
/// a moment, so it is appended and never touched again (ADR-0001). A Location is not: untracking
/// one updates its row (ADR-0003). The guard names the entity it protects, and these tests hold
/// it to both halves of that — Snapshots still refused, Locations still allowed.
/// </summary>
public class WeatherDbContextTests : IDisposable
{
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
    public void Refuses_to_update_a_forecast_snapshot()
    {
        var snapshot = AppendSnapshot();

        _db.Entry(snapshot).Property(s => s.Provider).CurrentValue = "Someone Else";

        var error = Assert.Throws<InvalidOperationException>(() => _db.SaveChanges());
        Assert.Contains("append-only", error.Message);
        Assert.Contains("Modified", error.Message);
    }

    [Fact]
    public async Task Refuses_to_update_a_forecast_snapshot_asynchronously()
    {
        var snapshot = AppendSnapshot();

        _db.Entry(snapshot).Property(s => s.Expires).CurrentValue = DateTimeOffset.UtcNow;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());
        Assert.Contains("append-only", error.Message);
    }

    [Fact]
    public void Refuses_to_delete_a_forecast_snapshot()
    {
        var snapshot = AppendSnapshot();

        _db.ForecastSnapshots.Remove(snapshot);

        var error = Assert.Throws<InvalidOperationException>(() => _db.SaveChanges());
        Assert.Contains("append-only", error.Message);
        Assert.Contains("Deleted", error.Message);
    }

    [Fact]
    public async Task Refuses_to_delete_a_forecast_snapshot_asynchronously()
    {
        var snapshot = AppendSnapshot();

        _db.ForecastSnapshots.Remove(snapshot);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());
    }

    /// <summary>
    /// A Snapshot must not be smuggled past the guard alongside a change the store does allow.
    /// </summary>
    [Fact]
    public void Refuses_a_snapshot_change_saved_together_with_a_location_change()
    {
        var snapshot = AppendSnapshot();
        var oslo = AddLocation("Oslo", 59.9139m, 10.7522m);

        oslo.Tracked = false;
        _db.ForecastSnapshots.Remove(snapshot);

        Assert.Throws<InvalidOperationException>(() => _db.SaveChanges());
        Assert.Equal(1, _db.ForecastSnapshots.Count());
    }

    [Fact]
    public void Appending_a_forecast_snapshot_is_always_allowed()
    {
        AppendSnapshot();
        AppendSnapshot();

        Assert.Equal(2, _db.ForecastSnapshots.Count());
    }

    /// <summary>Untracking is an update to the Location's row; the guard must not stand in its way.</summary>
    [Fact]
    public void Allows_untracking_a_location()
    {
        var oslo = AddLocation("Oslo", 59.9139m, 10.7522m);

        oslo.Tracked = false;
        _db.SaveChanges();

        Assert.False(_db.Locations.Single().Tracked);
    }

    [Fact]
    public void Allows_tracking_a_location_again()
    {
        var oslo = AddLocation("Oslo", 59.9139m, 10.7522m);
        oslo.Tracked = false;
        _db.SaveChanges();

        oslo.Tracked = true;
        _db.SaveChanges();

        Assert.True(_db.Locations.Single().Tracked);
    }

    /// <summary>
    /// A Location arrives untracked only if we say so; the column's default must not overwrite it.
    /// </summary>
    [Fact]
    public void Stores_a_location_added_untracked_as_untracked()
    {
        _db.Locations.Add(new Location
        {
            Name = "Oslo",
            Latitude = 59.9139m,
            Longitude = 10.7522m,
            Altitude = 23,
            Tracked = false,
        });
        _db.SaveChanges();

        Assert.False(_db.Locations.Single().Tracked);
    }

    private ForecastSnapshot AppendSnapshot()
    {
        var snapshot = new ForecastSnapshot
        {
            Provider = "MET Norway",
            Latitude = 59.9139m,
            Longitude = 10.7522m,
            IssuedAt = DateTimeOffset.UtcNow,
            Payload = GzipPayload.Compress("""{"properties":{"timeseries":[]}}"""),
        };

        _db.ForecastSnapshots.Add(snapshot);
        _db.SaveChanges();

        return snapshot;
    }

    private Location AddLocation(string name, decimal latitude, decimal longitude)
    {
        var location = new Location
        {
            Name = name,
            Latitude = latitude,
            Longitude = longitude,
            Altitude = 23,
        };

        _db.Locations.Add(location);
        _db.SaveChanges();

        return location;
    }
}
