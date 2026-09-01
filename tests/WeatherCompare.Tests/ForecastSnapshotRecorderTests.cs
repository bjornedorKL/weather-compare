using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherCompare.Api.Providers;
using WeatherCompare.Api.Storage;

namespace WeatherCompare.Tests;

public class ForecastSnapshotRecorderTests : IDisposable
{
    private const string CompactResponse =
        """{"type":"Feature","properties":{"meta":{"updated_at":"2026-09-01T10:23:41Z"},"timeseries":[]}}""";

    private readonly WeatherDbContext _db = new(
        new DbContextOptionsBuilder<WeatherDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Appends_a_snapshot_holding_the_providers_response_verbatim()
    {
        var expires = new DateTimeOffset(2026, 9, 1, 11, 30, 0, TimeSpan.Zero);
        var lastModified = new DateTimeOffset(2026, 9, 1, 10, 23, 41, TimeSpan.Zero);
        var provider = new StubForecastProvider(
            ForecastFetch.Fetched(CompactResponse, expires, lastModified));

        var result = await Recorder().RefreshAsync(provider, 59.913868m, 10.752245m, 23);

        Assert.True(result.SnapshotWritten);

        var snapshot = Assert.Single(_db.ForecastSnapshots);
        Assert.Equal("MET Norway", snapshot.Provider);
        Assert.Equal(59.9138m, snapshot.Latitude);
        Assert.Equal(10.7522m, snapshot.Longitude);
        Assert.Equal(expires, snapshot.Expires);
        Assert.Equal(lastModified, snapshot.LastModified);
        Assert.Equal(CompactResponse, GzipPayload.Decompress(snapshot.Payload));
    }

    [Fact]
    public async Task Truncates_the_coordinate_before_asking_the_provider()
    {
        var provider = new StubForecastProvider(ForecastFetch.NotModified());

        await Recorder().RefreshAsync(provider, 59.913868m, 10.752245m, 23);

        Assert.Equal(59.9138m, provider.AskedLatitude);
        Assert.Equal(10.7522m, provider.AskedLongitude);
    }

    [Fact]
    public async Task Writes_nothing_when_the_provider_has_nothing_new_to_say()
    {
        var lastModified = new DateTimeOffset(2026, 9, 1, 10, 23, 41, TimeSpan.Zero);
        var provider = new StubForecastProvider(
            ForecastFetch.Fetched(CompactResponse, null, lastModified),
            ForecastFetch.NotModified());

        await Recorder().RefreshAsync(provider, 59.9139m, 10.7522m, 23);
        var second = await Recorder().RefreshAsync(provider, 59.9139m, 10.7522m, 23);

        Assert.Equal(ForecastRefreshOutcome.NotModified, second.Outcome);
        Assert.False(second.SnapshotWritten);
        Assert.Single(_db.ForecastSnapshots);
    }

    [Fact]
    public async Task Asks_only_for_what_changed_since_the_newest_snapshot_we_hold()
    {
        var lastModified = new DateTimeOffset(2026, 9, 1, 10, 23, 41, TimeSpan.Zero);
        var provider = new StubForecastProvider(
            ForecastFetch.Fetched(CompactResponse, null, lastModified),
            ForecastFetch.NotModified());

        await Recorder().RefreshAsync(provider, 59.9139m, 10.7522m, 23);
        Assert.Null(provider.AskedKnownLastModified);

        await Recorder().RefreshAsync(provider, 59.9139m, 10.7522m, 23);
        Assert.Equal(lastModified, provider.AskedKnownLastModified);
    }

    [Fact]
    public async Task Writes_nothing_when_the_provider_could_not_be_asked()
    {
        var provider = new StubForecastProvider(ForecastFetch.Failed("MET Norway answered 503"));

        var result = await Recorder().RefreshAsync(provider, 59.9139m, 10.7522m, 23);

        Assert.Equal(ForecastRefreshOutcome.Failed, result.Outcome);
        Assert.Equal("MET Norway answered 503", result.Failure);
        Assert.Empty(_db.ForecastSnapshots);
    }

    private ForecastSnapshotRecorder Recorder() =>
        new(_db, NullLogger<ForecastSnapshotRecorder>.Instance);

    private class StubForecastProvider(params ForecastFetch[] answers) : IForecastProvider
    {
        private int _asked;

        public string Name => "MET Norway";

        public decimal AskedLatitude { get; private set; }

        public decimal AskedLongitude { get; private set; }

        public DateTimeOffset? AskedKnownLastModified { get; private set; }

        public Task<ForecastFetch> FetchAsync(
            decimal latitude,
            decimal longitude,
            int? altitude,
            DateTimeOffset? knownLastModified,
            CancellationToken cancellationToken = default)
        {
            AskedLatitude = latitude;
            AskedLongitude = longitude;
            AskedKnownLastModified = knownLastModified;

            return Task.FromResult(answers[Math.Min(_asked++, answers.Length - 1)]);
        }
    }
}
