namespace WeatherCompare.Api.Locations;

/// <summary>
/// The write path over the Catalogue. The verbs are the domain's own — a Location is tracked and
/// untracked, never added and deleted — so the routes say <c>track</c> and <c>untrack</c> rather
/// than leaning on DELETE, which would promise a removal this store cannot and must not perform.
/// <para>
/// <c>GET /api/locations</c> is left exactly as it is: it answers "the Catalogue, with Forecasts",
/// which is a different question from "everything we know".
/// </para>
/// </summary>
public static class LocationEndpoints
{
    public static IServiceCollection AddLocationTracking(this IServiceCollection services)
    {
        services.AddScoped<LocationTracking>();

        return services;
    }

    public static IEndpointRouteBuilder MapLocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var locations = endpoints.MapGroup("/api/locations");

        locations
            .MapGet("/known", async (LocationTracking tracking, CancellationToken cancellationToken) =>
            {
                var known = await tracking.KnownAsync(cancellationToken);

                return Results.Ok(known.Select(KnownLocation.Of).ToList());
            })
            .WithName("GetKnownLocations")
            .WithSummary("Every Location we know, tracked and untracked, so the page can offer both.");

        locations
            .MapPost("", TrackCoordinateAsync)
            .WithName("TrackLocation")
            .WithSummary("Tracks a typed coordinate. A coordinate we already know is tracked as it stands.");

        locations
            .MapPost("/{id:long}/track", (long id, LocationTracking tracking, CancellationToken cancellationToken) =>
                SetTrackedAsync(id, tracked: true, tracking, cancellationToken))
            .WithName("TrackKnownLocation")
            .WithSummary("Puts a Location we already know back into the Catalogue. Adds no row.");

        locations
            .MapPost("/{id:long}/untrack", (long id, LocationTracking tracking, CancellationToken cancellationToken) =>
                SetTrackedAsync(id, tracked: false, tracking, cancellationToken))
            .WithName("UntrackLocation")
            .WithSummary("Takes a Location out of the Catalogue. The row survives and no Snapshot is touched.");

        return endpoints;
    }

    /// <summary>
    /// 201 when the coordinate was new, 200 when we already knew it — under this name or another.
    /// The body is the Location the Catalogue now holds, so a caller whose name lost to the one on
    /// file can see that it did.
    /// </summary>
    private static async Task<IResult> TrackCoordinateAsync(
        TrackLocationRequest request,
        LocationTracking tracking,
        CancellationToken cancellationToken)
    {
        var described = request.Describe();

        if (described.Location is not { } wanted)
        {
            return Results.ValidationProblem(
                described.Errors,
                title: "That is not a Location we can track.");
        }

        var tracked = await tracking.TrackAsync(wanted, cancellationToken);
        var body = KnownLocation.Of(tracked.Location);

        return tracked.Created
            ? Results.Json(body, statusCode: StatusCodes.Status201Created)
            : Results.Ok(body);
    }

    private static async Task<IResult> SetTrackedAsync(
        long id,
        bool tracked,
        LocationTracking tracking,
        CancellationToken cancellationToken)
    {
        var location = await tracking.SetTrackedAsync(id, tracked, cancellationToken);

        return location is null
            ? Results.Problem(
                title: "No such Location.",
                detail: $"Nothing we know has id {id}.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(KnownLocation.Of(location));
    }
}

/// <summary>
/// A Location we know, and whether it is in the Catalogue. The id is how the page names it when
/// tracking or untracking, so it does not have to spell the coordinate out.
/// </summary>
public sealed record KnownLocation(
    long Id,
    string Name,
    decimal Latitude,
    decimal Longitude,
    int Altitude,
    bool Tracked)
{
    public static KnownLocation Of(Location location) =>
        new(
            location.Id,
            location.Name,
            location.Latitude,
            location.Longitude,
            location.Altitude,
            location.Tracked);
}
