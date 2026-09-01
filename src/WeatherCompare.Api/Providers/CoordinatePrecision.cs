namespace WeatherCompare.Api.Providers;

/// <summary>
/// A Location is its coordinate at the precision the Provider accepts. MET Norway's terms of
/// service cap that at four decimals: more defeats their cache and gets applications blocked.
/// The store agrees — Latitude and Longitude are numeric(8,4).
/// </summary>
public static class CoordinatePrecision
{
    public const int Decimals = 4;

    /// <summary>Truncates (never rounds up) a degree value to the accepted precision.</summary>
    public static decimal Truncate(decimal degrees) =>
        decimal.Round(degrees, Decimals, MidpointRounding.ToZero);
}
