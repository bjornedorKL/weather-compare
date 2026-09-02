using WeatherCompare.Api.Locations;

namespace WeatherCompare.Tests;

/// <summary>
/// What the page sends when someone types a Location in. A bad coordinate or a missing altitude
/// is answered with what is wrong with it, not with a silent no-op — and an over-precise
/// coordinate is truncated rather than refused, unlike the same coordinate in the seed file
/// (ADR-0003).
/// </summary>
public class TrackLocationRequestTests
{
    [Fact]
    public void Reads_a_location_someone_typed()
    {
        var described = Describe(new TrackLocationRequest("  Oslo  ", 59.9139m, 10.7522m, 23));

        Assert.Empty(described.Errors);
        Assert.Equal("Oslo", described.Location!.Name);
        Assert.Equal(23, described.Location.Altitude);
        Assert.True(described.Location.Tracked);
    }

    [Fact]
    public void Truncates_an_over_precise_coordinate_instead_of_refusing_it()
    {
        var described = Describe(new TrackLocationRequest("Oslo", 59.9138683m, 10.75224799m, 23));

        Assert.Empty(described.Errors);
        Assert.Equal(59.9138m, described.Location!.Latitude);
        Assert.Equal(10.7522m, described.Location.Longitude);
    }

    [Fact]
    public void Refuses_a_location_with_no_name()
    {
        var described = Describe(new TrackLocationRequest("   ", 59.9139m, 10.7522m, 23));

        Assert.Null(described.Location);
        Assert.Contains("Name", described.Errors.Keys);
    }

    [Fact]
    public void Refuses_a_location_with_no_altitude()
    {
        var described = Describe(new TrackLocationRequest("Oslo", 59.9139m, 10.7522m, null));

        Assert.Null(described.Location);
        Assert.Contains("metres above sea level", described.Errors["Altitude"].Single());
    }

    [Theory]
    [InlineData(null, 10.7522, "Latitude")]
    [InlineData(120.0, 10.7522, "Latitude")]
    [InlineData(59.9139, null, "Longitude")]
    [InlineData(59.9139, -400.0, "Longitude")]
    public void Refuses_a_coordinate_that_is_not_a_point_on_earth(double? latitude, double? longitude, string field)
    {
        var described = Describe(new TrackLocationRequest(
            "Somewhere",
            (decimal?)latitude,
            (decimal?)longitude,
            23));

        Assert.Null(described.Location);
        Assert.Contains(field, described.Errors.Keys);
    }

    [Fact]
    public void Refuses_an_altitude_that_is_a_typo()
    {
        var described = Describe(new TrackLocationRequest("Oslo", 59.9139m, 10.7522m, 40_000));

        Assert.Null(described.Location);
        Assert.Contains("Altitude", described.Errors.Keys);
    }

    /* ---- The same name rule, read through a rename ---- */

    /// <summary>
    /// A rename carries a name and nothing else, and the name rule is the tracking one called
    /// rather than restated. These assert it is genuinely the same rule, so it cannot drift.
    /// </summary>
    [Fact]
    public void Reads_a_new_name_someone_typed()
    {
        var described = new RenameLocationRequest("  Oslo sentrum  ").Describe();

        Assert.Empty(described.Errors);
        Assert.Equal("Oslo sentrum", described.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_a_rename_to_nothing(string? typed)
    {
        var described = new RenameLocationRequest(typed).Describe();

        Assert.Null(described.Name);
        Assert.Contains("Name", described.Errors.Keys);
    }

    [Fact]
    public void Refuses_a_name_longer_than_the_column_holds()
    {
        var overlong = new string('a', TrackLocationRequest.LongestName + 1);

        Assert.Contains("Name", new RenameLocationRequest(overlong).Describe().Errors.Keys);
        Assert.Contains("Name", Describe(new TrackLocationRequest(overlong, 59.9139m, 10.7522m, 23)).Errors.Keys);
    }

    /// <summary>The longest name the column holds is a name, not a typo — on both routes.</summary>
    [Fact]
    public void Accepts_a_name_exactly_as_long_as_the_column_holds()
    {
        var longest = new string('a', TrackLocationRequest.LongestName);

        Assert.Equal(longest, new RenameLocationRequest(longest).Describe().Name);
        Assert.Equal(longest, Describe(new TrackLocationRequest(longest, 59.9139m, 10.7522m, 23)).Location!.Name);
    }

    /// <summary>Both routes say the same thing about the same bad name, because it is one rule.</summary>
    [Fact]
    public void Says_the_same_thing_about_a_bad_name_however_it_arrives()
    {
        var renaming = new RenameLocationRequest("  ").Describe().Errors["Name"];
        var tracking = Describe(new TrackLocationRequest("  ", 59.9139m, 10.7522m, 23)).Errors["Name"];

        Assert.Equal(tracking, renaming);
    }

    private static LocationDescription Describe(TrackLocationRequest request) => request.Describe();
}
