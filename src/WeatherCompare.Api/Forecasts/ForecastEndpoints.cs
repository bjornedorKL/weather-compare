using System.Globalization;

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
        services.AddScoped<ForecastHistoryReader>();

        return services;
    }

    public static IEndpointRouteBuilder MapForecastEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/api/locations", async (LocationForecastReader reader, CancellationToken cancellationToken) =>
                Results.Ok(await reader.ReadAsync(cancellationToken)))
            .WithName("GetLocations")
            .WithSummary("Every Location we track, with the Forecasts from each Provider's newest Snapshot.");

        // A second question about the same Locations, and a route of its own rather than a shape
        // bolted onto the one above: "the Catalogue with its newest Forecasts" and "what one
        // Location's successive Snapshots said about one moment" are not the same read, and the
        // second is far more expensive. Nothing about the existing route changes.
        endpoints
            .MapGet("/api/locations/{id:long}/history", ReadHistoryAsync)
            .WithName("GetForecastHistory")
            .WithSummary("What successive Forecast Snapshots predicted for one moment at one Location.");

        return endpoints;
    }

    /// <summary>
    /// 400 when no moment was named — this read is about a moment, and there is no sensible
    /// default one — and 404 when nothing tracked has that id.
    /// </summary>
    private static async Task<IResult> ReadHistoryAsync(
        long id,
        string? validAt,
        int? limit,
        ForecastHistoryReader reader,
        CancellationToken cancellationToken)
    {
        if (!TryParseMoment(validAt, out var moment))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["validAt"] =
                    [
                        "Name the moment to look at, as an ISO 8601 instant such as " +
                        "2026-09-03T12:00:00Z. A Forecast is a statement about one moment, so " +
                        "there is nothing to show without one.",
                    ],
                },
                title: "That is not a moment we can look up.");
        }

        var history = await reader.ReadAsync(
            id,
            moment,
            limit ?? ForecastHistoryReader.DefaultSnapshots,
            cancellationToken);

        return history is null
            ? Results.Problem(
                title: "No such Location in the Catalogue.",
                detail: $"Nothing we track has id {id}.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(history);
    }

    private static bool TryParseMoment(string? validAt, out DateTimeOffset moment) =>
        DateTimeOffset.TryParse(
            validAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out moment);
}
