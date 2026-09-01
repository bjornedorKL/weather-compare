namespace WeatherCompare.Api.Forecasts;

public static class ForecastEndpoints
{
    /// <summary>
    /// Registers the read path: one payload reader per Provider we can read, plus the reader
    /// that turns the newest Forecast Snapshots into what the page renders.
    /// </summary>
    public static IServiceCollection AddForecastReading(this IServiceCollection services)
    {
        services.AddSingleton<IForecastPayloadReader, MetNorwayPayloadReader>();
        services.AddScoped<LocationForecastReader>();

        return services;
    }

    public static IEndpointRouteBuilder MapForecastEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/api/locations", async (LocationForecastReader reader, CancellationToken cancellationToken) =>
                Results.Ok(await reader.ReadAsync(cancellationToken)))
            .WithName("GetLocations")
            .WithSummary("Every Location we track, with the Forecasts from each Provider's newest Snapshot.");

        return endpoints;
    }
}
