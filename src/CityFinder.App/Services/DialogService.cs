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
        var text =
            $"Restaurer les noms d'origine de {count} fichier(s) ?\n\n" +
            "Les fichiers seront renommés en sens inverse. Aucun fichier existant ne sera écrasé.";

        return Show(text, "Annuler les renommages", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public void ShowInfo(string title, string message) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);

    public void ShowError(string title, string message) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);

    private static MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult defaultResult)
    {
        return Owner is null
            ? MessageBox.Show(message, title, buttons, image, defaultResult)
            : MessageBox.Show(Owner, message, title, buttons, image, defaultResult);
    }
}
