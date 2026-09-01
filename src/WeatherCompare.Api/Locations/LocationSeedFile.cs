using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeatherCompare.Api.Locations;

/// <summary>
/// The hand-written file the Catalogue starts from. It is seed data, not the truth: it is
/// applied on first run and the <c>locations</c> table is authoritative afterwards (ADR-0003).
/// Not a geocoder — nothing here looks a place up.
/// </summary>
public static class LocationSeedFile
{
    /// <summary>Coordinates are sent to Providers at four decimals and no more.</summary>
    public const int CoordinateDecimals = 4;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyList<Location> LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new LocationCatalogueException($"Location catalogue file not found: '{path}'.");
        }

        return Parse(File.ReadAllText(path), path);
    }

    public static IReadOnlyList<Location> Parse(string json, string source = "<inline>")
    {
        var entries = Deserialise(json, source);

        if (entries.Count == 0)
        {
            throw new LocationCatalogueException($"Location catalogue '{source}' contains no Locations.");
        }

        var locations = entries.Select((entry, index) => ToLocation(entry, index, source)).ToList();
        RejectDuplicateCoordinates(locations, source);

        return locations;
    }

    private static IReadOnlyList<Entry> Deserialise(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Entry>>(json, ReadOptions)
                   ?? throw new LocationCatalogueException($"Location catalogue '{source}' is empty.");
        }
        catch (JsonException e)
        {
            throw new LocationCatalogueException(
                $"Location catalogue '{source}' is not readable: {e.Message}");
        }
    }

    private static Location ToLocation(Entry entry, int index, string source)
    {
        var where = $"Location #{index + 1} in '{source}'";

        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            throw new LocationCatalogueException($"{where} has no name.");
        }

        var latitude = Coordinate(entry.Lat, "lat", -90m, 90m, $"{where} ('{entry.Name}')");
        var longitude = Coordinate(entry.Lon, "lon", -180m, 180m, $"{where} ('{entry.Name}')");

        return new Location
        {
            Name = entry.Name.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            Altitude = entry.Altitude ?? throw new LocationCatalogueException(
                $"{where} ('{entry.Name}') has no altitude; give it whole metres above sea level."),
        };
    }

    /// <summary>
    /// More than four decimals is forbidden by MET Norway's terms — it defeats their
    /// server-side cache and can get us blocked. A coordinate is a Location's identity, so a
    /// hand-written over-precise one is refused rather than quietly moved to a different point.
    /// A coordinate typed into the page is truncated instead; the asymmetry is deliberate (ADR-0003).
    /// </summary>
    private static decimal Coordinate(decimal? value, string field, decimal min, decimal max, string where)
    {
        if (value is not { } coordinate)
        {
            throw new LocationCatalogueException($"{where} has no '{field}'.");
        }

        if (coordinate < min || coordinate > max)
        {
            throw new LocationCatalogueException(
                $"{where} has '{field}' {coordinate}, outside {min}..{max}.");
        }

        if (decimal.Round(coordinate, CoordinateDecimals) != coordinate)
        {
            throw new LocationCatalogueException(
                $"{where} has '{field}' {coordinate} with more than {CoordinateDecimals} decimals; " +
                "MET Norway forbids finer coordinates. Write it with at most " +
                $"{CoordinateDecimals} decimals.");
        }

        return decimal.Round(coordinate, CoordinateDecimals);
    }

    /// <summary>
    /// Two entries with the same coordinate are the same Location however they are named,
    /// so the file would be claiming to track one Location twice.
    /// </summary>
    private static void RejectDuplicateCoordinates(IReadOnlyList<Location> locations, string source)
    {
        var duplicates = locations
            .GroupBy(l => l.Coordinate)
            .Where(g => g.Count() > 1)
            .Select(g => $"({g.Key.Latitude}, {g.Key.Longitude}) shared by {string.Join(" and ", g.Select(l => $"'{l.Name}'"))}")
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new LocationCatalogueException(
                $"Location catalogue '{source}' has duplicate coordinates — the same Location " +
                "listed more than once: " + string.Join("; ", duplicates));
        }
    }

    private sealed record Entry(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("lat")] decimal? Lat,
        [property: JsonPropertyName("lon")] decimal? Lon,
        [property: JsonPropertyName("altitude")] int? Altitude);
}
