using System.Windows;
using CityFinder.App.ViewModels;
using CityFinder.App.Views;
using CityFinder.Core.Models;
using CityFinder.Infrastructure.Updates;

namespace CityFinder.App.Services;

public sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow;

    public bool ConfirmRename(RenamePlan plan, string folderPath)
    {
        var window = new ConfirmRenameWindow(plan, folderPath)
        {
            Owner = Owner,
        };

        return window.ShowDialog() == true;
    }

    public bool ShowRenameResult(RenameExecutionResult result)
    {
        var window = new RenameResultWindow(result)
        {
            Owner = Owner,
        };

        window.ShowDialog();
        return window.UndoRequested;
    }

    public bool ConfirmUndo(int count)
    {
        var message =
            $"Restaurer les noms d'origine de {count} fichier(s) ?\n\n" +
            "Les fichiers seront renommés en sens inverse. Aucun fichier existant ne sera écrasé.";

        return ShowMessage(
            "Annuler les renommages",
            message,
            DialogTone.Warning,
            "Oui, restaurer",
            "Conserver les noms");
    }

    public BulkEditResult? ShowBulkEdit(
        BulkEditKind kind,
        IReadOnlyList<PhotoViewModel> selected,
        IReadOnlyList<PhotoViewModel> visible)
    {
        var window = new BulkEditWindow(kind, selected, visible)
        {
            Owner = Owner,
        };

        return window.ShowDialog() == true ? window.Result : null;
    }

    public bool ConfirmBulkOverwrite(BulkEditKind kind, int count, string value)
    {
        var files = count == 1 ? "1 photo" : $"{count} photos";
        var title = kind == BulkEditKind.City
            ? "Remplacer la ville existante"
            : "Remplacer le texte existant";

        var message = kind == BulkEditKind.City
            ? $"{files} ont déjà une ville différente.\n\n" +
              (string.IsNullOrWhiteSpace(value)
                  ? "La ville existante sera retirée."
                  : $"Elle sera remplacée par « {value} ».") +
              "\n\nContinuer ?"
            : $"{files} ont déjà un texte additionnel différent.\n\n" +
              (string.IsNullOrWhiteSpace(value)
                  ? "Le texte existant sera retiré."
                  : $"Il sera remplacé par « {value} ».") +
              "\n\nContinuer ?";

        return ShowMessage(title, message, DialogTone.Warning, "Oui, remplacer", "Annuler");
    }

    public void ShowPhotoDetails(PhotoViewModel photo)
    {
        var window = new PhotoDetailWindow(photo)
        {
            Owner = Owner,
        };

        window.ShowDialog();
    }

    public bool ShowUpdateAvailable(ReleaseInfo release, UpdateService updates)
    {
        var window = new UpdateWindow(release, updates)
        {
            Owner = Owner,
        };

        window.ShowDialog();
        return window.Postponed;
    }

    public void ShowInfo(string title, string message, DialogTone tone = DialogTone.Info) =>
        ShowMessage(title, message, tone, "Fermer");

    public void ShowError(string title, string message) =>
        ShowMessage(title, message, DialogTone.Error, "Fermer");

    private static bool ShowMessage(
        string title,
        string message,
        DialogTone tone,
        string primaryText,
        string? secondaryText = null)
    {
        var window = new AppMessageWindow(title, message, tone, primaryText, secondaryText)
        {
            Owner = Owner,
        };

        return window.ShowDialog() == true;
    }
}
