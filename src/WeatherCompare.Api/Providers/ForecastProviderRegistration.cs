using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace WeatherCompare.Api.Providers;

public static class ForecastProviderRegistration
{
    /// <summary>
    /// Registers every Provider we can ask, plus the refresh that appends what they say.
    /// </summary>
    public static IServiceCollection AddForecastProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MetNorwayOptions>(configuration.GetSection(MetNorwayOptions.Section));
        services.AddScoped<ForecastSnapshotRecorder>();

        services.AddHttpClient<IForecastProvider, MetNorwayProvider>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<MetNorwayOptions>>().Value;

            http.BaseAddress = new Uri(options.BaseAddress);
            http.Timeout = TimeSpan.FromSeconds(30);

            // Mandatory: MET blocks applications that do not identify themselves. Sent unvalidated
            // so a contact URL can be written plainly, the way MET's own examples do.
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
