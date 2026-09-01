using System.Net;
using System.Net.Http.Headers;

namespace WeatherCompare.Tests;

/// <summary>Answers with a canned response and remembers what was asked.</summary>
public class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public HttpRequestMessage LastRequest => Requests[^1];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(answer(request));
    }

    public static StubHttpMessageHandler Answering(
        HttpStatusCode status,
        string body = "",
        string? contentType = "application/json",
        DateTimeOffset? expires = null,
        DateTimeOffset? lastModified = null)
    {
        return new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body),
            };

            response.Content.Headers.ContentType =
                contentType is null ? null : new MediaTypeHeaderValue(contentType);
            response.Content.Headers.Expires = expires;
            response.Content.Headers.LastModified = lastModified;

            return response;
        });
    }
}
