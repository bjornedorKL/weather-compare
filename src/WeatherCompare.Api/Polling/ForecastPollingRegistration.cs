namespace WeatherCompare.Api.Polling;

public static class ForecastPollingRegistration
{
    /// <summary>
    /// Registers the sweep that keeps the store current, and the background service that runs it
    /// on a cadence. Both are singletons: the DbContext behind each ask is scoped per refresh.
    /// </summary>
    public static IServiceCollection AddForecastPolling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForecastPollingOptions>(configuration.GetSection(ForecastPollingOptions.Section));
        services.AddSingleton<ForecastPollingCycle>();
        services.AddHostedService<ForecastPoller>();

        return services;
    }
}
