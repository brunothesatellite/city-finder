using CityFinder.Core;
using CityFinder.Core.Models;
using CityFinder.Core.Naming;

namespace CityFinder.Infrastructure.Renaming;

/// <summary>
/// Executes a rename plan file by file, saves the session after each success
/// and can restore the original names later. Existing files are never
/// overwritten.
/// </summary>
public sealed class RenameExecutor
{
    private readonly ISessionStore _sessionStore;

    public RenameExecutor(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    public async Task<RenameExecutionResult> ExecuteAsync(
        string folderPath,
        RenamePlan plan,
        SessionDocument session,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var batchId = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..4];
        var outcomes = new List<RenameOutcome>();
        var warnings = new List<string>();
        var ready = plan.ReadyItems;

        for (var index = 0; index < ready.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = ready[index];
            var source = item.SourcePath;

            if (!File.Exists(source))
            {
                outcomes.Add(new RenameOutcome(item.Candidate.Id, item.Candidate.FileName,
                    item.TargetName, false, item.Renumbered, "Le fichier source n'existe plus."));
                progress?.Report(index + 1);
                continue;
            }

            var targetName = item.TargetName;
            var renumbered = item.Renumbered;
            var target = Path.Combine(Path.GetDirectoryName(source) ?? folderPath, targetName);

            if (!string.Equals(source, target, StringComparison.OrdinalIgnoreCase) && File.Exists(target))
            {
                var replacement = FindFreeTarget(item);
                if (replacement is null)
                {
                    outcomes.Add(new RenameOutcome(item.Candidate.Id, item.Candidate.FileName,
                        targetName, false, renumbered, "Le nom cible est déjà occupé."));
                    progress?.Report(index + 1);
                    continue;
                }

                targetName = replacement;
                target = Path.Combine(Path.GetDirectoryName(source) ?? folderPath, targetName);
                renumbered = true;
            }

            try
            {
                Move(source, target);
                var info = new FileInfo(target);
                UpdateSession(session, item.Candidate.FileName, targetName, item.Candidate.City,
                    item.Candidate.Text, info, batchId);

                var save = await _sessionStore.SaveAsync(folderPath, session, cancellationToken)
                    .ConfigureAwait(false);
                if (!save.Success && save.Warning is not null)
                {
                    warnings.Add(save.Warning);
                }

                outcomes.Add(new RenameOutcome(item.Candidate.Id, item.Candidate.FileName,
                    targetName, true, renumbered));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                outcomes.Add(new RenameOutcome(item.Candidate.Id, item.Candidate.FileName,
                    targetName, false, renumbered, exception.Message));
            }

            progress?.Report(index + 1);
        }

        return new RenameExecutionResult(batchId, outcomes, warnings);
    }

    public async Task<UndoResult> UndoAsync(
        string folderPath,
        SessionDocument session,
        string? batchId = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entries = session.Entries
            .Where(entry => batchId is null ||
                            string.Equals(entry.BatchId, batchId, StringComparison.Ordinal))
            .OrderBy(entry => entry.RenamedUtc)
            .ToList();

        var outcomes = new List<UndoOutcome>();
        var warnings = new List<string>();

        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[index];

            if (string.IsNullOrWhiteSpace(entry.OriginalName) ||
                string.IsNullOrWhiteSpace(entry.CurrentName))
            {
                outcomes.Add(new UndoOutcome(entry.Id, entry.CurrentName, entry.OriginalName,
                    false, false, "Entrée de session incomplète."));
                progress?.Report(index + 1);
                continue;
            }

            var source = Path.Combine(folderPath, entry.CurrentName);
            var target = Path.Combine(folderPath, entry.OriginalName);

            if (!File.Exists(source))
            {
                outcomes.Add(new UndoOutcome(entry.Id, entry.CurrentName, entry.OriginalName,
                    false, true, "Le fichier renommé est introuvable."));
                progress?.Report(index + 1);
                continue;
            }

            if (!string.Equals(source, target, StringComparison.OrdinalIgnoreCase) && File.Exists(target))
            {
                outcomes.Add(new UndoOutcome(entry.Id, entry.CurrentName, entry.OriginalName,
                    false, false, "Le nom d'origine est déjà occupé."));
                progress?.Report(index + 1);
                continue;
            }

            try
            {
                if (!string.Equals(source, target, StringComparison.Ordinal))
                {
                    Move(source, target);
                }

                session.Entries.Remove(entry);
                var save = await _sessionStore.SaveAsync(folderPath, session, cancellationToken)
                    .ConfigureAwait(false);
                if (!save.Success && save.Warning is not null)
                {
                    warnings.Add(save.Warning);
                }

                outcomes.Add(new UndoOutcome(entry.Id, entry.CurrentName, entry.OriginalName,
                    true, false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                outcomes.Add(new UndoOutcome(entry.Id, entry.CurrentName, entry.OriginalName,
                    false, false, exception.Message));
            }

            progress?.Report(index + 1);
        }

        return new UndoResult(outcomes, warnings);
    }

    private static void Move(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            // Case-only rename: go through a temporary name on Windows.
            var temporary = source + ".cityfinder-case-" + Guid.NewGuid().ToString("N")[..6];
            File.Move(source, temporary);
            try
            {
                File.Move(temporary, target);
            }
            catch
            {
                try
                {
                    File.Move(temporary, source);
                }
                catch
                {
                    // Leave the temporary name for manual recovery.
                }

                throw;
            }

            return;
        }

        File.Move(source, target, overwrite: false);
    }

    private static string? FindFreeTarget(RenameItem item)
    {
        for (var number = 2; number < 1_000; number++)
        {
            var candidate = FileNameTools.BuildTargetName(
                item.Candidate.BaseName,
                item.Candidate.City,
                item.Candidate.Text,
                item.Candidate.Extension,
                number);

            if (candidate.Length == 0)
            {
                return null;
            }

            var path = Path.Combine(item.Candidate.Folder, candidate);
            if (!File.Exists(path))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void UpdateSession(
        SessionDocument session,
        string sourceName,
        string targetName,
        string city,
        string text,
        FileInfo targetFile,
        string batchId)
    {
        var entry = session.FindByCurrentName(sourceName);
        if (entry is null)
        {
            entry = new SessionEntry
            {
                OriginalName = sourceName,
                CurrentName = targetName,
                BatchId = batchId,
            };
            session.Entries.Add(entry);
        }

        entry.CurrentName = targetName;
        entry.City = NameText.SanitizeCity(city);
        entry.Text = NameText.SanitizeText(text);
        entry.RenamedUtc = DateTime.UtcNow;
        entry.BatchId = batchId;
        entry.Length = targetFile.Exists ? targetFile.Length : 0;
        entry.LastWriteUtc = targetFile.Exists ? targetFile.LastWriteTimeUtc : DateTime.UtcNow;
    }
}
