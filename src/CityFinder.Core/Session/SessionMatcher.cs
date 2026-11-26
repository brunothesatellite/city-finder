using CityFinder.Core.Models;
using CityFinder.Core.Naming;

namespace CityFinder.Core.Session;

public sealed record SessionMatchResult(
    IReadOnlyDictionary<string, SessionEntry> MatchesByPhotoId,
    int MatchedCount,
    int StaleCount);

/// <summary>
/// Associates session entries with scanned files using the current name, size
/// and last-write time. A mismatch is never auto-renamed.
/// </summary>
public static class SessionMatcher
{
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(2);

    public static SessionMatchResult Match(IReadOnlyList<Photo> photos, SessionDocument? document)
    {
        if (document is null || document.Entries.Count == 0)
        {
            return new SessionMatchResult(
                new Dictionary<string, SessionEntry>(StringComparer.Ordinal),
                0,
                0);
        }

        var entriesByName = new Dictionary<string, SessionEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.Entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.CurrentName))
            {
                entriesByName[entry.CurrentName] = entry;
            }
        }

        var matches = new Dictionary<string, SessionEntry>(StringComparer.Ordinal);
        var usedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var photo in photos)
        {
            if (!entriesByName.TryGetValue(photo.FileName, out var entry))
            {
                continue;
            }

            if (entry.Length > 0 && entry.Length != photo.Length)
            {
                continue;
            }

            if (entry.LastWriteUtc != default &&
                (photo.LastWriteUtc - entry.LastWriteUtc).Duration() > TimestampTolerance)
            {
                continue;
            }

            matches[photo.Id] = entry;
            usedEntries.Add(entry.Id);
        }

        var stale = document.Entries.Count(entry => !usedEntries.Contains(entry.Id));
        return new SessionMatchResult(matches, matches.Count, stale);
    }
}
