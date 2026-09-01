namespace WeatherCompare.Api.Polling;

/// <summary>
/// How often we walk the Location catalogue asking every Provider. The real cadence is set by
/// the Providers themselves — a (Provider, Location) pair is not asked again until its newest
/// Forecast Snapshot has Expired — so these are the knobs around that, not a refresh schedule.
/// </summary>
public class ForecastPollingOptions
{
    public const string Section = "Polling";

    /// <summary>Whether the poller runs at all; off means nothing is ever asked.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long to wait between sweeps of the catalogue. Kept shorter than the Providers' own
    /// Expires, so a lapsed Snapshot is refreshed soon after rather than a whole cycle later.
    /// </summary>
    public TimeSpan CycleInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long to wait between two asks. MET Norway's terms ask that a batch of Locations is
    /// spread out rather than fired at them all at once.
    /// </summary>
    public TimeSpan Stagger { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a Snapshot counts as fresh when the Provider did not say when it Expires,
    /// so a silent Provider is not asked again on every single cycle.
    /// </summary>
    public TimeSpan AssumedFreshness { get; set; } = TimeSpan.FromMinutes(30);
}
