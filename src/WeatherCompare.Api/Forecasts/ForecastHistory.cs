namespace WeatherCompare.Api.Forecasts;

/// <summary>
/// What successive Forecast Snapshots said about one future moment at one Location — the read
/// the append-only store exists for. Every other read takes the newest Snapshot and throws the
/// rest away; this one walks them in the order they were Issued, so a Forecast that moved can
/// be seen moving.
/// <para>
/// It shows how a prediction <em>moved</em>, never whether it was right. Being right would need
/// an Observation — what the weather actually was — which is deliberately not a term in this
/// domain and which nothing here produces (CONTEXT.md).
/// </para>
/// </summary>
/// <param name="ValidAt">
/// The future moment every Forecast below describes. Matched exactly, never nearest: a Forecast
/// for 12:00 is a statement about 12:00, and offering it as an answer about 14:00 would be an
/// invention.
/// </param>
/// <param name="SnapshotsRead">
/// How many Snapshot payloads were decompressed and read to answer. Bounded on request, because
/// this is the first read that touches more than one row per (Provider, Location).
/// </param>
public sealed record ForecastHistory(
    string Name,
    decimal Latitude,
    decimal Longitude,
    int Altitude,
    DateTimeOffset ValidAt,
    int SnapshotsRead,
    IReadOnlyList<ProviderForecastHistory> Providers);

/// <summary>
/// One Provider's successive answers about the moment, oldest Issued first, and the stretches
/// where it was asked for none. Comparing Providers to each other is a different question; this
/// compares a Provider to its own earlier self.
/// </summary>
public sealed record ProviderForecastHistory(
    string Provider,
    IReadOnlyList<ForecastHistoryPoint> Points,
    IReadOnlyList<ForecastHistoryGap> Gaps);

/// <summary>
/// What one Snapshot said about the moment. <c>Forecast</c> is null when the Snapshot was
/// recorded but described no Forecast for exactly this moment — MET's steps lengthen to six
/// hours down the range, so an older Snapshot may simply never have spoken about 14:00. A
/// Snapshot that was silent is not the same thing as a Snapshot that is missing.
/// </summary>
public sealed record ForecastHistoryPoint(DateTimeOffset IssuedAt, Forecast? Forecast);

/// <summary>
/// A stretch of the record where a Forecast Snapshot was due and none was recorded. The poller
/// runs locally, so Snapshots exist only for the periods the machine was awake, and a gap can
/// never be backfilled — a Provider cannot be asked what it said last Tuesday (CONTEXT.md).
/// <para>
/// Due, not merely absent: a Provider that answered <c>304 Not Modified</c> appends nothing, and
/// that is the record working as designed rather than a hole in it. <c>DueAt</c> is when the
/// Snapshot before the gap said to ask again, so a gap starts where the record should have
/// continued rather than wherever it happened to pause.
/// </para>
/// </summary>
/// <param name="FromIssuedAt">Issued At of the last Snapshot before the gap.</param>
/// <param name="ToIssuedAt">Issued At of the first Snapshot after it.</param>
/// <param name="DueAt">When the Provider said to ask again, and so when the record fell silent.</param>
public sealed record ForecastHistoryGap(
    DateTimeOffset FromIssuedAt,
    DateTimeOffset ToIssuedAt,
    DateTimeOffset DueAt);
