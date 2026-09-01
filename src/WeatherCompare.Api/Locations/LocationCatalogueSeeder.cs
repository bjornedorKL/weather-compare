using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Api.Locations;

/// <summary>
/// Puts the hand-written seed file into <c>locations</c> the first time the application runs
/// against an empty table. Every later run finds the coordinates already there and writes
/// nothing — including the ones someone has untracked, which must not come back to life
/// (ADR-0003). The table is the truth once seeded; the file is never read for changes.
/// </summary>
public sealed class LocationCatalogueSeeder(
    WeatherDbContext db,
    ILogger<LocationCatalogueSeeder> logger)
{
    public async Task SeedAsync(
        IReadOnlyList<Location> seed,
        CancellationToken cancellationToken = default)
    {
        var known = await KnownCoordinatesAsync(cancellationToken);
        var missing = seed.Where(location => !known.Contains(location.Coordinate)).ToList();

        if (missing.Count == 0)
        {
            logger.LogInformation(
                "Location seed applied already: all {Count} seeded coordinates are known",
                seed.Count);

            return;
        }

        db.Locations.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {Count} Locations into the Catalogue ({Names})",
            missing.Count,
            string.Join(", ", missing.Select(location => location.Name)));
    }

    /// <summary>
    /// Every coordinate the table already describes, tracked or not. A Location is its
    /// coordinate, so this is what makes seeding idempotent.
    /// </summary>
    private async Task<HashSet<(decimal Latitude, decimal Longitude)>> KnownCoordinatesAsync(
        CancellationToken cancellationToken)
    {
        var coordinates = await db.Locations
            .AsNoTracking()
            .Select(location => new { location.Latitude, location.Longitude })
            .ToListAsync(cancellationToken);

        return coordinates
            .Select(c => (decimal.Round(c.Latitude, LocationSeedFile.CoordinateDecimals),
                          decimal.Round(c.Longitude, LocationSeedFile.CoordinateDecimals)))
            .ToHashSet();
    }
}
