using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Api.Locations;

/// <summary>
/// The set of Locations we currently track: the rows of <c>locations</c> that are tracked.
/// Scoped, not a singleton — the Catalogue changes while the application runs, so nothing may
/// hold on to one (ADR-0003).
/// </summary>
public sealed class LocationCatalogue(WeatherDbContext db)
{
    /// <summary>
    /// The Catalogue as a query, so callers can join Forecast Snapshots to it in the database
    /// rather than pulling it into memory first. The <c>tracked</c> filter lives here and only
    /// here: an untracked Location must never reach a Provider or the page.
    /// </summary>
    public IQueryable<Location> Tracked =>
        db.Locations
            .AsNoTracking()
            .Where(location => location.Tracked)
            .OrderBy(location => location.Id);

    /// <summary>The Catalogue, in the order Locations entered it.</summary>
    public async Task<IReadOnlyList<Location>> TrackedAsync(CancellationToken cancellationToken = default) =>
        await Tracked.ToListAsync(cancellationToken);
}
