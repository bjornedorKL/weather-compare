namespace WeatherCompare.Api.Locations;

/// <summary>
/// The write path over the Catalogue. The verbs are the domain's own — a Location is tracked,
/// untracked and renamed, never added and deleted — so the routes say <c>track</c>, <c>untrack</c>
/// and <c>rename</c> rather than leaning on DELETE, which would promise a removal this store
/// cannot and must not perform, or on PATCH, which says nothing about which fields may move.
/// <para>
/// <c>GET /api/locations</c> is left exactly as it is: it answers "the Catalogue, with Forecasts",
/// which is a different question from "everything we know".
/// </para>
/// <para>
/// <c>GET /api/locations/search</c> is the exception to all of that: a read that creates nothing
/// and returns Matches, not Locations. It sits here because it is what fills the track form, and
/// it is named for what it hands back rather than borrowing the Catalogue's words (ADR-0004).
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

        locations
            .MapPost("/{id:long}/rename", RenameAsync)
            .WithName("RenameLocation")
            .WithSummary("Gives a Location we know a different label. Nothing else about it moves.");

        locations
            .MapGet("/search", SearchForMatchesAsync)
            .WithName("SearchLocations")
            .WithSummary("Candidate coordinates for a name. Returns Matches, and tracks nothing.");

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

    /// <summary>
    /// Renames any Location we know, tracked or not — the untracked ones are on the page too. The
    /// name is validated exactly as tracking validates it, and a name another Location already
    /// carries is accepted: the coordinate is identity, the name is a label (CONTEXT.md).
    /// </summary>
    private static async Task<IResult> RenameAsync(
        long id,
        RenameLocationRequest request,
        LocationTracking tracking,
        CancellationToken cancellationToken)
    {
        var described = request.Describe();

        if (described.Name is not { } name)
        {
            return Results.ValidationProblem(
                described.Errors,
                title: "That is not a name we can show a Location by.");
        }

        var renamed = await tracking.RenameAsync(id, name, cancellationToken);

        return renamed is null
            ? Results.Problem(
                title: "No such Location.",
                detail: $"Nothing we know has id {id}.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(KnownLocation.Of(renamed));
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

    /// <summary>
    /// Matches for a name, straight through to the page. 502 when the gazetteer could not answer,
    /// because that is what it is — the failure is someone else's, and the page has a fallback for
    /// it. An empty list is a search that ran and matched nothing, which is a 200.
    /// <para>
    /// A Match's coordinate goes out as the gazetteer gave it, unrounded. Tracking is what
    /// truncates a coordinate to the four decimals Providers accept
    /// (<see cref="TrackLocationRequest.Describe"/>), and doing it twice would put the rule in two
    /// places for no gain.
    /// </para>
    /// </summary>
    private static async Task<IResult> SearchForMatchesAsync(
        string? q,
        OpenMeteoGazetteer gazetteer,
        CancellationToken cancellationToken)
    {
        var name = q?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["q"] = ["Give a name to search for."] },
                title: "That is not a name we can search for.");
        }

        var search = await gazetteer.SearchAsync(name, cancellationToken);

        return search.Failure is { } failure
            ? Results.Problem(
                title: "The name search is unavailable.",
                detail: $"Searching for “{name}” failed: {failure}. A coordinate can still be typed by hand.",
                statusCode: StatusCodes.Status502BadGateway)
            : Results.Ok(search.Matches);
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
