using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Api.Locations;

/// <summary>
/// Tracking and untracking: the two writes anyone makes to <c>locations</c>. Both are flag
/// changes on a row that already exists, except the one case where a coordinate is genuinely
/// new. Nothing here deletes anything, and no Forecast Snapshot is touched either way (ADR-0003).
/// </summary>
public sealed class LocationTracking(WeatherDbContext db, ILogger<LocationTracking> logger)
{
    /// <summary>
    /// Every Location we know, tracked and untracked, oldest first. Deliberately unfiltered: the
    /// page offers "add back" as well as "currently tracked", so it needs the ones that left the
    /// Catalogue too. The <c>tracked</c> filter belongs to <see cref="LocationCatalogue"/> alone.
    /// </summary>
    public async Task<IReadOnlyList<Location>> KnownAsync(CancellationToken cancellationToken = default) =>
        await db.Locations
            .AsNoTracking()
            .OrderBy(location => location.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Puts a Location we already know into the Catalogue, or takes it out. Returns null when no
    /// Location has that id, so the caller can say so rather than silently doing nothing.
    /// Untracking keeps the row and every Snapshot recorded at that coordinate; re-tracking later
    /// resumes with an unbackfillable gap in the middle of its history.
    /// </summary>
    public async Task<Location?> SetTrackedAsync(
        long id,
        bool tracked,
        CancellationToken cancellationToken = default)
    {
        var location = await db.Locations.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (location is null)
        {
            return null;
        }

        if (location.Tracked != tracked)
        {
            location.Tracked = tracked;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "{Verb} Location {Id} ({Name})",
                tracked ? "Tracked" : "Untracked",
                location.Id,
                location.Name);
        }

        return location;
    }

    /// <summary>
    /// Tracks a described coordinate. If we already know that coordinate the row is tracked as it
    /// stands — a Location is its coordinate, so a second row would be the same Location twice
    /// (CONTEXT.md). The name and altitude already on file win over the ones just typed: the act
    /// asked for is "track this point", and quietly renaming a Location the page is showing is a
    /// surprise nobody asked for. The caller is told which Location it actually got.
    /// </summary>
    public async Task<TrackedLocation> TrackAsync(
        Location described,
        CancellationToken cancellationToken = default)
    {
        var known = await db.Locations.SingleOrDefaultAsync(
            l => l.Latitude == described.Latitude && l.Longitude == described.Longitude,
            cancellationToken);

        if (known is null)
        {
            db.Locations.Add(described);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Tracked a new Location {Id} ({Name}) at ({Latitude}, {Longitude})",
                described.Id,
                described.Name,
                described.Latitude,
                described.Longitude);

            return new TrackedLocation(described, Created: true);
        }

        if (!known.Tracked)
        {
            known.Tracked = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "({Latitude}, {Longitude}) is already known as Location {Id} ({Name}); tracked that one" +
            " rather than adding '{Typed}' beside it",
            known.Latitude,
            known.Longitude,
            known.Id,
            known.Name,
            described.Name);

        return new TrackedLocation(known, Created: false);
    }
}

/// <summary>
/// The Location now in the Catalogue, and whether tracking it added a row.
/// <see cref="Created"/> is false when the coordinate was already known — under this name or
/// any other.
/// </summary>
public sealed record TrackedLocation(Location Location, bool Created);
