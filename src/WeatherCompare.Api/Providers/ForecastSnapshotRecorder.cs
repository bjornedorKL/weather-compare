using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Api.Providers;

/// <summary>
/// Refreshes what we hold for one Provider at one coordinate: asks the Provider what it says
/// now, and appends a Forecast Snapshot if it said something new. A refresh is an append, never
/// an update (ADR-0001) — and a 304 appends nothing at all.
/// </summary>
public class ForecastSnapshotRecorder(WeatherDbContext db, ILogger<ForecastSnapshotRecorder> logger)
{
    public async Task<ForecastRefreshResult> RefreshAsync(
        IForecastProvider provider,
        decimal latitude,
        decimal longitude,
        int? altitude = null,
        CancellationToken cancellationToken = default)
    {
        var lat = CoordinatePrecision.Truncate(latitude);
        var lon = CoordinatePrecision.Truncate(longitude);

        var knownLastModified = await NewestLastModifiedAsync(provider.Name, lat, lon, cancellationToken);
        var fetch = await provider.FetchAsync(lat, lon, altitude, knownLastModified, cancellationToken);

        return fetch.Outcome switch
        {
            ForecastFetchOutcome.NotModified => new ForecastRefreshResult(ForecastRefreshOutcome.NotModified),
            ForecastFetchOutcome.Failed => new ForecastRefreshResult(
                ForecastRefreshOutcome.Failed,
                Failure: fetch.Failure),
            _ => await AppendAsync(provider.Name, lat, lon, fetch, cancellationToken),
        };
    }

    private async Task<ForecastRefreshResult> AppendAsync(
        string provider,
        decimal latitude,
        decimal longitude,
        ForecastFetch fetch,
        CancellationToken cancellationToken)
    {
        // The Provider's response goes in verbatim: normalising into Forecasts happens on read.
        var snapshot = new ForecastSnapshot
        {
            Provider = provider,
            Latitude = latitude,
            Longitude = longitude,
            IssuedAt = DateTimeOffset.UtcNow,
            Payload = GzipPayload.Compress(fetch.Body!),
            Expires = fetch.Expires,
            LastModified = fetch.LastModified,
        };

        db.ForecastSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Appended a Forecast Snapshot from {Provider} for {Latitude},{Longitude} ({Bytes} bytes gzipped)",
            provider,
            latitude,
            longitude,
            snapshot.Payload.Length);

        return new ForecastRefreshResult(
            ForecastRefreshOutcome.SnapshotAppended,
            snapshot.Id,
            snapshot.Payload.Length);
    }

    /// <summary>
    /// The <c>Last-Modified</c> of the newest Snapshot we hold, which becomes the next request's
    /// <c>If-Modified-Since</c>.
    /// </summary>
    private Task<DateTimeOffset?> NewestLastModifiedAsync(
        string provider,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken) =>
        db.ForecastSnapshots
            .Where(s => s.Provider == provider && s.Latitude == latitude && s.Longitude == longitude)
            .OrderByDescending(s => s.IssuedAt)
            .Select(s => s.LastModified)
            .FirstOrDefaultAsync(cancellationToken);
}
