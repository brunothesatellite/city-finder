using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CityFinder.App.ViewModels;
using CityFinder.Core.Naming;

namespace CityFinder.App.Views;

public partial class BulkEditWindow : Window
{
    private readonly BulkEditKind _kind;
    private readonly IReadOnlyList<PhotoViewModel> _selected;
    private readonly IReadOnlyList<PhotoViewModel> _visible;
    private bool _ready;

    public BulkEditWindow(
        BulkEditKind kind,
        IReadOnlyList<PhotoViewModel> selected,
        IReadOnlyList<PhotoViewModel> visible)
    {
        InitializeComponent();

        _kind = kind;
        _selected = selected;
        _visible = visible;

        Configure(kind);
        SelectedScope.Content = $"Photos sélectionnées ({selected.Count})";
        VisibleScope.Content = $"Photos visibles ({visible.Count})";
        SelectedScope.IsEnabled = selected.Count > 0;

        if (selected.Count > 0)
        {
            SelectedScope.IsChecked = true;
        }
        else
        {
            VisibleScope.IsChecked = true;
        }

        _ready = true;
        ApplyButton.IsEnabled = CurrentTargets().Count > 0;
        UpdatePreview();
        Loaded += (_, _) =>
        {
            ValueInput.Focus();
            ValueInput.SelectAll();
        };
    }

    public BulkEditResult? Result { get; private set; }

    private void Configure(BulkEditKind kind)
    {
        if (kind == BulkEditKind.City)
        {
            Title = "Ville groupée";
            TitleBar.Title = Title;
            HeadingText.Text = "Renseigner la ville de plusieurs photos";
            SubtitleText.Text =
                "La même commune sera appliquée au nom cible de chaque photo. Les autres champs ne sont pas modifiés.";
            FieldLabelText.Text = "COMMUNE";
            PlaceholderText.Text = "Ex. Annecy";
            ValueInput.MaxLength = 80;
        }
        else
        {
            Title = "Texte additionnel groupé";
            TitleBar.Title = Title;
            HeadingText.Text = "Appliquer un texte à plusieurs photos";
            SubtitleText.Text =
                "Le même texte sera ajouté après la ville dans le nom cible. Les autres champs ne sont pas modifiés.";
            FieldLabelText.Text = "TEXTE ADDITIONNEL";
            PlaceholderText.Text = "Ex. week-end à Annecy, mariage…";
            ValueInput.MaxLength = 120;
        }
    }

    private IReadOnlyList<PhotoViewModel> CurrentTargets() =>
        SelectedScope.IsChecked == true ? _selected : _visible;

    private string Sanitize(string? value) => _kind == BulkEditKind.City
        ? NameText.SanitizeCity(value)
        : NameText.SanitizeText(value);

    private string CurrentValue(PhotoViewModel photo) => _kind == BulkEditKind.City
        ? photo.City
        : photo.Text;

    private void UpdatePreview()
    {
        if (!_ready)
        {
            return;
        }

        var targets = CurrentTargets();
        var value = Sanitize(ValueInput.Text);
        var overwriteCount = targets.Count(photo =>
        {
            var current = CurrentValue(photo);
            return !string.IsNullOrWhiteSpace(current) && !NameText.EqualsLoosely(current, value);
        });

        OverwritePanel.Visibility = overwriteCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        OverwriteText.Text = BuildOverwriteMessage(overwriteCount, value);

        HintText.Text = value.Length == 0
            ? _kind == BulkEditKind.City
                ? "Champ vide : la ville sera retirée des photos concernées."
                : "Champ vide : le texte additionnel sera retiré des photos concernées."
            : _kind == BulkEditKind.City
                ? "La ville est nettoyée automatiquement. Elle peut aussi être appliquée aux photos sans GPS."
                : "Le texte est ajouté après la ville, séparé par « , ». Il est nettoyé automatiquement.";

        ApplyButton.Content = value.Length == 0
            ? _kind == BulkEditKind.City ? "Effacer la ville" : "Effacer le texte"
            : "Appliquer";
        ApplyButton.IsEnabled = targets.Count > 0;

        var sample = _kind == BulkEditKind.City
            ? targets.FirstOrDefault()
            : targets.FirstOrDefault(photo => !string.IsNullOrWhiteSpace(photo.City)) ?? targets.FirstOrDefault();

        if (sample is null)
        {
            PreviewText.Text = "Aucune photo dans cette portée.";
            PreviewSourceText.Text = string.Empty;
            return;
        }

        var city = _kind == BulkEditKind.City ? value : sample.City;
        var text = _kind == BulkEditKind.Text ? value : sample.Text;
        PreviewSourceText.Text = $"Exemple : {sample.FileName}";

        if (string.IsNullOrWhiteSpace(city))
        {
            PreviewText.Text = _kind == BulkEditKind.City
                ? "Renseignez une ville pour voir le nouveau nom."
                : "Une ville est requise pour prévisualiser le nom.";
            return;
        }

        var target = FileNameTools.BuildTargetName(sample.Model.BaseName, city, text, sample.Extension);
        PreviewText.Text = string.IsNullOrWhiteSpace(target)
            ? "Renseignez une ville pour voir le nouveau nom."
            : target;
    }

    private string BuildOverwriteMessage(int count, string value)
    {
        var files = count == 1 ? "1 photo" : $"{count} photos";

        if (_kind == BulkEditKind.City)
        {
            return value.Length == 0
                ? $"{files} ont déjà une ville. Elle sera retirée après confirmation."
                : $"{files} ont déjà une ville différente. Elle sera remplacée par « {value} » après confirmation.";
        }

        return value.Length == 0
            ? $"{files} ont déjà un texte additionnel. Il sera retiré après confirmation."
            : $"{files} ont déjà un texte additionnel différent. Il sera remplacé par « {value} » après confirmation.";
    }

    private void Scope_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        ApplyButton.IsEnabled = CurrentTargets().Count > 0;
        UpdatePreview();
    }

    private void ValueInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        UpdatePreview();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var targets = CurrentTargets();
        if (targets.Count == 0)
        {
            return;
        }

        Result = new BulkEditResult(
            _kind,
            SelectedScope.IsChecked == true ? BulkEditScope.Selected : BulkEditScope.Visible,
            Sanitize(ValueInput.Text));
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TitleBar_CloseRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }
}
