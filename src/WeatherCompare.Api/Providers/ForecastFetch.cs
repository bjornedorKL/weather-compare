namespace WeatherCompare.Api.Providers;

public enum ForecastFetchOutcome
{
    /// <summary>The Provider answered with Forecasts we do not already hold.</summary>
    Fetched,

    /// <summary>The Provider has not recomputed since the Snapshot we already hold.</summary>
    NotModified,

    /// <summary>The Provider could not be asked, or answered with something unusable.</summary>
    Failed,
}

/// <summary>
/// What one ask of a Provider produced. <see cref="Body"/> is the Provider's response verbatim,
/// unparsed and unfiltered, and is only present when the outcome is
/// <see cref="ForecastFetchOutcome.Fetched"/>.
/// </summary>
public sealed record ForecastFetch(
    ForecastFetchOutcome Outcome,
    string? Body = null,
    DateTimeOffset? Expires = null,
    DateTimeOffset? LastModified = null,
    string? Failure = null)
{
    public static ForecastFetch Fetched(string body, DateTimeOffset? expires, DateTimeOffset? lastModified) =>
        new(ForecastFetchOutcome.Fetched, body, expires, lastModified);

    public static ForecastFetch NotModified() => new(ForecastFetchOutcome.NotModified);

    public static ForecastFetch Failed(string failure) =>
        new(ForecastFetchOutcome.Failed, Failure: failure);
}
