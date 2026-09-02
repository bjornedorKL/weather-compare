using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Tests;

/// <summary>
/// Tracking and untracking. A Location is its coordinate, so the same point tracked twice is one
/// Location however it is named; untracking is a flag, so the row and every Forecast Snapshot
/// recorded at that coordinate survive it (ADR-0003).
/// </summary>
public class LocationTrackingTests : IDisposable
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

    private LocationTracking Tracking => new(_db, NullLogger<LocationTracking>.Instance);

    [Fact]
    public async Task Tracks_a_coordinate_we_have_never_seen()
    {
        var tracked = await TrackAsync("Oslo", 59.9139m, 10.7522m, 23);

        Assert.True(tracked.Created);
        Assert.True(tracked.Location.Tracked);
        Assert.Equal("Oslo", tracked.Location.Name);
        Assert.Equal(1, await _db.Locations.CountAsync());
    }

    /// <summary>
    /// A coordinate typed or pasted by a person is truncated to the four decimals the Provider
    /// accepts, not refused. The seed file refuses the same coordinate; that asymmetry is the
    /// point of ADR-0003.
    /// </summary>
    [Fact]
    public async Task Truncates_a_coordinate_finer_than_the_provider_accepts()
    {
        var tracked = await TrackAsync("Oslo", 59.9138683m, 10.75224799m, 23);

        Assert.Equal(59.9138m, tracked.Location.Latitude);
        Assert.Equal(10.7522m, tracked.Location.Longitude);
    }

    /// <summary>Truncation never rounds up: it would move the Location to a different point.</summary>
    [Fact]
    public async Task Truncation_never_rounds_up()
    {
        var tracked = await TrackAsync("Somewhere", 59.91389999m, 10.75229999m, 10);

        Assert.Equal(59.9138m, tracked.Location.Latitude);
        Assert.Equal(10.7522m, tracked.Location.Longitude);
    }

    /// <summary>
    /// Two entries with the same coordinate are the same Location whatever they are called
    /// (CONTEXT.md), so this adds no row — and the name already on file wins.
    /// </summary>
    [Fact]
    public async Task Tracking_a_known_coordinate_under_another_name_adds_no_row()
    {
        await TrackAsync("Oslo", 59.9139m, 10.7522m, 23);

        var again = await TrackAsync("Oslo sentrum", 59.9139m, 10.7522m, 500);

        Assert.False(again.Created);
        Assert.Equal("Oslo", again.Location.Name);
        Assert.Equal(23, again.Location.Altitude);
        Assert.Equal(1, await _db.Locations.CountAsync());
    }

    /// <summary>An over-precise coordinate that truncates onto a known one is that known one.</summary>
    [Fact]
    public async Task An_over_precise_coordinate_can_land_on_a_location_we_know()
    {
        var first = await TrackAsync("Oslo", 59.9138m, 10.7522m, 23);

        var again = await TrackAsync("Oslo again", 59.9138683m, 10.75224799m, 23);

        Assert.False(again.Created);
        Assert.Equal(first.Location.Id, again.Location.Id);
        Assert.Equal(1, await _db.Locations.CountAsync());
    }

    /// <summary>Re-tracking an untracked Location flips the flag back; it does not add a row.</summary>
    [Fact]
    public async Task Tracking_a_known_coordinate_brings_it_back_into_the_catalogue()
    {
        var first = await TrackAsync("Oslo", 59.9139m, 10.7522m, 23);
        await Tracking.SetTrackedAsync(first.Location.Id, tracked: false);

        var again = await TrackAsync("Oslo", 59.9139m, 10.7522m, 23);

        Assert.False(again.Created);
        Assert.True(again.Location.Tracked);
        Assert.Equal(1, await _db.Locations.CountAsync());
    }

    [Fact]
    public async Task Tracking_a_location_we_know_by_id_flips_the_flag_and_adds_no_row()
    {
        var first = await TrackAsync("Oslo", 59.9139m, 10.7522m, 23);
        await Tracking.SetTrackedAsync(first.Location.Id, tracked: false);

        var tracked = await Tracking.SetTrackedAsync(first.Location.Id, tracked: true);

        Assert.True(tracked!.Tracked);
        Assert.Equal(1, await _db.Locations.CountAsync());
    }

    /// <summary>
    /// Untracking freezes a Location's history; it never removes what a Provider already told us.
    /// The row survives too, so the Location can be tracked again without being described afresh.
    /// </summary>
    [Fact]
    public async Task Untracking_leaves_the_row_and_every_snapshot_alone()
    {
        var location = (await TrackAsync("Oslo", 59.9139m, 10.7522m, 23)).Location;
        AppendSnapshot(location);
        AppendSnapshot(location);

        var untracked = await Tracking.SetTrackedAsync(location.Id, tracked: false);

        Assert.False(untracked!.Tracked);
        Assert.Equal(1, await _db.Locations.CountAsync());
        Assert.Equal(2, await _db.ForecastSnapshots.CountAsync());
        Assert.All(
            _db.ForecastSnapshots,
            snapshot => Assert.Equal(location.Coordinate, (snapshot.Latitude, snapshot.Longitude)));
    }

    /// <summary>An untracked Location leaves the Catalogue but stays known, so the page can offer it back.</summary>
    [Fact]
    public async Task An_untracked_location_leaves_the_catalogue_but_stays_known()
    {
        var location = (await TrackAsync("Oslo", 59.9139m, 10.7522m, 23)).Location;
        await Tracking.SetTrackedAsync(location.Id, tracked: false);

        var catalogue = await new LocationCatalogue(_db).TrackedAsync();
        var known = await Tracking.KnownAsync();

        Assert.Empty(catalogue);
        Assert.Equal(["Oslo"], known.Select(l => l.Name));
        Assert.False(known.Single().Tracked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_id_we_do_not_know_is_not_a_silent_no_op(bool tracked)
    {
        await TrackAsync("Oslo", 59.9139m, 10.7522m, 23);

        Assert.Null(await Tracking.SetTrackedAsync(4242, tracked));
    }

    /* ---- Renaming ---- */

    /// <summary>
    /// A rename changes the label and nothing else. The coordinate is what identifies a Location,
    /// so the row that comes back is the same Location it was before (CONTEXT.md).
    /// </summary>
    [Fact]
    public async Task Renaming_changes_the_label_and_nothing_else()
    {
        var location = (await TrackAsync("Oslo", 59.9139m, 10.7522m, 23)).Location;

        var renamed = await Tracking.RenameAsync(location.Id, "Oslo sentrum");

        Assert.Equal("Oslo sentrum", renamed!.Name);
        Assert.Equal(location.Id, renamed.Id);
        Assert.Equal(location.Coordinate, renamed.Coordinate);
        Assert.Equal(23, renamed.Altitude);
        Assert.True(renamed.Tracked);
        Assert.Equal(1, await _db.Locations.CountAsync());
    }

    /// <summary>
    /// A Forecast Snapshot stores a coordinate and no name, so a rename cannot reach history even
    /// in principle. Asserted rather than assumed, because it is what makes renaming safe.
    /// </summary>
    [Fact]
    public async Task Renaming_touches_no_snapshot()
    {
        var location = (await TrackAsync("Oslo", 59.9139m, 10.7522m, 23)).Location;
        AppendSnapshot(location);
        AppendSnapshot(location);

        await Tracking.RenameAsync(location.Id, "Home");

        Assert.Equal(2, await _db.ForecastSnapshots.CountAsync());
        Assert.All(
            _db.ForecastSnapshots,
            snapshot => Assert.Equal(location.Coordinate, (snapshot.Latitude, snapshot.Longitude)));
    }

    /// <summary>An untracked Location shows on the page as one we know, so it is renamed too.</summary>
    [Fact]
    public async Task Renaming_an_untracked_location_leaves_it_untracked()
    {
        var location = (await TrackAsync("Oslo", 59.9139m, 10.7522m, 23)).Location;
        await Tracking.SetTrackedAsync(location.Id, tracked: false);

        var renamed = await Tracking.RenameAsync(location.Id, "Oslo, one day");

        Assert.Equal("Oslo, one day", renamed!.Name);
        Assert.False(renamed.Tracked);
        Assert.Empty(await new LocationCatalogue(_db).TrackedAsync());
    }

    /// <summary>
    /// Two Locations may both be "Home". Refusing that would make the name part of a Location's
    /// identity, which is exactly what CONTEXT.md says it is not; the coordinate keeps them apart.
    /// </summary>
    [Fact]
    public async Task Two_locations_are_allowed_to_share_a_name()
    {
        var first = (await TrackAsync("Oslo", 59.9139m, 10.7522m, 23)).Location;
        var second = (await TrackAsync("Bergen", 60.3913m, 5.3221m, 44)).Location;

        await Tracking.RenameAsync(first.Id, "Home");
        var renamed = await Tracking.RenameAsync(second.Id, "Home");

        Assert.Equal("Home", renamed!.Name);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await _db.Locations.CountAsync());
    }

    /// <summary>
    /// Tracking a coordinate we know keeps the name on file, as it always has. Renaming is the
    /// deliberate way to change it — that is what makes keeping the rule safe (ADR-0004).
    /// </summary>
    [Fact]
    public async Task Tracking_under_another_name_still_keeps_the_name_on_file_after_a_rename()
    {
        var location = (await TrackAsync("Oslo", 59.9139m, 10.7522m, 23)).Location;
        await Tracking.RenameAsync(location.Id, "Home");

        var again = await TrackAsync("Oslo sentrum", 59.9139m, 10.7522m, 23);

        Assert.Equal("Home", again.Location.Name);
        Assert.Equal(1, await _db.Locations.CountAsync());
    }

    [Fact]
    public async Task Renaming_an_id_we_do_not_know_is_not_a_silent_no_op()
    {
        await TrackAsync("Oslo", 59.9139m, 10.7522m, 23);

        Assert.Null(await Tracking.RenameAsync(4242, "Home"));
    }

    private async Task<TrackedLocation> TrackAsync(string name, decimal latitude, decimal longitude, int altitude)
    {
        var described = new TrackLocationRequest(name, latitude, longitude, altitude).Describe();

        Assert.Empty(described.Errors);

        return await Tracking.TrackAsync(described.Location!);
    }

    private void AppendSnapshot(Location location)
    {
        _db.ForecastSnapshots.Add(new ForecastSnapshot
        {
            Provider = "MET Norway",
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            IssuedAt = DateTimeOffset.UtcNow,
            Payload = [1, 2, 3],
        });

        _db.SaveChanges();
    }
}
