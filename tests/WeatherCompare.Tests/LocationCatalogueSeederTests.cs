using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Tests;

/// <summary>
/// The seed file is applied on first run and never again: the table is the truth afterwards
/// (ADR-0003). Seeding twice must add nothing, and must not bring back a Location someone
/// deliberately untracked.
/// </summary>
public class LocationCatalogueSeederTests
{
    private const string TwoLocations =
        """
        [
          { "name": "Oslo",   "lat": 59.9139, "lon": 10.7522, "altitude": 23 },
          { "name": "Bergen", "lat": 60.3913, "lon": 5.3221,  "altitude": 12 }
        ]
        """;

    private readonly string _database = Guid.NewGuid().ToString();

    [Fact]
    public async Task Seeds_an_empty_table()
    {
        await SeedAsync(TwoLocations);

        await using var db = Db();
        Assert.Equal(["Oslo", "Bergen"], db.Locations.OrderBy(l => l.Id).Select(l => l.Name));
        Assert.All(db.Locations, location => Assert.True(location.Tracked));
    }

    [Fact]
    public async Task Seeding_twice_adds_nothing()
    {
        await SeedAsync(TwoLocations);
        await SeedAsync(TwoLocations);

        await using var db = Db();
        Assert.Equal(2, await db.Locations.CountAsync());
    }

    /// <summary>
    /// The common case: someone untracks a Location, the application restarts, and the seed must
    /// leave it alone. Resurrecting it would silently start asking Providers about it again.
    /// </summary>
    [Fact]
    public async Task Does_not_resurrect_an_untracked_location()
    {
        await SeedAsync(TwoLocations);
        await UntrackAsync("Oslo");

        await SeedAsync(TwoLocations);

        await using var db = Db();
        Assert.Equal(2, await db.Locations.CountAsync());
        Assert.False(db.Locations.Single(l => l.Name == "Oslo").Tracked);
    }

    /// <summary>A renamed Location is the same Location — identity is the coordinate.</summary>
    [Fact]
    public async Task Leaves_a_renamed_location_alone()
    {
        await SeedAsync(TwoLocations);
        await RenameAsync("Oslo", "Oslo sentrum");

        await SeedAsync(TwoLocations);

        await using var db = Db();
        Assert.Equal(2, await db.Locations.CountAsync());
        Assert.Contains(db.Locations, l => l.Name == "Oslo sentrum");
    }

    private async Task SeedAsync(string seed)
    {
        await using var db = Db();

        await new LocationCatalogueSeeder(db, NullLogger<LocationCatalogueSeeder>.Instance)
            .SeedAsync(LocationSeedFile.Parse(seed));
    }

    private async Task UntrackAsync(string name)
    {
        await using var db = Db();

        db.Locations.Single(l => l.Name == name).Tracked = false;
        await db.SaveChangesAsync();
    }

    private async Task RenameAsync(string from, string to)
    {
        await using var db = Db();

        var location = db.Locations.Single(l => l.Name == from);
        db.Entry(location).Property(l => l.Name).CurrentValue = to;
        await db.SaveChangesAsync();
    }

    private WeatherDbContext Db() =>
        new(new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(_database).Options);
}
