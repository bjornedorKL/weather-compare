using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeatherCompare.Api.Locations;

namespace WeatherCompare.Tests;

/// <summary>
/// Searching a name. The gazetteer's twenty-field answer is trimmed to Matches here and nowhere
/// else, so this is where its shape is pinned; and every way it can let us down comes back as a
/// search that failed, never as a throw, because typing a coordinate by hand has to keep working
/// (ADR-0004).
/// </summary>
public class OpenMeteoGazetteerTests
{
    /// <summary>Open-Meteo's own answer for "bergen", cut to two results and all their fields.</summary>
    private const string BergenResponse =
        """
        {
          "results": [
            {
              "id": 3161732,
              "name": "Bergen",
              "latitude": 60.39299,
              "longitude": 5.32415,
              "elevation": 12.0,
              "feature_code": "PPLA",
              "country_code": "NO",
              "admin1_id": 3172860,
              "timezone": "Europe/Oslo",
              "population": 213585,
              "country_id": 3144096,
              "country": "Norway",
              "admin1": "Vestland",
              "admin2": "Bergen"
            },
            {
              "id": 2949186,
              "name": "Bergen",
              "latitude": 52.80968,
              "longitude": 9.96045,
              "elevation": 60.6,
              "feature_code": "PPL",
              "country_code": "DE",
              "timezone": "Europe/Berlin",
              "population": 15628,
              "country": "Germany",
              "admin1": "Lower Saxony",
              "admin2": "Celle"
            }
          ],
          "generationtime_ms": 0.7
        }
        """;

    [Fact]
    public async Task Trims_the_gazetteers_answer_to_matches()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, BergenResponse);

        var search = await Gazetteer(stub).SearchAsync("bergen");

        Assert.Null(search.Failure);
        Assert.Equal(
            [
                new Match("Bergen", "Vestland", "Norway", 12, 60.39299m, 5.32415m),
                new Match("Bergen", "Lower Saxony", "Germany", 61, 52.80968m, 9.96045m),
            ],
            search.Matches);
    }

    [Fact]
    public async Task Asks_for_ten_matches_under_the_name_that_was_searched_for()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, BergenResponse);

        await Gazetteer(stub).SearchAsync("Bodø sentrum");

        Assert.Equal(
            "https://geocoding-api.open-meteo.com/v1/search?name=Bod%C3%B8%20sentrum&count=10&language=en&format=json",
            // AbsoluteUri rather than ToString, which hands back the name unescaped and would
            // not show whether it was escaped on the way out.
            stub.LastRequest.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Identifies_itself_to_the_gazetteer()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, BergenResponse);

        await Gazetteer(stub).SearchAsync("bergen");

        var userAgent = stub.LastRequest.Headers.GetValues("User-Agent").Single();
        Assert.Equal("weather-compare/0.1 github.com/bjornedorKL/weather-compare", userAgent);
    }

    /// <summary>Nothing matched comes back without a <c>results</c> array at all, not with an empty one.</summary>
    [Fact]
    public async Task Finding_nothing_is_an_answer_not_a_failure()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, """{"generationtime_ms":0.2}""");

        var search = await Gazetteer(stub).SearchAsync("qqqqq");

        Assert.Null(search.Failure);
        Assert.Empty(search.Matches);
    }

    [Fact]
    public async Task Drops_a_result_the_gazetteer_gave_no_elevation_for()
    {
        var stub = StubHttpMessageHandler.Answering(
            HttpStatusCode.OK,
            """
            {"results":[
              {"name":"Somewhere","latitude":1.5,"longitude":2.5,"country":"Nowhere"},
              {"name":"Finse","latitude":60.6,"longitude":7.5,"elevation":1222.0,"country":"Norway"}
            ]}
            """);

        var search = await Gazetteer(stub).SearchAsync("somewhere");

        var match = Assert.Single(search.Matches);
        Assert.Equal("Finse", match.Name);
        Assert.Equal(1222, match.Elevation);
    }

    /// <summary>A gazetteer result need not say what region or country it is in; a Match says so too.</summary>
    [Fact]
    public async Task Keeps_a_match_that_has_no_region_or_country()
    {
        var stub = StubHttpMessageHandler.Answering(
            HttpStatusCode.OK,
            """{"results":[{"name":"Bouvetøya","latitude":-54.4,"longitude":3.4,"elevation":780.0,"admin1":"  "}]}""");

        var match = Assert.Single((await Gazetteer(stub).SearchAsync("bouvet")).Matches);

        Assert.Null(match.Admin1);
        Assert.Null(match.Country);
    }

    [Fact]
    public async Task Fails_without_throwing_when_the_gazetteer_answers_with_an_error()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.TooManyRequests, "slow down");

        var search = await Gazetteer(stub).SearchAsync("bergen");

        Assert.Empty(search.Matches);
        Assert.Contains("429", search.Failure);
    }

    [Fact]
    public async Task Fails_without_throwing_when_the_gazetteer_cannot_be_reached()
    {
        var stub = new StubHttpMessageHandler(_ => throw new HttpRequestException("no route to host"));

        var search = await Gazetteer(stub).SearchAsync("bergen");

        Assert.Empty(search.Matches);
        Assert.Contains("no route to host", search.Failure);
    }

    /// <summary>An error page served as a 200 is a failure, not zero Matches.</summary>
    [Fact]
    public async Task Fails_without_throwing_when_the_answer_is_not_the_gazetteers_json()
    {
        var stub = StubHttpMessageHandler.Answering(
            HttpStatusCode.OK, "<html><body>502 Bad Gateway</body></html>", "text/html");

        var search = await Gazetteer(stub).SearchAsync("bergen");

        Assert.Empty(search.Matches);
        Assert.Contains("could not read", search.Failure);
    }

    /// <summary>Resolves the gazetteer through the real registration, over a stubbed transport.</summary>
    private static OpenMeteoGazetteer Gazetteer(StubHttpMessageHandler transport)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddLocationSearch(new ConfigurationBuilder().Build());
        services
            .AddHttpClient<OpenMeteoGazetteer>()
            .ConfigurePrimaryHttpMessageHandler(() => transport);

        return services.BuildServiceProvider().GetRequiredService<OpenMeteoGazetteer>();
    }
}
