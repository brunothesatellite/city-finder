namespace CityFinder.Core.Models;

public sealed record GeoPlace(
    string City,
    string? Country = null,
    string? State = null,
    string? County = null,
    double? Latitude = null,
    double? Longitude = null);

public enum GeocodeStatus
{
    Found,
    NotFound,
    Failed,
}

public sealed record GeocodeResult(GeocodeStatus Status, GeoPlace? Place = null, string? Error = null)
{
    public static GeocodeResult Found(GeoPlace place) => new(GeocodeStatus.Found, place);
    public static GeocodeResult NotFound() => new(GeocodeStatus.NotFound);
    public static GeocodeResult Failed(string error) => new(GeocodeStatus.Failed, null, error);
}
