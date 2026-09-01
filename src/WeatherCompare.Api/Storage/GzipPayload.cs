using System.IO.Compression;
using System.Text;

namespace WeatherCompare.Api.Storage;

/// <summary>
/// A Forecast Snapshot's Payload is the Provider's response verbatim, gzipped (ADR-0001).
/// </summary>
public static class GzipPayload
{
    public static byte[] Compress(string text)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(Encoding.UTF8.GetBytes(text));
        }

        return output.ToArray();
    }

    public static string Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);

        return Encoding.UTF8.GetString(output.ToArray());
    }
}
