using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using CityFinder.App.Services;
using CityFinder.Infrastructure.Updates;

namespace CityFinder.App.Views;

public partial class UpdateWindow : Window
{
    private readonly ReleaseInfo _release;
    private readonly UpdateService _updates;
    private CancellationTokenSource? _downloadCts;
    private string? _stagingDirectory;
    private bool _downloading;
    private bool _ready;

    public UpdateWindow(ReleaseInfo release, UpdateService updates)
    {
        InitializeComponent();

        _release = release;
        _updates = updates;
        Title = $"Mise à jour {UpdateService.FormatVersion(release.Version)}";
        TitleBar.Title = Title;

        HeadingText.Text = $"CityFinder {UpdateService.FormatVersion(release.Version)}";
        CurrentText.Text = release.Asset is { } asset
            ? $"Version actuelle : {updates.CurrentVersionLabel} · archive {FormatSize(asset.Size)}"
            : $"Version actuelle : {updates.CurrentVersionLabel}";

        NotesText.Text = string.IsNullOrWhiteSpace(release.Notes)
            ? "Aucune note de version n'est fournie pour cette release."
            : release.Notes.Trim();

        ReleaseLink.NavigateUri = new Uri(
            $"https://github.com/brunothesatellite/city-finder/releases/tag/{Uri.EscapeDataString(release.Tag)}");

        if (release.Asset is null)
        {
            StatusText.Text = "Cette release ne contient aucune archive Windows x64.";
            InstallButton.IsEnabled = false;
        }
        else if (!UpdateInstaller.CanWriteToInstallDirectory())
        {
            StatusText.Text =
                "Le dossier d'installation est en lecture seule. Téléchargez l'archive depuis GitHub et remplacez le dossier manuellement.";
            InstallButton.IsEnabled = false;
        }
        else
        {
            StatusText.Text =
                "L'archive officielle va être téléchargée et vérifiée, puis installée après redémarrage.";
        }

        Closing += OnClosing;
    }

    /// <summary>True when the user dismissed the update without restarting.</summary>
    public bool Postponed { get; private set; } = true;

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var asset = _release.Asset;
        if (asset is null || _downloading)
        {
            return;
        }

        _downloading = true;
        _ready = false;
        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;

        LaterButton.Content = "Annuler";
        InstallButton.Visibility = Visibility.Collapsed;
        RestartButton.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;
        SetStatus("Téléchargement de la mise à jour…", warning: false);

        var progress = new Progress<UpdateDownloadProgress>(value =>
        {
            ProgressBar.Value = value.Percent;
            ProgressText.Text = value.TotalBytes is > 0
                ? $"Téléchargement {value.Percent:0} % ({FormatSize(value.BytesReceived)} / {FormatSize(value.TotalBytes.Value)})"
                : $"Téléchargement {FormatSize(value.BytesReceived)}…";
        });

        try
        {
            Directory.CreateDirectory(UpdateInstaller.UpdatesRoot);

            var zipPath = await _updates.DownloadAsync(
                asset,
                UpdateInstaller.UpdatesRoot,
                progress,
                token);

            ProgressText.Text = "Extraction de l'archive…";
            ProgressBar.Value = 100;

            var stagingRoot = Path.Combine(
                UpdateInstaller.UpdatesRoot,
                "staging",
                UpdateService.FormatVersion(_release.Version));
            _stagingDirectory = await Task.Run(
                () => _updates.ExtractAppDirectory(zipPath, stagingRoot),
                token);

            TryDelete(zipPath);
            SetReadyState();
        }
        catch (OperationCanceledException)
        {
            if (IsLoaded)
            {
                SetStatus("Mise à jour annulée.", warning: false);
                ResetInstallState();
            }
        }
        catch (Exception exception)
        {
            FileLog.Write("Mise à jour impossible.", exception);
            if (IsLoaded)
            {
                SetStatus("Échec de la mise à jour : " + exception.Message, warning: true);
                ResetInstallState();
            }
        }
        finally
        {
            _downloading = false;
            if (IsLoaded && !_ready)
            {
                LaterButton.Content = "Plus tard";
                InstallButton.IsEnabled = true;
            }
        }
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_stagingDirectory))
        {
            return;
        }

        try
        {
            UpdateInstaller.Launch(_stagingDirectory);
            Postponed = false;
            Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            FileLog.Write("Installation de la mise à jour impossible.", exception);
            SetStatus("Installation impossible : " + exception.Message, warning: true);
        }
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
        Close();
    }

    private void SetReadyState()
    {
        _ready = true;
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Mise à jour prête.";
        ProgressBar.Value = 100;
        RestartButton.Visibility = Visibility.Visible;
        InstallButton.Visibility = Visibility.Collapsed;
        LaterButton.Content = "Plus tard";
        SetStatus(
            $"L'archive a été vérifiée. Redémarrez pour installer CityFinder {UpdateService.FormatVersion(_release.Version)}.",
            warning: false);
    }

    private void ResetInstallState()
    {
        ProgressPanel.Visibility = Visibility.Collapsed;
        RestartButton.Visibility = Visibility.Collapsed;
        InstallButton.Visibility = Visibility.Visible;
        InstallButton.IsEnabled = true;
    }

    private void SetStatus(string text, bool warning)
    {
        StatusText.Text = text;
        StatusText.Foreground = warning
            ? (Brush)FindResource("Warning")
            : (Brush)FindResource("InkMuted");
    }

    private void ReleaseLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            FileLog.Write("Ouverture de la page des releases impossible.", exception);
        }

        e.Handled = true;
    }

    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _downloadCts?.Cancel();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _downloadCts?.Cancel();
            Close();
            e.Handled = true;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024L => $"{bytes / (1024.0 * 1024.0):0.0} Mo",
        >= 1024L => $"{bytes / 1024.0:0} Ko",
        _ => $"{bytes} o",
    };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort; the next update check can reuse the cache.
        }
    }
}
