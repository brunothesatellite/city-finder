namespace CityFinder.App.Models;

public sealed class AppSettings
{
    public string? LastFolder { get; set; }
    public string Theme { get; set; } = "light";
    public bool MapVisible { get; set; } = true;
    public bool IncludeSubfolders { get; set; }
}
