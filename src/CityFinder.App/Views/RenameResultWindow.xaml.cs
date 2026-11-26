using System.Windows;
using CityFinder.Core.Models;

namespace CityFinder.App.Views;

public partial class RenameResultWindow : Window
{
    public RenameResultWindow(RenameExecutionResult result)
    {
        InitializeComponent();

        TitleText.Text = result.FailureCount == 0
            ? (result.SuccessCount == 1 ? "Photo renommée" : $"{result.SuccessCount} photos renommées")
            : $"{result.SuccessCount} renommée(s), {result.FailureCount} échec(s)";

        SubtitleText.Text = result.SuccessCount == 1
            ? "Le nouveau nom est enregistré dans le dossier."
            : "Les nouveaux noms sont enregistrés dans le dossier.";

        if (result.FailureCount > 0)
        {
            FailurePanel.Visibility = Visibility.Visible;
            FailureTitle.Text = $"{result.FailureCount} fichier(s) n'ont pas pu être renommés";
            FailureList.ItemsSource = result.Outcomes
                .Where(outcome => !outcome.Success)
                .Select(outcome => $"{outcome.SourceName} : {outcome.Error}")
                .ToList();
        }

        if (result.Warnings.Count > 0)
        {
            WarningPanel.Visibility = Visibility.Visible;
            WarningList.ItemsSource = result.Warnings;
        }

        if (result.SuccessCount == 0)
        {
            UndoButton.Visibility = Visibility.Collapsed;
            FooterHint.Visibility = Visibility.Collapsed;
        }
    }

    public bool UndoRequested { get; private set; }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        UndoRequested = true;
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
