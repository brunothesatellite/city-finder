namespace CityFinder.Core.Models;

/// <summary>
/// Content of .cityfinder.json stored in the photo folder. It records the
/// original name of every file CityFinder renamed so the operation can be
/// undone, even after a restart.
/// </summary>
public sealed class SessionDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<SessionEntry> Entries { get; set; } = new();

    public SessionEntry? FindByCurrentName(string currentName) =>
        Entries.FirstOrDefault(entry => NameTextEquals(entry.CurrentName, currentName));

    public SessionEntry? FindByOriginalName(string originalName) =>
        Entries.FirstOrDefault(entry => NameTextEquals(entry.OriginalName, originalName));

    private static bool NameTextEquals(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public sealed class SessionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OriginalName { get; set; } = string.Empty;
    public string CurrentName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? PhotonCity { get; set; }
    public long Length { get; set; }
    public DateTime LastWriteUtc { get; set; }
    public DateTime? TakenAt { get; set; }
    public DateTime RenamedUtc { get; set; } = DateTime.UtcNow;
    public string BatchId { get; set; } = string.Empty;
}
