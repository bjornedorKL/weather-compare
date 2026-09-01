using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Polling;
using WeatherCompare.Api.Providers;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Tests;

public class ForecastPollingCycleTests : IDisposable
{
    private const string CompactResponse =
        """{"type":"Feature","properties":{"meta":{"updated_at":"2026-09-01T10:23:41Z"},"timeseries":[]}}""";

    private const string TwoLocations =
        """
        [
          { "name": "Oslo",   "lat": 59.9139, "lon": 10.7522, "altitude": 23 },
          { "name": "Bergen", "lat": 60.3913, "lon": 5.3221,  "altitude": 12 }
        ]
        """;

    private readonly string _database = Guid.NewGuid().ToString();
    private readonly List<IForecastProvider> _providers = [];
    private ServiceProvider? _services;

    public void Dispose()
    {
        _services?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Carries_on_to_the_next_pair_when_a_provider_throws()
    {
        var falling = new StubForecastProvider("Falls Over", _ => throw new InvalidOperationException("boom"));
        var healthy = new StubForecastProvider("MET Norway", _ => Fetched());
        _providers.AddRange([falling, healthy]);

        var tally = await Cycle().RunAsync(CancellationToken.None);

        Assert.Equal(2, tally.Failed);
        Assert.Equal(2, tally.Appended);
        Assert.Equal(2, healthy.Asked.Count);
        Assert.Equal(2, await SnapshotCountAsync());
    }

    [Fact]
    public async Task Does_not_ask_again_before_the_newest_snapshot_expires()
    {
        var provider = new StubForecastProvider("MET Norway", _ => Fetched());
        _providers.Add(provider);
        await SeedSnapshotAsync("MET Norway", 59.9139m, 10.7522m, expires: DateTimeOffset.UtcNow.AddMinutes(25));

        var tally = await Cycle().RunAsync(CancellationToken.None);

        Assert.Equal(1, tally.StillFresh);
        Assert.Equal(1, tally.Appended);
        Assert.Equal([(60.3913m, 5.3221m)], provider.Asked);
    }

    [Fact]
    public async Task Asks_again_once_the_newest_snapshot_has_expired()
    {
        var provider = new StubForecastProvider("MET Norway", _ => Fetched());
        _providers.Add(provider);
        await SeedSnapshotAsync("MET Norway", 59.9139m, 10.7522m, expires: DateTimeOffset.UtcNow.AddMinutes(-1));

        var tally = await Cycle().RunAsync(CancellationToken.None);

        Assert.Equal(0, tally.StillFresh);
        Assert.Equal(2, tally.Appended);
    }

    [Fact]
    public async Task Treats_a_snapshot_without_an_expires_as_fresh_for_the_assumed_freshness()
    {
        var provider = new StubForecastProvider("MET Norway", _ => Fetched());
        _providers.Add(provider);
        await SeedSnapshotAsync("MET Norway", 59.9139m, 10.7522m, expires: null);

        var tally = await Cycle().RunAsync(CancellationToken.None);

        Assert.Equal(1, tally.StillFresh);
        Assert.Equal([(60.3913m, 5.3221m)], provider.Asked);
    }

    [Fact]
    public async Task Counts_a_provider_that_answered_304_separately_from_one_that_failed()
    {
        _providers.Add(new StubForecastProvider("MET Norway", _ => ForecastFetch.NotModified()));
        _providers.Add(new StubForecastProvider("Sulking", _ => ForecastFetch.Failed("answered 503")));

        var tally = await Cycle().RunAsync(CancellationToken.None);

        Assert.Equal(2, tally.NotModified);
        Assert.Equal(2, tally.Failed);
        Assert.Equal(0, tally.Appended);
        Assert.Equal(0, await SnapshotCountAsync());
    }

    [Fact]
    public async Task Stops_asking_once_cancelled()
    {
        var provider = new StubForecastProvider("MET Norway", _ => Fetched());
        _providers.Add(provider);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var tally = await Cycle().RunAsync(cancelled.Token);

        Assert.Empty(provider.Asked);
        Assert.Equal(0, tally.Asked);
    }

    private static ForecastFetch Fetched() =>
        ForecastFetch.Fetched(CompactResponse, DateTimeOffset.UtcNow.AddMinutes(30), DateTimeOffset.UtcNow);

    private ForecastPollingCycle Cycle()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<WeatherDbContext>(o => o.UseInMemoryDatabase(_database));
        services.AddScoped<ForecastSnapshotRecorder>();

        foreach (var provider in _providers)
        {
            services.AddSingleton<IForecastProvider>(provider);
        }

        _services = services.BuildServiceProvider();

        return new ForecastPollingCycle(
            _services.GetRequiredService<IServiceScopeFactory>(),
            LocationCatalogue.Parse(TwoLocations),
            Options.Create(new ForecastPollingOptions { Stagger = TimeSpan.Zero }),
            NullLogger<ForecastPollingCycle>.Instance);
    }

    private WeatherDbContext Db() =>
        new(new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(_database).Options);

    private async Task SeedSnapshotAsync(string provider, decimal latitude, decimal longitude, DateTimeOffset? expires)
    {
        await using var db = Db();

        db.ForecastSnapshots.Add(new ForecastSnapshot
        {
            Provider = provider,
            Latitude = latitude,
            Longitude = longitude,
            IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            Payload = GzipPayload.Compress(CompactResponse),
            Expires = expires,
            LastModified = DateTimeOffset.UtcNow.AddMinutes(-5),
        });

        await db.SaveChangesAsync();
    }

    private async Task<int> SnapshotCountAsync()
    {
        await using var db = Db();
        return await db.ForecastSnapshots.CountAsync();
    }

    private sealed class StubForecastProvider(string name, Func<(decimal Latitude, decimal Longitude), ForecastFetch> answer)
        : IForecastProvider
    {
        public string Name => name;

        public List<(decimal Latitude, decimal Longitude)> Asked { get; } = [];

        public Task<ForecastFetch> FetchAsync(
            decimal latitude,
            decimal longitude,
            int? altitude,
            DateTimeOffset? knownLastModified,
            CancellationToken cancellationToken = default)
        {
            Asked.Add((latitude, longitude));
            return Task.FromResult(answer((latitude, longitude)));
        }
    }
}
