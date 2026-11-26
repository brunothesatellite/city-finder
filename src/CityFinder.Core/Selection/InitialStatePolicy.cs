using CityFinder.Core.Models;
using CityFinder.Core.Naming;

namespace CityFinder.Core.Selection;

/// <summary>
/// Computes the initial city, optional text and selection for a photo after
/// scanning and geocoding. Already renamed files are never selected again.
/// </summary>
public static class InitialStatePolicy
{
    public static void Apply(Photo photo, SessionEntry? sessionEntry, KnownCityCatalog knownCities)
    {
        photo.SuggestionCity = null;

        if (sessionEntry is not null)
        {
            var parsed = FileNameParser.Parse(photo.FileName, knownCities);
            photo.BaseName = parsed?.BaseName ?? StripKnownSuffix(photo.FileName, sessionEntry);
            photo.City = sessionEntry.City;
            photo.Text = sessionEntry.Text;
            photo.ExistingCity = sessionEntry.City;
            photo.ExistingText = sessionEntry.Text;
            photo.IsFromSession = true;
            photo.IsAlreadyRenamed = true;
            photo.IsSelected = false;
            photo.SessionOriginalName = sessionEntry.OriginalName;
            photo.InitialCity = photo.City;
            photo.InitialText = photo.Text;
            return;
        }

        var parsedName = FileNameParser.Parse(photo.FileName, knownCities);
        if (parsedName is not null)
        {
            photo.BaseName = parsedName.BaseName;
            photo.City = parsedName.City;
            photo.Text = parsedName.Text;
            photo.ExistingCity = parsedName.City;
            photo.ExistingText = parsedName.Text;

            var target = FileNameTools.BuildTargetName(
                photo.BaseName,
                photo.City,
                photo.Text,
                photo.Extension);

            photo.IsAlreadyRenamed = NameText.EqualsLoosely(target, photo.FileName);
            photo.IsSelected = photo.HasGps && !photo.IsAlreadyRenamed;

            if (!string.IsNullOrWhiteSpace(photo.PhotonCity) &&
                !NameText.EqualsLoosely(photo.PhotonCity, photo.City))
            {
                photo.SuggestionCity = photo.PhotonCity;
            }
        }
        else
        {
            photo.BaseName = FileNameTools.StripExtension(photo.FileName);
            photo.City = photo.PhotonCity ?? string.Empty;
            photo.Text = string.Empty;
            photo.IsSelected = photo.HasGps && !string.IsNullOrWhiteSpace(photo.City);
        }

        photo.InitialCity = photo.City;
        photo.InitialText = photo.Text;
    }

    private static string StripKnownSuffix(string fileName, SessionEntry entry)
    {
        var stem = FileNameTools.StripExtension(fileName);
        var suffix = FileNameTools.CitySeparator + entry.City +
                     (string.IsNullOrWhiteSpace(entry.Text) ? string.Empty : FileNameTools.TextSeparator + entry.Text);

        if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return stem[..^suffix.Length].Trim();
        }

        var separator = stem.LastIndexOf(FileNameTools.CitySeparator, StringComparison.Ordinal);
        return separator > 0 ? stem[..separator].Trim() : stem;
    }
}
