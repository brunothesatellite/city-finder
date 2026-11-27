using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using CityFinder.App.Services;
using CityFinder.App.ViewModels;

namespace CityFinder.App.Views;

public partial class PhotoDetailWindow : Window
{
    private readonly PhotoViewModel _photo;
    private readonly ThumbnailLoader _loader = new();
    private readonly CancellationTokenSource _cts = new();

    public PhotoDetailWindow(PhotoViewModel photo)
    {
        InitializeComponent();

        _photo = photo;
        DataContext = photo;
        DetailImage.Source = photo.Thumbnail;

        if (photo.Thumbnail is not null)
        {
            ImageLoading.Visibility = Visibility.Collapsed;
        }

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var token = _cts.Token;
        try
        {
            var image = await Task.Run(
                () => _loader.Load(_photo.FullPath, _photo.Model.Orientation, 1800),
                token);

            if (token.IsCancellationRequested || !IsLoaded)
            {
                return;
            }

            if (image is not null)
            {
                DetailImage.Source = image;
                ImagePlaceholder.Visibility = Visibility.Collapsed;
            }
            else if (DetailImage.Source is null)
            {
                ImagePlaceholder.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            FileLog.Write("Aperçu détaillé impossible.", exception);
            if (DetailImage.Source is null)
            {
                ImagePlaceholder.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                ImageLoading.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void Field_LostFocus(object sender, RoutedEventArgs e)
    {
        _photo.NormalizeFields();
    }

    private void OpenOsm_Click(object sender, RoutedEventArgs e)
    {
        var latitude = _photo.Model.Latitude;
        var longitude = _photo.Model.Longitude;
        if (latitude is null || longitude is null)
        {
            return;
        }

        var url = FormattableString.Invariant(
            $"https://www.openstreetmap.org/?mlat={latitude:0.00000}&mlon={longitude:0.00000}#map=15/{latitude:0.00000}/{longitude:0.00000}");

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            FileLog.Write("Ouverture d'OpenStreetMap impossible.", exception);
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        _photo.NormalizeFields();
        Close();
    }

    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _photo.NormalizeFields();
            Close();
            e.Handled = true;
        }
    }
}
