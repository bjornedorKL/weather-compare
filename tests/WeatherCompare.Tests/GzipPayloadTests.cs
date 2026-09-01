using WeatherCompare.Api.Storage;

namespace WeatherCompare.Tests;

public class GzipPayloadTests
{
    // A trimmed shape of what MET Norway's Locationforecast `compact` returns.
    private const string ProviderResponse = """
        {
          "type": "Feature",
          "geometry": { "type": "Point", "coordinates": [10.7522, 59.9139, 23] },
          "properties": {
            "meta": {
              "updated_at": "2026-09-01T10:23:41Z",
              "units": { "air_temperature": "celsius", "wind_speed": "m/s" }
            },
            "timeseries": [
              {
                "time": "2026-09-01T11:00:00Z",
                "data": {
                  "instant": { "details": { "air_temperature": 17.4, "wind_speed": 3.1 } },
                  "next_1_hours": {
                    "summary": { "symbol_code": "partlycloudy_day" },
                    "details": { "precipitation_amount": 0.0 }
                  }
                }
              }
            ]
          }
        }
        """;

    [Fact]
    public void Round_trips_a_provider_response()
    {
        var compressed = GzipPayload.Compress(ProviderResponse);

        Assert.Equal(ProviderResponse, GzipPayload.Decompress(compressed));
    }

    [Fact]
    public void Compressing_shrinks_a_provider_response()
    {
        var compressed = GzipPayload.Compress(ProviderResponse);

        Assert.True(
            compressed.Length < System.Text.Encoding.UTF8.GetByteCount(ProviderResponse),
            $"gzipped payload was {compressed.Length} bytes, no smaller than the raw response");
    }
}
