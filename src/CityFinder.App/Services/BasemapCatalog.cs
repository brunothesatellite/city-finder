namespace CityFinder.App.Services;

/// <summary>
/// Basemaps that do not require an API key. Google Maps is deliberately not
/// included: its tiles may not be used outside the paid Google Maps APIs.
/// </summary>
public static class BasemapCatalog
{
    public const string Plan = "plan";
    public const string Osm = "osm";
    public const string Satellite = "satellite";
    public const string Relief = "relief";
    public const string Default = Osm;

    public static string Normalize(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        Plan => Plan,
        Satellite => Satellite,
        Relief => Relief,
        _ => Default,
    };

    public static string AttributionFor(string? mode) => Normalize(mode) switch
    {
        Osm => "Fond © OpenStreetMap contributors",
        Satellite => "Imagerie © Esri, Maxar, Earthstar Geographics · Libellés © Esri",
        Relief => "Fond © OpenTopoMap (CC-BY-SA) · © OpenStreetMap contributors",
        _ => "Fond © OpenStreetMap contributors · OpenFreeMap · OpenMapTiles",
    };
}
