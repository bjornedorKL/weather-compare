using WeatherCompare.Api.Locations;

namespace WeatherCompare.Tests;

public class LocationCatalogueTests
{
    private const string Oslo = """{ "name": "Oslo", "lat": 59.9139, "lon": 10.7522, "altitude": 23 }""";

    [Fact]
    public void Reads_a_location()
    {
        var location = Assert.Single(LocationCatalogue.Parse($"[{Oslo}]").Locations);

        Assert.Equal("Oslo", location.Name);
        Assert.Equal(59.9139m, location.Latitude);
        Assert.Equal(10.7522m, location.Longitude);
        Assert.Equal(23, location.Altitude);
    }

    [Theory]
    [InlineData("""[{ "name": "Oslo", "lat": 59.91387, "lon": 10.7522, "altitude": 23 }]""")]
    [InlineData("""[{ "name": "Oslo", "lat": 59.9139, "lon": 10.752245, "altitude": 23 }]""")]
    public void Rejects_a_coordinate_finer_than_four_decimals(string json)
    {
        var error = Assert.Throws<LocationCatalogueException>(() => LocationCatalogue.Parse(json));

        Assert.Contains("more than 4 decimals", error.Message);
        Assert.Contains("Oslo", error.Message);
    }

    [Fact]
    public void Accepts_a_coordinate_at_exactly_four_decimals()
    {
        var location = Assert.Single(LocationCatalogue.Parse($"[{Oslo}]").Locations);

        Assert.All(
            new[] { location.Latitude, location.Longitude },
            c => Assert.Equal(c, decimal.Round(c, LocationCatalogue.CoordinateDecimals)));
    }

    [Fact]
    public void Every_seeded_location_is_within_the_precision_met_norway_allows()
    {
        foreach (var location in LoadSeededCatalogue().Locations)
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

        var error = Assert.Throws<LocationCatalogueException>(() => LocationCatalogue.Parse(json));

        Assert.Contains("duplicate coordinates", error.Message);
        Assert.Contains("'Oslo'", error.Message);
        Assert.Contains("'Oslo S'", error.Message);
    }

    [Fact]
    public void Rejects_an_altitude_that_is_not_whole_metres()
    {
        var json = """[{ "name": "Oslo", "lat": 59.9139, "lon": 10.7522, "altitude": 23.5 }]""";

        Assert.Throws<LocationCatalogueException>(() => LocationCatalogue.Parse(json));
    }

    [Fact]
    public void Rejects_an_empty_catalogue()
    {
        Assert.Throws<LocationCatalogueException>(() => LocationCatalogue.Parse("[]"));
    }

    [Fact]
    public void Seeded_catalogue_loads_and_holds_distinct_coordinates()
    {
        var catalogue = LoadSeededCatalogue();

        Assert.InRange(catalogue.Locations.Count, 15, 30);
        Assert.Equal(
            catalogue.Locations.Count,
            catalogue.Locations.Select(l => l.Coordinate).Distinct().Count());
        Assert.Contains(catalogue.Locations, l => l.Name == "Oslo");
        Assert.Contains(catalogue.Locations, l => l.Name == "Tromsø");
    }

    private static LocationCatalogue LoadSeededCatalogue() =>
        LocationCatalogue.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Locations", "locations.json"));
}
