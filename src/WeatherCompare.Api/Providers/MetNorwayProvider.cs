using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;

namespace WeatherCompare.Api.Providers;

/// <summary>
/// MET Norway, asked through Locationforecast 2.0 <c>compact</c> (ADR-0001: compact carries
/// everything the page renders at a fraction of complete's size).
/// </summary>
public class MetNorwayProvider(
    HttpClient http,
    IOptions<MetNorwayOptions> options,
    ILogger<MetNorwayProvider> logger) : IForecastProvider
{
    private readonly MetNorwayOptions _options = options.Value;

    public string Name => _options.Name;

    public async Task<ForecastFetch> FetchAsync(
        decimal latitude,
        decimal longitude,
        int? altitude,
        DateTimeOffset? knownLastModified,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CompactUrl(latitude, longitude, altitude));
        request.Headers.IfModifiedSince = knownLastModified;

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "{Provider} could not be reached", Name);
            return ForecastFetch.Failed($"could not reach {Name}: {e.Message}");
        }
    }

    private async Task<ForecastFetch> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            logger.LogDebug("{Provider} has not recomputed since our newest Forecast Snapshot", Name);
            return ForecastFetch.NotModified();
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("{Provider} answered {Status}", Name, (int)response.StatusCode);
            return ForecastFetch.Failed($"{Name} answered {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // MET serves HTML error pages under load. Storing one would be silent corruption:
        // it would sit in the store looking like a Forecast Snapshot until something read it.
        if (!LooksLikeJson(response, body))
        {
            logger.LogWarning(
                "{Provider} answered 200 with {ContentType}, not JSON; nothing appended",
                Name,
                response.Content.Headers.ContentType?.MediaType ?? "no content type");
            return ForecastFetch.Failed($"{Name} answered 200 with a body that is not JSON");
        }

        return ForecastFetch.Fetched(
            body,
            response.Content.Headers.Expires,
            response.Content.Headers.LastModified);
    }

    private static bool LooksLikeJson(HttpResponseMessage response, string body)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        var declaresJson = mediaType is not null && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);

        return declaresJson && body.AsSpan().TrimStart().StartsWith("{");
    }

    /// <summary>
    /// Coordinates go out truncated to four decimals — MET's terms forbid more, because extra
    /// precision defeats their cache.
    /// </summary>
    private static string CompactUrl(decimal latitude, decimal longitude, int? altitude)
    {
        var url =
            $"compact?lat={Degrees(latitude)}&lon={Degrees(longitude)}";

        return altitude is null
            ? url
            : $"{url}&altitude={altitude.Value.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string Degrees(decimal value) =>
        CoordinatePrecision.Truncate(value).ToString(CultureInfo.InvariantCulture);
}
