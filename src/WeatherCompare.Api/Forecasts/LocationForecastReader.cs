using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Api.Forecasts;

/// <summary>
/// The read path: newest Forecast Snapshot per (Provider, Location), decompressed and read into
/// Forecasts. Normalisation happens here rather than at write time, so a mapping bug is fixed by
/// changing code and reading the stored Snapshots again (ADR-0001).
/// </summary>
public sealed class LocationForecastReader(
    WeatherDbContext db,
    LocationCatalogue catalogue,
    IEnumerable<IForecastPayloadReader> payloadReaders,
    ILogger<LocationForecastReader> logger)
{
    private readonly IReadOnlyDictionary<string, IForecastPayloadReader> _readers =
        payloadReaders.ToDictionary(reader => reader.Provider, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every Location in the Catalogue. Most have never been asked about, and a Location with
    /// no Snapshot is a Location with no Forecasts — not a failure.
    /// </summary>
    public async Task<IReadOnlyList<LocationForecasts>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var locations = await catalogue.TrackedAsync(cancellationToken);
        var newest = await NewestSnapshotsAsync(cancellationToken);

        return locations.Select(location => Read(location, newest)).ToList();
    }

    private LocationForecasts Read(Location location, ILookup<long, ForecastSnapshot> newest)
    {
        var snapshots = newest[location.Id]
            .GroupBy(snapshot => snapshot.Provider, StringComparer.OrdinalIgnoreCase)
            .Select(perProvider => perProvider.MaxBy(snapshot => snapshot.IssuedAt)!)
            .Select(ToForecasts)
            .OfType<SnapshotForecasts>()
            .OrderBy(snapshot => snapshot.Provider, StringComparer.Ordinal)
            .ToList();

        return new LocationForecasts(
            location.Name,
            location.Latitude,
            location.Longitude,
            location.Altitude,
            snapshots);
    }

    private SnapshotForecasts? ToForecasts(ForecastSnapshot snapshot)
    {
        if (!_readers.TryGetValue(snapshot.Provider, out var reader))
        {
            logger.LogWarning(
                "Nothing can read Forecast Snapshots from {Provider}; Snapshot {Snapshot} is not shown",
                snapshot.Provider,
                snapshot.Id);

            return null;
        }

        try
        {
            var forecasts = reader.Read(GzipPayload.Decompress(snapshot.Payload));

            return new SnapshotForecasts(snapshot.Provider, snapshot.IssuedAt.ToUniversalTime(), forecasts);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            // One unreadable Snapshot is not a reason to fail the whole page; the Location shows
            // as having nothing, and the Snapshot stays in the store to be read again after a fix.
            logger.LogWarning(
                e,
                "Forecast Snapshot {Snapshot} from {Provider} could not be read",
                snapshot.Id,
                snapshot.Provider);

            return null;
        }
    }

    /// <summary>
    /// The newest Snapshot each Provider holds for each Location in the Catalogue, keyed by
    /// Location. Now that Locations are rows, this is a join: the Catalogue and the Snapshots
    /// are matched on coordinate in the database, so a Snapshot taken at a coordinate we no
    /// longer track never leaves it (ADR-0003). Only the newest Snapshots' payloads are read.
    /// </summary>
    private async Task<ILookup<long, ForecastSnapshot>> NewestSnapshotsAsync(CancellationToken cancellationToken)
    {
        var newestPerPair = db.ForecastSnapshots
            .GroupBy(snapshot => new { snapshot.Provider, snapshot.Latitude, snapshot.Longitude })
            .Select(pair => new
            {
                pair.Key.Provider,
                pair.Key.Latitude,
                pair.Key.Longitude,
                IssuedAt = pair.Max(snapshot => snapshot.IssuedAt),
            });

        var query =
            from location in catalogue.Tracked
            join pair in newestPerPair
                on new { location.Latitude, location.Longitude }
                equals new { pair.Latitude, pair.Longitude }
            join snapshot in db.ForecastSnapshots.AsNoTracking()
                on new { pair.Provider, pair.Latitude, pair.Longitude, pair.IssuedAt }
                equals new { snapshot.Provider, snapshot.Latitude, snapshot.Longitude, snapshot.IssuedAt }
            select new { LocationId = location.Id, Snapshot = snapshot };

        var rows = await query.ToListAsync(cancellationToken);

        return rows.ToLookup(row => row.LocationId, row => row.Snapshot);
    }
}
