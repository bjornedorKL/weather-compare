using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Api.Locations;

/// <summary>The hand-written seed file, read once at startup so a bad file fails immediately.</summary>
public sealed record LocationSeed(IReadOnlyList<Location> Locations);

public static class LocationCatalogueRegistration
{
    private const string DefaultFile = "Locations/locations.json";

    /// <summary>
    /// Registers the Catalogue and the seed it starts from. The Catalogue is scoped, not shared:
    /// it is a query over <c>locations</c>, and that table changes while we run (ADR-0003).
    /// </summary>
    public static IServiceCollection AddLocationCatalogue(
        this IServiceCollection services,
        string? path = null)
    {
        var file = Path.Combine(AppContext.BaseDirectory, path ?? DefaultFile);

        services.AddSingleton(new LocationSeed(LocationSeedFile.LoadFromFile(file)));
        services.AddScoped<LocationCatalogue>();
        services.AddScoped<LocationCatalogueSeeder>();
        services.AddHostedService<LocationCatalogueAnnouncer>();

        return services;
    }

    /// <summary>
    /// Brings the store up to date and applies the seed. Both run before anything serves or
    /// polls, and seeding is idempotent, so a later run adds nothing and resurrects nothing.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<WeatherDbContext>().Database.MigrateAsync();

        await scope.ServiceProvider
            .GetRequiredService<LocationCatalogueSeeder>()
            .SeedAsync(scope.ServiceProvider.GetRequiredService<LocationSeed>().Locations);
    }
}

/// <summary>Reports the Catalogue once at startup, so the log shows what we track.</summary>
internal sealed class LocationCatalogueAnnouncer(
    IServiceScopeFactory scopes,
    ILogger<LocationCatalogueAnnouncer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // The Catalogue is scoped, so it is resolved here rather than held for the process's life.
        await using var scope = scopes.CreateAsyncScope();

        var locations = await scope.ServiceProvider
            .GetRequiredService<LocationCatalogue>()
            .TrackedAsync(cancellationToken);

        logger.LogInformation(
            "Location catalogue: {Count} tracked Locations ({Names})",
            locations.Count,
            string.Join(", ", locations.Select(l => l.Name)));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
