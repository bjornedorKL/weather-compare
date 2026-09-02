using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace WeatherCompare.Api.Locations;

public static class LocationSearchRegistration
{
    /// <summary>
    /// Registers the gazetteer a name search asks. Reached server-side, like every other external
    /// service here: <c>vite.config.ts</c> names one origin in the browser as deliberate, and a
    /// <c>User-Agent</c> we control is only ours to send from here (ADR-0004).
    /// </summary>
    public static IServiceCollection AddLocationSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GazetteerOptions>(configuration.GetSection(GazetteerOptions.Section));

        services.AddHttpClient<OpenMeteoGazetteer>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<GazetteerOptions>>().Value;

            http.BaseAddress = new Uri(options.BaseAddress);
            // Shorter than the poller's thirty seconds: someone is waiting for this one, and a
            // search that has not answered by now is more usefully reported as unreachable.
            http.Timeout = TimeSpan.FromSeconds(10);

            // Sent unvalidated so a contact URL can be written plainly, as MET's is.
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
