namespace CityFinder.Core.Models;

public enum RenameItemStatus
{
    Ready,
    AlreadyNamed,
    MissingCity,
    InvalidTarget,
}

public sealed record RenameCandidate(
    string Id,
    string FullPath,
    string FileName,
    string Extension,
    string BaseName,
    string City,
    string Text)
{
    public string Folder => Path.GetDirectoryName(FullPath) ?? string.Empty;
}

public sealed record RenameItem(
    RenameCandidate Candidate,
    string TargetName,
    RenameItemStatus Status,
    bool Renumbered = false,
    string? Note = null)
{
    public string SourcePath => Candidate.FullPath;
    public string TargetPath => Path.Combine(Candidate.Folder, TargetName);
    public bool IsReady => Status == RenameItemStatus.Ready;
}

public sealed class RenamePlan
{
    public RenamePlan(IReadOnlyList<RenameItem> items)
    {
        Items = items;
    }

    public IReadOnlyList<RenameItem> Items { get; }

    public IReadOnlyList<RenameItem> ReadyItems =>
        Items.Where(item => item.Status == RenameItemStatus.Ready).ToList();

    public int ReadyCount => Items.Count(item => item.Status == RenameItemStatus.Ready);
    public int AlreadyNamedCount => Items.Count(item => item.Status == RenameItemStatus.AlreadyNamed);
    public int MissingCityCount => Items.Count(item => item.Status == RenameItemStatus.MissingCity);
    public int InvalidCount => Items.Count(item => item.Status == RenameItemStatus.InvalidTarget);
    public int RenumberedCount => Items.Count(item => item.Status == RenameItemStatus.Ready && item.Renumbered);

    public bool HasWork => ReadyCount > 0;
}
