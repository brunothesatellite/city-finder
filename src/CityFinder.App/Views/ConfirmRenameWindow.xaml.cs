using System.Windows;
using CityFinder.Core.Models;

namespace CityFinder.App.Views;

public partial class ConfirmRenameWindow : Window
{
    public ConfirmRenameWindow(RenamePlan plan, string folderPath)
    {
        InitializeComponent();

        TitleText.Text = plan.ReadyCount == 1
            ? "1 photo va être renommée"
            : $"{plan.ReadyCount} photos vont être renommées";

        FolderText.Text = folderPath;
        SummaryText.Text = $"{plan.ReadyCount} fichier(s) sélectionné(s) seront renommés.";

        var notes = new List<string>();
        if (plan.AlreadyNamedCount > 0)
        {
            notes.Add($"{plan.AlreadyNamedCount} déjà au bon nom, ignoré(s)");
        }

        if (plan.MissingCityCount > 0)
        {
            notes.Add($"{plan.MissingCityCount} sans ville, ignoré(s)");
        }

        if (plan.RenumberedCount > 0)
        {
            notes.Add($"{plan.RenumberedCount} numéroté(s) pour éviter un conflit");
        }

        NotesText.Text = notes.Count > 0
            ? string.Join("  ·  ", notes)
            : "Les noms cibles sont libres.";
        NotesText.Visibility = Visibility.Visible;

        ItemsList.ItemsSource = plan.ReadyItems
            .Select(item => new RenamePreviewRow(
                item.Candidate.FileName,
                item.TargetName,
                item.Note ?? string.Empty))
            .ToList();

        ConfirmButton.Content = plan.ReadyCount == 1
            ? "Renommer la photo"
            : $"Renommer {plan.ReadyCount} photos";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public sealed record RenamePreviewRow(string Source, string Target, string Note)
{
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    public override string ToString() => Source + " → " + Target;
}
