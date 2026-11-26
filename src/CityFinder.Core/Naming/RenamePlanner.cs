using CityFinder.Core.Models;

namespace CityFinder.Core.Naming;

/// <summary>
/// Builds a safe rename plan: no overwrite, unique target names, automatic
/// <c> (2)</c>, <c> (3)</c>… suffixes when a name is already taken.
/// </summary>
public static class RenamePlanner
{
    public static RenamePlan CreatePlan(
        IEnumerable<RenameCandidate> candidates,
        IEnumerable<string>? existingFileNames = null)
    {
        var candidateList = candidates.ToList();
        var occupied = new HashSet<string>(StringComparer.Ordinal);
        if (existingFileNames is not null)
        {
            foreach (var name in existingFileNames)
            {
                var key = NameText.Fold(name);
                if (key.Length > 0)
                {
                    occupied.Add(key);
                }
            }
        }

        foreach (var candidate in candidateList)
        {
            var key = NameText.Fold(candidate.FileName);
            if (key.Length > 0)
            {
                occupied.Add(key);
            }
        }

        var planned = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<RenameItem>(candidateList.Count);

        foreach (var candidate in candidateList)
        {
            if (NameText.SanitizeCity(candidate.City).Length == 0)
            {
                items.Add(new RenameItem(candidate, candidate.FileName, RenameItemStatus.MissingCity,
                    Note: "Aucune ville n'est renseignée."));
                continue;
            }

            var target = FileNameTools.BuildTargetName(
                candidate.BaseName,
                candidate.City,
                candidate.Text,
                candidate.Extension);

            if (target.Length == 0)
            {
                items.Add(new RenameItem(candidate, candidate.FileName, RenameItemStatus.InvalidTarget,
                    Note: "Le nom cible est invalide."));
                continue;
            }

            if (string.Equals(target, candidate.FileName, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new RenameItem(candidate, candidate.FileName, RenameItemStatus.AlreadyNamed,
                    Note: "Le fichier porte déjà ce nom."));
                continue;
            }

            var renumbered = false;
            if (occupied.Contains(NameText.Fold(target)) || planned.Contains(NameText.Fold(target)))
            {
                var found = false;
                for (var number = 2; number < 10_000; number++)
                {
                    var numbered = FileNameTools.BuildTargetName(
                        candidate.BaseName,
                        candidate.City,
                        candidate.Text,
                        candidate.Extension,
                        number);

                    var numberedKey = NameText.Fold(numbered);
                    if (numberedKey.Length == 0 ||
                        occupied.Contains(numberedKey) ||
                        planned.Contains(numberedKey))
                    {
                        continue;
                    }

                    target = numbered;
                    renumbered = true;
                    found = true;
                    break;
                }

                if (!found)
                {
                    items.Add(new RenameItem(candidate, candidate.FileName, RenameItemStatus.InvalidTarget,
                        Note: "Impossible de trouver un nom libre."));
                    continue;
                }
            }

            planned.Add(NameText.Fold(target));
            items.Add(new RenameItem(
                candidate,
                target,
                RenameItemStatus.Ready,
                renumbered,
                renumbered ? "Nom déjà utilisé : un numéro a été ajouté." : null));
        }

        return new RenamePlan(items);
    }

    public static RenameCandidate ToCandidate(Photo photo) =>
        new(
            photo.Id,
            photo.FullPath,
            photo.FileName,
            photo.Extension,
            photo.BaseName,
            photo.City,
            photo.Text);
}
