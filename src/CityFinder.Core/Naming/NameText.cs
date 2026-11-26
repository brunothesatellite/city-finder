using System.Globalization;
using System.Text;

namespace CityFinder.Core.Naming;

/// <summary>
/// Text helpers for city names, free text and file names. Accents are kept in
/// file names but ignored for comparisons; Windows-invalid characters are
/// removed or replaced.
/// </summary>
public static class NameText
{
    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Accent-, case- and whitespace-insensitive comparison key.</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var prepared = new StringBuilder(value.Length + 4);
        foreach (var character in value.Trim())
        {
            switch (character)
            {
                case 'œ':
                case 'Œ':
                    prepared.Append("oe");
                    break;
                case 'æ':
                case 'Æ':
                    prepared.Append("ae");
                    break;
                case 'ß':
                    prepared.Append("ss");
                    break;
                default:
                    prepared.Append(character);
                    break;
            }
        }

        var decomposed = prepared.ToString().Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (!previousWasSpace && folded.Length > 0)
                {
                    folded.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            folded.Append(character);
        }

        return folded.ToString().Trim().ToUpperInvariant();
    }

    public static bool EqualsLoosely(string? left, string? right) =>
        string.Equals(Fold(left), Fold(right), StringComparison.Ordinal);

    /// <summary>Sanitises a city name; commas are removed because they delimit the optional text.</summary>
    public static string SanitizeCity(string? value, int maxLength = 80) =>
        SanitizePart(value, maxLength, removeCommas: true);

    /// <summary>Sanitises the optional free text appended after the city.</summary>
    public static string SanitizeText(string? value, int maxLength = 120) =>
        SanitizePart(value, maxLength, removeCommas: false);

    private static string SanitizePart(string? value, int maxLength, bool removeCommas)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var character in normalized)
        {
            var invalid = char.IsControl(character) ||
                          Array.IndexOf(InvalidNameChars, character) >= 0 ||
                          (removeCommas && (character == ',' || character == ';'));

            var output = invalid ? ' ' : character;
            if (char.IsWhiteSpace(output))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            builder.Append(output);
        }

        var result = builder.ToString().Trim().Trim('.', ' ');
        if (result.Length > maxLength)
        {
            result = Truncate(result, maxLength).TrimEnd(' ', '.', '-');
        }

        return result;
    }

    public static string SanitizeBaseName(string? value, int maxLength = 160)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "photo";
        }

        var normalized = value.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var character in normalized)
        {
            var invalid = char.IsControl(character) || Array.IndexOf(InvalidNameChars, character) >= 0;
            var output = invalid ? '-' : character;
            if (char.IsWhiteSpace(output))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            previousWasSpace = false;
            builder.Append(output);
        }

        var result = builder.ToString().Trim().Trim('.', ' ');
        while (result.Contains("--", StringComparison.Ordinal))
        {
            result = result.Replace("--", "-", StringComparison.Ordinal);
        }

        if (result.Length > maxLength)
        {
            result = Truncate(result, maxLength).TrimEnd(' ', '.', '-');
        }

        if (result.Length == 0)
        {
            result = "photo";
        }

        return IsReserved(result) ? "_" + result : result;
    }

    public static bool IsReserved(string stem) => ReservedNames.Contains(stem.Trim());

    public static string Truncate(string value, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        var length = maxLength;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return value[..length];
    }
}
