using System.Windows;
using CityFinder.App.Views;
using CityFinder.Core.Models;

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
