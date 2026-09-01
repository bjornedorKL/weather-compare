using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Providers;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Api.Polling;

/// <summary>
/// One sweep of the catalogue: every Provider is asked about every Location, unless the newest
/// Forecast Snapshot we hold for that pair has not Expired yet. Asks are spread out rather than
/// fired at once, and a Provider or a Location falling over never stops the sweep.
/// </summary>
public sealed class ForecastPollingCycle(
    IServiceScopeFactory scopes,
    LocationCatalogue catalogue,
    IOptions<ForecastPollingOptions> options,
    ILogger<ForecastPollingCycle> logger)
{
    private readonly ForecastPollingOptions _options = options.Value;

    public async Task<PollingCycleTally> RunAsync(CancellationToken cancellationToken)
    {
        var tally = new PollingCycleTally();
        var clock = Stopwatch.StartNew();

        foreach (var (providerName, location) in Pairs())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await RefreshAsync(providerName, location, tally, cancellationToken);
        }

        logger.LogInformation(
            "Forecast poll cycle over {Pairs} (Provider, Location) pairs in {Seconds:0.0}s: " +
            "{Appended} Snapshots appended, {NotModified} not modified (304), {Failed} failed, " +
            "{StillFresh} still fresh so not asked",
            tally.Asked + tally.StillFresh,
            clock.Elapsed.TotalSeconds,
            tally.Appended,
            tally.NotModified,
            tally.Failed,
            tally.StillFresh);

        return tally;
    }

    /// <summary>Every (Provider, Location) pair the sweep covers.</summary>
    private IEnumerable<(string ProviderName, Location Location)> Pairs() =>
        ProviderNames().SelectMany(_ => catalogue.Locations, (name, location) => (name, location));

    private async Task RefreshAsync(
        string providerName,
        Location location,
        PollingCycleTally tally,
        CancellationToken cancellationToken)
    {
        // The poller is a singleton, so the DbContext behind one refresh is resolved here — per
        // unit of work — and disposed with the scope rather than captured for the process's life.
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;

        try
        {
            if (await IsStillFreshAsync(services.GetRequiredService<WeatherDbContext>(), providerName, location, cancellationToken))
            {
                logger.LogDebug(
                    "Not asking {Provider} about {Location}: our newest Forecast Snapshot has not Expired",
                    providerName,
                    location.Name);
                tally.CountStillFresh();
                return;
            }

            await Task.Delay(_options.Stagger, cancellationToken);

            var result = await services.GetRequiredService<ForecastSnapshotRecorder>().RefreshAsync(
                Provider(services, providerName),
                location.Latitude,
                location.Longitude,
                location.Altitude,
                cancellationToken);

            Count(result, providerName, location, tally);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            // One Provider or one Location falling over is not a reason to abandon the rest.
            tally.CountFailed();
            logger.LogError(e, "Asking {Provider} about {Location} failed", providerName, location.Name);
        }
    }

    /// <summary>
    /// The Provider says when to ask again: we do not re-ask a pair before the newest Snapshot's
    /// Expires has lapsed. A Provider that said nothing is trusted for <c>AssumedFreshness</c>.
    /// </summary>
    private async Task<bool> IsStillFreshAsync(
        WeatherDbContext db,
        string providerName,
        Location location,
        CancellationToken cancellationToken)
    {
        var latitude = CoordinatePrecision.Truncate(location.Latitude);
        var longitude = CoordinatePrecision.Truncate(location.Longitude);

        var newest = await db.ForecastSnapshots
            .Where(s => s.Provider == providerName && s.Latitude == latitude && s.Longitude == longitude)
            .OrderByDescending(s => s.IssuedAt)
            .Select(s => new { s.IssuedAt, s.Expires })
            .FirstOrDefaultAsync(cancellationToken);

        if (newest is null)
        {
            return false;
        }

        return (newest.Expires ?? newest.IssuedAt + _options.AssumedFreshness) > DateTimeOffset.UtcNow;
    }

    private void Count(
        ForecastRefreshResult result,
        string providerName,
        Location location,
        PollingCycleTally tally)
    {
        switch (result.Outcome)
        {
            case ForecastRefreshOutcome.SnapshotAppended:
                tally.CountAppended();
                break;
            case ForecastRefreshOutcome.NotModified:
                tally.CountNotModified();
                break;
            default:
                tally.CountFailed();
                logger.LogWarning(
                    "{Provider} had nothing usable for {Location}: {Failure}",
                    providerName,
                    location.Name,
                    result.Failure);
                break;
        }
    }

    private IReadOnlyList<string> ProviderNames()
    {
        using var scope = scopes.CreateScope();
        return scope.ServiceProvider.GetServices<IForecastProvider>().Select(p => p.Name).ToList();
    }

    private static IForecastProvider Provider(IServiceProvider services, string name) =>
        services.GetServices<IForecastProvider>().First(p => p.Name == name);
}
