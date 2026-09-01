using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeatherCompare.Api.Providers;

namespace WeatherCompare.Tests;

public class MetNorwayProviderTests
{
    private const string CompactResponse = """{"type":"Feature","properties":{"timeseries":[]}}""";

    [Fact]
    public async Task Asks_for_compact_with_coordinates_truncated_to_four_decimals()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, CompactResponse);

        await Provider(stub).FetchAsync(59.913868m, 10.752245m, 23, null);

        Assert.Equal(
            "https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=59.9138&lon=10.7522&altitude=23",
            stub.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Identifies_itself_to_the_provider()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, CompactResponse);

        await Provider(stub).FetchAsync(59.9139m, 10.7522m, null, null);

        var userAgent = stub.LastRequest.Headers.GetValues("User-Agent").Single();
        Assert.Equal("weather-compare/0.1 github.com/bjornedorKL/weather-compare", userAgent);
    }

    [Fact]
    public async Task Omits_altitude_when_it_is_not_known()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.OK, CompactResponse);

        await Provider(stub).FetchAsync(59.9139m, 10.7522m, null, null);

        Assert.DoesNotContain("altitude", stub.LastRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task Asks_only_for_what_changed_since_the_newest_snapshot()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.NotModified);
        var lastModified = new DateTimeOffset(2026, 9, 1, 10, 23, 41, TimeSpan.Zero);

        await Provider(stub).FetchAsync(59.9139m, 10.7522m, 23, lastModified);

        Assert.Equal(lastModified, stub.LastRequest.Headers.IfModifiedSince);
    }

    [Fact]
    public async Task Keeps_the_response_verbatim_along_with_expires_and_last_modified()
    {
        var expires = new DateTimeOffset(2026, 9, 1, 11, 30, 0, TimeSpan.Zero);
        var lastModified = new DateTimeOffset(2026, 9, 1, 10, 23, 41, TimeSpan.Zero);
        var stub = StubHttpMessageHandler.Answering(
            HttpStatusCode.OK, CompactResponse, expires: expires, lastModified: lastModified);

        var fetch = await Provider(stub).FetchAsync(59.9139m, 10.7522m, 23, null);

        Assert.Equal(ForecastFetchOutcome.Fetched, fetch.Outcome);
        Assert.Equal(CompactResponse, fetch.Body);
        Assert.Equal(expires, fetch.Expires);
        Assert.Equal(lastModified, fetch.LastModified);
    }

    [Fact]
    public async Task Reports_nothing_new_on_304()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.NotModified);

        var fetch = await Provider(stub).FetchAsync(59.9139m, 10.7522m, 23, DateTimeOffset.UtcNow);

        Assert.Equal(ForecastFetchOutcome.NotModified, fetch.Outcome);
        Assert.Null(fetch.Body);
    }

    [Fact]
    public async Task Refuses_an_html_error_page_served_as_a_forecast()
    {
        var stub = StubHttpMessageHandler.Answering(
            HttpStatusCode.OK, "<html><body>503 Service Unavailable</body></html>", "text/html");

        var fetch = await Provider(stub).FetchAsync(59.9139m, 10.7522m, 23, null);

        Assert.Equal(ForecastFetchOutcome.Failed, fetch.Outcome);
        Assert.Null(fetch.Body);
    }

    [Fact]
    public async Task Fails_without_throwing_when_the_provider_answers_with_an_error()
    {
        var stub = StubHttpMessageHandler.Answering(HttpStatusCode.TooManyRequests, "slow down");

        var fetch = await Provider(stub).FetchAsync(59.9139m, 10.7522m, 23, null);

        Assert.Equal(ForecastFetchOutcome.Failed, fetch.Outcome);
        Assert.Contains("429", fetch.Failure);
    }

    [Fact]
    public async Task Fails_without_throwing_when_the_provider_cannot_be_reached()
    {
        var stub = new StubHttpMessageHandler(_ => throw new HttpRequestException("no route to host"));

        var fetch = await Provider(stub).FetchAsync(59.9139m, 10.7522m, 23, null);

        Assert.Equal(ForecastFetchOutcome.Failed, fetch.Outcome);
        Assert.Contains("no route to host", fetch.Failure);
    }

    [Fact]
    public void Is_named_after_the_provider_not_its_endpoint()
    {
        Assert.Equal("MET Norway", Provider(StubHttpMessageHandler.Answering(HttpStatusCode.OK)).Name);
    }

    /// <summary>Resolves the Provider through the real registration, over a stubbed transport.</summary>
    private static IForecastProvider Provider(StubHttpMessageHandler transport)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddForecastProviders(new ConfigurationBuilder().Build());
        services
            .AddHttpClient<IForecastProvider, MetNorwayProvider>()
            .ConfigurePrimaryHttpMessageHandler(() => transport);

        return services.BuildServiceProvider().GetRequiredService<IForecastProvider>();
    }
}
