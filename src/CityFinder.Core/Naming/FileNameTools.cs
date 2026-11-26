namespace CityFinder.Core.Naming;

public static class FileNameTools
{
    public const string CitySeparator = " - ";
    public const string TextSeparator = " , ";
    public const int MaxStemLength = 200;

    /// <summary>
    /// Builds "<c>base - ville[ , texte][ (n)]</c>" with the original
    /// extension. Returns an empty string when the city is empty.
    /// </summary>
    public static string BuildTargetName(
        string? baseName,
        string? city,
        string? text,
        string? extension,
        int? collisionNumber = null)
    {
        var safeCity = NameText.SanitizeCity(city);
        if (safeCity.Length == 0)
        {
            return string.Empty;
        }

        var safeBase = NameText.SanitizeBaseName(baseName);
        var safeText = NameText.SanitizeText(text);
        var numbered = collisionNumber is > 1;
        var numberSuffix = numbered ? $" ({collisionNumber})" : string.Empty;

        var fixedLength = CitySeparator.Length +
                          safeCity.Length +
                          (safeText.Length > 0 ? TextSeparator.Length + safeText.Length : 0) +
                          numberSuffix.Length;

        var allowedBaseLength = Math.Max(1, MaxStemLength - fixedLength);
        if (safeBase.Length > allowedBaseLength)
        {
            safeBase = NameText.Truncate(safeBase, allowedBaseLength).TrimEnd(' ', '.', '-');
        }

        if (safeBase.Length == 0)
        {
            safeBase = "photo";
        }

        var stem = safeBase + CitySeparator + safeCity +
                   (safeText.Length > 0 ? TextSeparator + safeText : string.Empty) +
                   numberSuffix;

        if (NameText.IsReserved(stem))
        {
            stem = "_" + stem;
        }

        return stem + NormalizeExtension(extension);
    }

    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    public static string StripExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Length > 0 ? fileName[..^extension.Length] : fileName;
    }
}
