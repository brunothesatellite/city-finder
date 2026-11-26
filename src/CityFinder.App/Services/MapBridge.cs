using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CityFinder.App.Services;

/// <summary>
/// Bridge between the WPF UI and the local Leaflet/MapLibre page. Only map
/// data crosses the bridge; files never leave the process.
/// </summary>
public sealed class MapBridge : IMapService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly WebView2 _webView;
    private readonly string _mapFolder;
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    private IReadOnlyList<MapPointDto> _lastPoints = Array.Empty<MapPointDto>();
    private string _theme = "light";
    private bool _fitPending;
    private bool _ready;
    private bool _initialized;
    private string? _initializationError;

    public MapBridge(WebView2 webView, string mapFolder)
    {
        _webView = webView;
        _mapFolder = Path.GetFullPath(mapFolder);
    }

    public event EventHandler<string>? PhotoFocused;
    public event EventHandler<string>? InitializationFailed;

    public bool IsReady => _ready;
    public string? InitializationError => _initializationError;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        try
        {
            var runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(runtimeVersion))
            {
                throw new InvalidOperationException(
                    "Le runtime Microsoft Edge WebView2 n'est pas installé sur cet ordinateur.");
            }

            _webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CityFinder",
                "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder).ConfigureAwait(true);
            await _webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);

            var core = _webView.CoreWebView2;
            if (core is null)
            {
                throw new InvalidOperationException("WebView2 n'a pas pu démarrer.");
            }

            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.SetVirtualHostNameToFolderMapping(
                "cityfinder.local",
                _mapFolder,
                CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessageReceived;

            var navigation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
            {
                core.NavigationCompleted -= OnNavigationCompleted;
                if (args.IsSuccess)
                {
                    navigation.TrySetResult(true);
                }
                else
                {
                    navigation.TrySetException(new InvalidOperationException(
                        $"La carte locale n'a pas pu être chargée ({args.WebErrorStatus})."));
                }
            }

            core.NavigationCompleted += OnNavigationCompleted;
            core.Navigate("https://cityfinder.local/map.html");

            await navigation.Task.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(true);
            // The page posts a "ready" message once Leaflet is initialized.
            await WaitForReadyAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _initializationError = exception.Message;
            FileLog.Write("Initialisation de la carte impossible.", exception);
            InitializationFailed?.Invoke(this, exception.Message);
        }
    }

    private async Task WaitForReadyAsync(TimeSpan timeout)
    {
        var start = Environment.TickCount64;
        while (!_ready && Environment.TickCount64 - start < timeout.TotalMilliseconds)
        {
            await Task.Delay(100).ConfigureAwait(true);
        }

        if (!_ready)
        {
            throw new TimeoutException("La carte n'a pas signalé qu'elle était prête.");
        }
    }

    public async Task SetPhotosAsync(
        IReadOnlyList<MapPointDto> points,
        string theme,
        bool fit,
        CancellationToken cancellationToken = default)
    {
        _lastPoints = points;
        _theme = ThemeManager.Normalize(theme);
        _fitPending |= fit;

        if (!_ready)
        {
            return;
        }

        await FlushAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var points = _lastPoints;
        var fit = _fitPending;
        _fitPending = false;
        var json = JsonSerializer.Serialize(points, JsonOptions);
        var fitJson = fit ? "true" : "false";
        var theme = JsonSerializer.Serialize(_theme);

        await ExecuteScriptAsync(
            $"window.cityfinder && window.cityfinder.setTheme({theme});" +
            $"window.cityfinder && window.cityfinder.setPhotos({json}, {fitJson});",
            cancellationToken).ConfigureAwait(true);
    }

    public async Task FocusPhotoAsync(string id)
    {
        if (!_ready)
        {
            return;
        }

        var json = JsonSerializer.Serialize(id);
        await ExecuteScriptAsync(
            $"window.cityfinder && window.cityfinder.focusPhoto({json});").ConfigureAwait(true);
    }

    public async Task FitAllAsync()
    {
        if (!_ready)
        {
            _fitPending = true;
            return;
        }

        await ExecuteScriptAsync("window.cityfinder && window.cityfinder.fitAll();").ConfigureAwait(true);
    }

    public async Task SetThemeAsync(string theme)
    {
        _theme = ThemeManager.Normalize(theme);
        if (!_ready)
        {
            return;
        }

        var json = JsonSerializer.Serialize(_theme);
        await ExecuteScriptAsync($"window.cityfinder && window.cityfinder.setTheme({json});")
            .ConfigureAwait(true);
    }

    private async Task ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        if (!_ready && !_initialized)
        {
            return;
        }

        await _executionGate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            if (_webView.CoreWebView2 is null)
            {
                return;
            }

            await _webView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            FileLog.Write("Exécution JavaScript de la carte impossible.", exception);
        }
        finally
        {
            _executionGate.Release();
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            if (!document.RootElement.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var type = typeElement.GetString();
            if (string.Equals(type, "ready", StringComparison.Ordinal))
            {
                _ready = true;
                FileLog.Write("Carte WebView2 prête.");
                _ = FlushAsync();
                return;
            }

            if (string.Equals(type, "jsError", StringComparison.Ordinal))
            {
                FileLog.Write("Erreur JavaScript de la carte : " + document.RootElement.GetRawText());
                return;
            }

            if (string.Equals(type, "photoFocused", StringComparison.Ordinal) &&
                document.RootElement.TryGetProperty("id", out var idElement) &&
                idElement.GetString() is { Length: > 0 } id)
            {
                var handler = PhotoFocused;
                if (handler is null)
                {
                    return;
                }

                if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => handler(this, id));
                }
                else
                {
                    handler(this, id);
                }
            }
        }
        catch (Exception exception)
        {
            FileLog.Write("Message de carte illisible.", exception);
        }
    }
}
