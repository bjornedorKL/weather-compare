namespace WeatherCompare.Api.Locations;

/// <summary>
/// Thrown when the hand-written Location catalogue cannot be trusted. The catalogue is
/// loaded at startup, so a bad file stops the application rather than reaching a Provider.
/// </summary>
public sealed class LocationCatalogueException(string message) : Exception(message);
