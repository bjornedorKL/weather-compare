using Microsoft.Extensions.Options;

namespace WeatherCompare.Api.Polling;

/// <summary>
/// Keeps the store current by sweeping the catalogue on a cadence. It only schedules: what to
/// ask, and whether a pair is due at all, belongs to <see cref="ForecastPollingCycle"/>.
/// </summary>
public sealed class ForecastPoller(
    ForecastPollingCycle cycle,
    IOptions<ForecastPollingOptions> options,
    ILogger<ForecastPoller> logger) : BackgroundService
{
    private readonly ForecastPollingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Forecast polling is switched off; no Provider will be asked");
            return;
        }

        logger.LogInformation(
            "Forecast polling every {CycleInterval}, {Stagger} between asks; a pair is asked again " +
            "once its newest Snapshot Expires",
            _options.CycleInterval,
            _options.Stagger);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOneCycleAsync(stoppingToken);
            await WaitAsync(stoppingToken);
        }
    }

    private async Task RunOneCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            await cycle.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception e)
        {
            logger.LogError(e, "A forecast poll cycle failed outright; waiting for the next one");
        }
    }

    private async Task WaitAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_options.CycleInterval, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
