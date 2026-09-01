namespace WeatherCompare.Api.Polling;

/// <summary>What one sweep of the catalogue did, counted per (Provider, Location) pair.</summary>
public sealed class PollingCycleTally
{
    /// <summary>Pairs where a new Forecast Snapshot was appended.</summary>
    public int Appended { get; private set; }

    /// <summary>Pairs where the Provider answered 304: nothing new to say, nothing written.</summary>
    public int NotModified { get; private set; }

    /// <summary>Pairs where the Provider could not be asked, or answered unusably.</summary>
    public int Failed { get; private set; }

    /// <summary>Pairs not asked at all, because our newest Snapshot has not Expired yet.</summary>
    public int StillFresh { get; private set; }

    /// <summary>Pairs the Provider was actually asked about.</summary>
    public int Asked => Appended + NotModified + Failed;

    public void CountAppended() => Appended++;

    public void CountNotModified() => NotModified++;

    public void CountFailed() => Failed++;

    public void CountStillFresh() => StillFresh++;
}
