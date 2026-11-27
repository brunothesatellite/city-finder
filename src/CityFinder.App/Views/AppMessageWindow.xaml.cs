using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CityFinder.App.Services;

namespace CityFinder.App.Views;

public partial class AppMessageWindow : Window
{
    public AppMessageWindow(
        string title,
        string message,
        DialogTone tone,
        string primaryText,
        string? secondaryText = null,
        bool primaryIsDestructive = false)
    {
        InitializeComponent();

        Title = title;
        TitleBar.Title = title;
        HeadingText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = primaryText;

        if (string.IsNullOrWhiteSpace(secondaryText))
        {
            SecondaryButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SecondaryButton.Content = secondaryText;
            SecondaryButton.Visibility = Visibility.Visible;
        }

        if (primaryIsDestructive)
        {
            PrimaryButton.Style = (Style)FindResource("DangerButton");
        }

        ApplyTone(tone);
    }

    private void ApplyTone(DialogTone tone)
    {
        var (backgroundKey, foregroundKey, glyph) = tone switch
        {
            DialogTone.Success => ("AccentSoft", "Accent", "✓"),
            DialogTone.Warning => ("WarningSoft", "Warning", "!"),
            DialogTone.Error => ("DangerSoft", "Danger", "×"),
            _ => ("AccentSoft", "Accent", "i"),
        };

        ToneBadge.Background = (Brush)FindResource(backgroundKey);
        ToneGlyph.Foreground = (Brush)FindResource(foregroundKey);
        ToneGlyph.Text = glyph;
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Secondary_Click(object sender, RoutedEventArgs e)
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
        else if (e.Key == Key.Enter)
        {
            // A confirmation with two choices defaults to the safe secondary
            // action; an information window simply closes.
            DialogResult = SecondaryButton.Visibility == Visibility.Visible ? false : true;
            e.Handled = true;
        }
    }
}
