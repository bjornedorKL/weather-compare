using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Polling;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Api.Forecasts;

/// <summary>
/// Reads what successive Forecast Snapshots said about one future moment at one Location.
/// <para>
/// This is the first read path that touches more than one row per (Provider, Location), and it
/// decompresses and parses every one of them (ADR-0001 accepted exactly this cost). It is kept
/// affordable by narrowing rather than by caching: one Location, only Snapshots Issued before
/// the moment — a Snapshot taken afterwards was not predicting anything — and never more than
/// <see cref="MaximumSnapshots"/> of them.
/// </para>
/// </summary>
public sealed class ForecastHistoryReader(
    WeatherDbContext db,
    LocationCatalogue catalogue,
    IEnumerable<IForecastPayloadReader> payloadReaders,
    IOptions<ForecastPollingOptions> polling,
    ILogger<ForecastHistoryReader> logger)
{
    /// <summary>Roughly two days at MET's half-hourly Expires: enough to watch a Forecast move.</summary>
    public const int DefaultSnapshots = 100;

    /// <summary>The ceiling on one read, whatever is asked for.</summary>
    public const int MaximumSnapshots = 500;

    private readonly IReadOnlyDictionary<string, IForecastPayloadReader> _readers =
        payloadReaders.ToDictionary(reader => reader.Provider, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The history for one Location in the Catalogue, or null when nothing tracked has that id.
    /// An untracked Location's Snapshots survive, but they are not the page's to show (ADR-0003).
    /// </summary>
    public async Task<ForecastHistory?> ReadAsync(
        long locationId,
        DateTimeOffset validAt,
        int limit = DefaultSnapshots,
        CancellationToken cancellationToken = default)
    {
        var location = await catalogue.Tracked
            .FirstOrDefaultAsync(candidate => candidate.Id == locationId, cancellationToken);

        if (location is null)
        {
            return null;
        }

        var moment = validAt.ToUniversalTime();
        var snapshots = await NewestBeforeAsync(location, moment, limit, cancellationToken);
        var started = Stopwatch.GetTimestamp();

        var providers = snapshots
            .GroupBy(snapshot => snapshot.Provider, StringComparer.OrdinalIgnoreCase)
            .Select(perProvider => ReadProvider(perProvider.Key, perProvider.ToList(), moment))
            .OrderBy(provider => provider.Provider, StringComparer.Ordinal)
            .ToList();

        logger.LogInformation(
            "Read {Count} Forecast Snapshots for {Location} about {ValidAt:o}, decompressed and parsed in {Elapsed} ms",
            snapshots.Count,
            location.Name,
            moment,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return new ForecastHistory(
            location.Name,
            location.Latitude,
            location.Longitude,
            location.Altitude,
            moment,
            snapshots.Count,
            providers);
    }

    /// <summary>
    /// The Snapshots to read: this Location's coordinate, Issued before the moment, newest first
    /// so the bound keeps the most recent, then reversed because oldest first is the order a
    /// Forecast moves in. Only the payloads that survive the bound ever leave the database.
    /// </summary>
    private async Task<List<ForecastSnapshot>> NewestBeforeAsync(
        Location location,
        DateTimeOffset moment,
        int limit,
        CancellationToken cancellationToken)
    {
        var newest = await db.ForecastSnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.Latitude == location.Latitude &&
                snapshot.Longitude == location.Longitude &&
                snapshot.IssuedAt < moment)
            .OrderByDescending(snapshot => snapshot.IssuedAt)
            .Take(Math.Clamp(limit, 1, MaximumSnapshots))
            .ToListAsync(cancellationToken);

        newest.Reverse();

        return newest;
    }

    private ProviderForecastHistory ReadProvider(
        string provider,
        IReadOnlyList<ForecastSnapshot> snapshots,
        DateTimeOffset moment)
    {
        var points = snapshots
            .Select(snapshot => new ForecastHistoryPoint(
                snapshot.IssuedAt.ToUniversalTime(),
                ForecastFor(snapshot, moment)))
            .ToList();

        return new ProviderForecastHistory(provider, points, GapsIn(snapshots));
    }

    /// <summary>
    /// What this Snapshot said about exactly this moment, or null when it said nothing about it —
    /// either because the Provider's steps had lengthened past it, or because the payload cannot
    /// be read at all, which is one bad Snapshot and not a failed page.
    /// </summary>
    private Forecast? ForecastFor(ForecastSnapshot snapshot, DateTimeOffset moment)
    {
        if (!_readers.TryGetValue(snapshot.Provider, out var reader))
        {
            return null;
        }

        try
        {
            return reader
                .Read(GzipPayload.Decompress(snapshot.Payload))
                .FirstOrDefault(forecast => forecast.ValidAt == moment);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            logger.LogWarning(
                e,
                "Forecast Snapshot {Snapshot} from {Provider} could not be read",
                snapshot.Id,
                snapshot.Provider);

            return null;
        }
    }

    /// <summary>
    /// Where the record fell silent: a Snapshot said when to ask again, the sweep that would have
    /// asked never ran, and the next Snapshot arrived far later. The grace is two polling cycles,
    /// because a Snapshot coming due mid-sweep is picked up by the next one; beyond that, no
    /// sweep happened, and a stretch with no sweep can never be filled in afterwards.
    /// </summary>
    private List<ForecastHistoryGap> GapsIn(IReadOnlyList<ForecastSnapshot> snapshots)
    {
        var options = polling.Value;
        var grace = options.CycleInterval * 2;
        var gaps = new List<ForecastHistoryGap>();

        for (var i = 1; i < snapshots.Count; i++)
        {
            var before = snapshots[i - 1];
            var after = snapshots[i];
            var dueAt = before.Expires ?? before.IssuedAt + options.AssumedFreshness;

            if (after.IssuedAt > dueAt + grace)
            {
                gaps.Add(new ForecastHistoryGap(
                    before.IssuedAt.ToUniversalTime(),
                    after.IssuedAt.ToUniversalTime(),
                    dueAt.ToUniversalTime()));
            }
        }

        return gaps;
    }
}
