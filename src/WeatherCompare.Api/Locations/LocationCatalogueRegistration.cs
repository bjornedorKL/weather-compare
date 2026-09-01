namespace WeatherCompare.Api.Locations;

public static class LocationCatalogueRegistration
{
    private const string DefaultFile = "Locations/locations.json";

    /// <summary>
    /// Loads the Location catalogue and shares it with everything that needs the set of
    /// Locations we track. Loading happens here, at startup, so a bad file fails the
    /// application immediately instead of at the first refresh.
    /// </summary>
    public static IServiceCollection AddLocationCatalogue(
        this IServiceCollection services,
        string? path = null)
    {
        var file = Path.Combine(AppContext.BaseDirectory, path ?? DefaultFile);

        services.AddSingleton(LocationCatalogue.LoadFromFile(file));
        services.AddHostedService<LocationCatalogueAnnouncer>();

        return services;
    }
}

/// <summary>Reports the loaded catalogue once at startup, so the log shows what we track.</summary>
internal sealed class LocationCatalogueAnnouncer(
    LocationCatalogue catalogue,
    ILogger<LocationCatalogueAnnouncer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Location catalogue loaded: {Count} Locations ({Names})",
            catalogue.Locations.Count,
            string.Join(", ", catalogue.Locations.Select(l => l.Name)));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
