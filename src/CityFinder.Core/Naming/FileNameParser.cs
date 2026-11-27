namespace CityFinder.Core.Naming;

public sealed record ParsedFileName(string BaseName, string City, string Text);

/// <summary>
/// Reads an existing "<c>base - ville[, texte]</c>" suffix. A candidate is
/// accepted only when the city is present in the known catalogue, so arbitrary
/// words are never mistaken for a commune.
/// </summary>
public static class FileNameParser
{
    public static ParsedFileName? Parse(string fileName, KnownCityCatalog knownCities)
    {
        if (string.IsNullOrWhiteSpace(fileName) || knownCities is null)
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        var stem = extension.Length > 0 ? fileName[..^extension.Length] : fileName;

        var separators = new List<int>();
        var searchFrom = 0;
        while (searchFrom < stem.Length &&
               (searchFrom = stem.IndexOf(" - ", searchFrom, StringComparison.Ordinal)) >= 0)
        {
            if (searchFrom > 0)
            {
                separators.Add(searchFrom);
            }

            searchFrom += 3;
        }

        // Start from the rightmost possible separator, but keep moving left when
        // the right side is not a known city (the text itself may contain " - ").
        for (var index = separators.Count - 1; index >= 0; index--)
        {
            var separator = separators[index];
            var baseName = stem[..separator].Trim();
            var suffix = stem[(separator + 3)..].Trim();
            if (baseName.Length == 0 || suffix.Length == 0)
            {
                continue;
            }

            var parsed = ParseSuffix(baseName, suffix, knownCities);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return null;
    }

    private static ParsedFileName? ParseSuffix(
        string baseName,
        string suffix,
        KnownCityCatalog knownCities)
    {
        // The new separator is ", ". Searching for it also accepts the old
        // " , " form for files named by previous versions.
        var textSeparator = suffix.IndexOf(", ", StringComparison.Ordinal);
        if (textSeparator > 0)
        {
            var cityPart = suffix[..textSeparator].Trim();
            if (knownCities.TryGet(cityPart, out var canonical))
            {
                var text = suffix[(textSeparator + 2)..].Trim();
                return new ParsedFileName(baseName, canonical, text);
            }

            return null;
        }

        if (knownCities.TryGet(suffix, out var wholeCity))
        {
            return new ParsedFileName(baseName, wholeCity, string.Empty);
        }

        // Legacy space separator, accepted only with an exact known-city prefix.
        foreach (var city in knownCities.Cities.OrderByDescending(value => value.Length))
        {
            if (suffix.Length <= city.Length ||
                !suffix.StartsWith(city, StringComparison.OrdinalIgnoreCase) ||
                !char.IsWhiteSpace(suffix[city.Length]))
            {
                continue;
            }

            var text = suffix[(city.Length + 1)..].Trim();
            return new ParsedFileName(baseName, city, text);
        }

        return null;
    }
}
