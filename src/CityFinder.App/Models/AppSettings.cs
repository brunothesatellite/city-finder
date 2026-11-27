namespace CityFinder.App.Models;

public sealed class AppSettings
{
    public string? LastFolder { get; set; }
    public string Theme { get; set; } = "light";
    public bool MapVisible { get; set; } = true;
    public string MapBasemap { get; set; } = "osm";
    public bool IncludeSubfolders { get; set; }
    public DateTime? LastUpdateCheckUtc { get; set; }
    public string? SkippedUpdateTag { get; set; }
}
