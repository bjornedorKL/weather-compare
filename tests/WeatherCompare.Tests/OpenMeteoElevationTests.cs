using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeatherCompare.Api.Locations;

namespace WeatherCompare.Tests;

/// <summary>
/// Looking a coordinate's height up. This is the only altitude the "use my location" route has —
/// the browser's own reading is height above the ellipsoid and is never read (ADR-0004) — so what
/// is pinned here is that a height is either the model's or absent, never invented, and that every
/// way the model can let us down comes back as a lookup that failed rather than as a throw. A
/// Location can still be tracked with a typed altitude when it does.
/// </summary>
public class OpenMeteoElevationTests
{
    /// <summary>Open-Meteo's own answer, which is this and nothing else.</summary>
    private const string BergenResponse = """{"elevation":[38.0]}""";

    [Fact]
    public async Task Reads_the_height_out_of_the_models_answer()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, BergenResponse);

        var lookup = await Elevation(stub).AtAsync(60.3929m, 5.3241m);

        Assert.Null(lookup.Failure);
        Assert.Equal(38, lookup.Metres);
    }

    /// <summary>Whole metres, as everything in the domain is. 60.6 m is 61, not 60.</summary>
    [Fact]
    public async Task Rounds_the_height_to_whole_metres()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, """{"elevation":[1222.5]}""");

        Assert.Equal(1223, (await Elevation(stub).AtAsync(60.6m, 7.5m)).Metres);
    }

    /// <summary>
    /// Asked for at the four decimals a Location is identified by, so the height belongs to the
    /// point that would be tracked and not to a finer one nothing stores.
    /// </summary>
    [Fact]
    public async Task Asks_about_the_coordinate_at_the_precision_a_location_is_tracked_at()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, BergenResponse);

        await Elevation(stub).AtAsync(60.39299123m, -5.32415987m);

        Assert.Equal(
            "https://api.open-meteo.com/v1/elevation?latitude=60.3929&longitude=-5.3241",
            stub.LastRequest.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Identifies_itself_to_the_model()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, BergenResponse);

        await Elevation(stub).AtAsync(60.3929m, 5.3241m);

        var userAgent = stub.LastRequest.Headers.GetValues("User-Agent").Single();
        Assert.Equal("weather-compare/0.1 github.com/bjornedorKL/weather-compare", userAgent);
    }

    /// <summary>
    /// No height is a failure, not a Location at sea level: zero metres is a real altitude, so a
    /// stand-in would be indistinguishable from a reading and would wrong the temperature.
    /// </summary>
    [Theory]
    [InlineData("""{"elevation":[]}""")]
    [InlineData("""{"generationtime_ms":0.02}""")]
    [InlineData("""{"elevation":null}""")]
    public async Task Fails_rather_than_defaulting_when_the_answer_carries_no_height(string body)
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, body);

        var lookup = await Elevation(stub).AtAsync(60.3929m, 5.3241m);

        Assert.Null(lookup.Metres);
        Assert.Contains("no height", lookup.Failure);
    }

    [Fact]
    public async Task Fails_without_throwing_when_the_model_answers_with_an_error()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.TooManyRequests, "slow down");

        var lookup = await Elevation(stub).AtAsync(60.3929m, 5.3241m);

        Assert.Null(lookup.Metres);
        Assert.Contains("429", lookup.Failure);
    }

    [Fact]
    public async Task Fails_without_throwing_when_the_model_cannot_be_reached()
    {
        var stub = new StubHttpMessageHandler(_ => throw new HttpRequestException("no route to host"));

        var lookup = await Elevation(stub).AtAsync(60.3929m, 5.3241m);

        Assert.Null(lookup.Metres);
        Assert.Contains("no route to host", lookup.Failure);
    }

    /// <summary>An error page served as a 200 is a failure, not a height.</summary>
    [Fact]
    public async Task Fails_without_throwing_when_the_answer_is_not_the_models_json()
    {
        var stub = StubHttpMessageHandler.Answering(
            HttpStatusCode.OK, "<html><body>502 Bad Gateway</body></html>", "text/html");

        var lookup = await Elevation(stub).AtAsync(60.3929m, 5.3241m);

        Assert.Null(lookup.Metres);
        Assert.Contains("could not read", lookup.Failure);
    }

    /// <summary>Resolves the client through the real registration, over a stubbed transport.</summary>
    private static OpenMeteoElevation Elevation(StubHttpMessageHandler transport)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddLocationElevation(new ConfigurationBuilder().Build());
        services
            .AddHttpClient<OpenMeteoElevation>()
            .ConfigurePrimaryHttpMessageHandler(() => transport);

        return services.BuildServiceProvider().GetRequiredService<OpenMeteoElevation>();
    }
}
