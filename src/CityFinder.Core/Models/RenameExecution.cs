namespace CityFinder.Core.Models;

public sealed record RenameOutcome(
    string PhotoId,
    string SourceName,
    string TargetName,
    bool Success,
    bool Renumbered,
    string? Error = null);

public sealed class RenameExecutionResult
{
    public RenameExecutionResult(
        string batchId,
        IReadOnlyList<RenameOutcome> outcomes,
        IReadOnlyList<string>? warnings = null)
    {
        BatchId = batchId;
        Outcomes = outcomes;
        Warnings = warnings ?? Array.Empty<string>();
    }

    public string BatchId { get; }
    public IReadOnlyList<RenameOutcome> Outcomes { get; }
    public IReadOnlyList<string> Warnings { get; }
    public int SuccessCount => Outcomes.Count(outcome => outcome.Success);
    public int FailureCount => Outcomes.Count(outcome => !outcome.Success);
    public bool HasFailures => FailureCount > 0;
}

public sealed record UndoOutcome(
    string EntryId,
    string CurrentName,
    string OriginalName,
    bool Success,
    bool Missing,
    string? Error = null);

public sealed class UndoResult
{
    public UndoResult(IReadOnlyList<UndoOutcome> outcomes, IReadOnlyList<string>? warnings = null)
    {
        Outcomes = outcomes;
        Warnings = warnings ?? Array.Empty<string>();
    }

    public IReadOnlyList<UndoOutcome> Outcomes { get; }
    public IReadOnlyList<string> Warnings { get; }
    public int RestoredCount => Outcomes.Count(outcome => outcome.Success);
    public int MissingCount => Outcomes.Count(outcome => outcome.Missing);
    public int FailureCount => Outcomes.Count(outcome => !outcome.Success && !outcome.Missing);
}
