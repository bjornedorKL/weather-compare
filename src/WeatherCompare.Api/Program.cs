using Microsoft.EntityFrameworkCore;
using WeatherCompare.Api.Forecasts;
using WeatherCompare.Api.Locations;
using WeatherCompare.Api.Polling;
using WeatherCompare.Api.Providers;
using WeatherCompare.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddLocationCatalogue();
builder.Services.AddDbContext<WeatherDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WeatherCompare")));
builder.Services.AddForecastProviders(builder.Configuration);
builder.Services.AddForecastReading();
builder.Services.AddForecastPolling(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<WeatherDbContext>().Database.Migrate();
}

app.MapGet("/health", async (WeatherDbContext db) =>
{
    var snapshots = await db.ForecastSnapshots.CountAsync();
    return Results.Ok(new { status = "ok", snapshots });
});

app.MapForecastEndpoints();

app.Run();
