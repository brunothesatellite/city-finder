namespace CityFinder.Core.Models;

public sealed record PhotoStatistics(
    int Total,
    int Located,
    int MissingGps,
    int Cities,
    int Selected,
    int Renamed,
    int WithoutCity);

public static class PhotoStatisticsCalculator
{
    public static PhotoStatistics Calculate(IEnumerable<Photo> photos)
    {
        var list = photos as IReadOnlyCollection<Photo> ?? photos.ToList();
        var located = list.Count(photo => photo.HasGps);
        var cities = list
            .Select(photo => photo.City?.Trim())
            .Where(city => !string.IsNullOrWhiteSpace(city))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new PhotoStatistics(
            Total: list.Count,
            Located: located,
            MissingGps: list.Count - located,
            Cities: cities,
            Selected: list.Count(photo => photo.IsSelected),
            Renamed: list.Count(photo => photo.IsAlreadyRenamed),
            WithoutCity: list.Count(photo => string.IsNullOrWhiteSpace(photo.City)));
    }
}
