using WeatherCompare.Api.Locations;

namespace WeatherCompare.Tests;

public class LocationSeedFileTests
{
    private const string Oslo = """{ "name": "Oslo", "lat": 59.9139, "lon": 10.7522, "altitude": 23 }""";

    [Fact]
    public void Reads_a_location()
    {
        var location = Assert.Single(LocationSeedFile.Parse($"[{Oslo}]"));

        Assert.Equal("Oslo", location.Name);
        Assert.Equal(59.9139m, location.Latitude);
        Assert.Equal(10.7522m, location.Longitude);
        Assert.Equal(23, location.Altitude);
    }

    /// <summary>A seeded Location enters the Catalogue tracked; untracking is a later, deliberate act.</summary>
    [Fact]
    public void Reads_a_location_as_tracked()
    {
        Assert.True(Assert.Single(LocationSeedFile.Parse($"[{Oslo}]")).Tracked);
    }

    [Theory]
    [InlineData("""[{ "name": "Oslo", "lat": 59.91387, "lon": 10.7522, "altitude": 23 }]""")]
    [InlineData("""[{ "name": "Oslo", "lat": 59.9139, "lon": 10.752245, "altitude": 23 }]""")]
    public void Rejects_a_coordinate_finer_than_four_decimals(string json)
    {
        var error = Assert.Throws<LocationCatalogueException>(() => LocationSeedFile.Parse(json));

        Assert.Contains("more than 4 decimals", error.Message);
        Assert.Contains("Oslo", error.Message);
    }

    [Fact]
    public void Accepts_a_coordinate_at_exactly_four_decimals()
    {
        var location = Assert.Single(LocationSeedFile.Parse($"[{Oslo}]"));

        Assert.All(
            new[] { location.Latitude, location.Longitude },
            c => Assert.Equal(c, decimal.Round(c, LocationSeedFile.CoordinateDecimals)));
    }

    [Fact]
    public void Every_seeded_location_is_within_the_precision_met_norway_allows()
    {
        foreach (var location in LoadSeedFile())
        {
            Assert.Equal(decimal.Round(location.Latitude, 4), location.Latitude);
            Assert.Equal(decimal.Round(location.Longitude, 4), location.Longitude);
        }
    }

    [Fact]
    public void Rejects_two_locations_sharing_a_coordinate()
    {
        var json = $$"""
            [
              {{Oslo}},
              { "name": "Oslo S", "lat": 59.9139, "lon": 10.7522, "altitude": 3 }
            ]
            """;

        var error = Assert.Throws<LocationCatalogueException>(() => LocationSeedFile.Parse(json));

        Assert.Contains("duplicate coordinates", error.Message);
        Assert.Contains("'Oslo'", error.Message);
        Assert.Contains("'Oslo S'", error.Message);
    }

    [Fact]
    public void Rejects_an_altitude_that_is_not_whole_metres()
    {
        var json = """[{ "name": "Oslo", "lat": 59.9139, "lon": 10.7522, "altitude": 23.5 }]""";

        Assert.Throws<LocationCatalogueException>(() => LocationSeedFile.Parse(json));
    }

    [Fact]
    public void Rejects_an_empty_seed_file()
    {
        Assert.Throws<LocationCatalogueException>(() => LocationSeedFile.Parse("[]"));
    }

    [Fact]
    public void Seed_file_loads_and_holds_distinct_coordinates()
    {
        var locations = LoadSeedFile();

        Assert.InRange(locations.Count, 15, 30);
        Assert.Equal(locations.Count, locations.Select(l => l.Coordinate).Distinct().Count());
        Assert.Contains(locations, l => l.Name == "Oslo");
        Assert.Contains(locations, l => l.Name == "Tromsø");
    }

    private static IReadOnlyList<Location> LoadSeedFile() =>
        LocationSeedFile.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Locations", "locations.json"));
}
