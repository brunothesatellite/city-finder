using System.Windows;
using System.Windows.Media;

namespace CityFinder.App.Services;

public static class ThemeManager
{
    private static readonly IReadOnlyDictionary<string, string> Light =
        new Dictionary<string, string>
        {
            ["AppBg"] = "#F7F3EA",
            ["Surface"] = "#FFFDF8",
            ["SurfaceAlt"] = "#F0EADD",
            ["Border"] = "#DDD3C2",
            ["Ink"] = "#23281F",
            ["InkMuted"] = "#6E7267",
            ["Accent"] = "#2E5D46",
            ["AccentInk"] = "#F7FBF8",
            ["AccentSoft"] = "#E2EEE6",
            ["Warning"] = "#98601F",
            ["WarningSoft"] = "#F8EBD9",
            ["Danger"] = "#9C3B32",
            ["DangerSoft"] = "#F8E3DE",
            ["MapBg"] = "#E7E1D4",
            ["Overlay"] = "#B3F7F3EA",
            ["Shadow"] = "#1A2B241C",
        };

    private static readonly IReadOnlyDictionary<string, string> Dark =
        new Dictionary<string, string>
        {
            ["AppBg"] = "#151A16",
            ["Surface"] = "#1E2520",
            ["SurfaceAlt"] = "#262E28",
            ["Border"] = "#37423A",
            ["Ink"] = "#F0F3EC",
            ["InkMuted"] = "#A4ADA2",
            ["Accent"] = "#92C9AA",
            ["AccentInk"] = "#112218",
            ["AccentSoft"] = "#24382C",
            ["Warning"] = "#E3B172",
            ["WarningSoft"] = "#392C1C",
            ["Danger"] = "#F08C80",
            ["DangerSoft"] = "#3F2421",
            ["MapBg"] = "#1B221D",
            ["Overlay"] = "#B3151A16",
            ["Shadow"] = "#66000000",
        };

    public static string Normalize(string? theme) =>
        string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";

    public static bool IsDark(string? theme) => Normalize(theme) == "dark";

    public static void Apply(string? theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        var palette = IsDark(theme) ? Dark : Light;
        foreach (var pair in palette)
        {
            if (ColorConverter.ConvertFromString(pair.Value) is Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                Application.Current.Resources[pair.Key] = brush;
            }
        }

        Application.Current.Resources["WindowTheme"] = Normalize(theme);
    }
}
