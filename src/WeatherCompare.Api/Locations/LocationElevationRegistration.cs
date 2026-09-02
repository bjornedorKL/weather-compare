using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace WeatherCompare.Api.Locations;

public static class LocationElevationRegistration
{
    /// <summary>
    /// Registers the elevation model a coordinate's altitude is looked up in. Its own
    /// registration beside <c>AddLocationSearch</c>, not folded into it: the two are separate
    /// calls behind separate endpoints of ours, and either can be replaced alone (ADR-0004).
    /// Reached server-side for the same reasons the gazetteer is — one origin in the browser, and
    /// a <c>User-Agent</c> naming the application.
    /// </summary>
    public static IServiceCollection AddLocationElevation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ElevationOptions>(configuration.GetSection(ElevationOptions.Section));

        services.AddHttpClient<OpenMeteoElevation>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<ElevationOptions>>().Value;

            http.BaseAddress = new Uri(options.BaseAddress);
            // Someone pressed a button and is waiting: ten seconds, as the gazetteer has, after
            // which "unreachable" is the more useful answer than a longer wait.
            http.Timeout = TimeSpan.FromSeconds(10);

            // Sent unvalidated so a contact URL can be written plainly, as MET's is.
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
