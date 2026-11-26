namespace CityFinder.App.Services;

public sealed record MapPointDto(
    string Id,
    double Lat,
    double Lon,
    string Name,
    string City,
    string Text,
    bool Selected,
    bool Active,
    string Status,
    string Date);
