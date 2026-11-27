using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using CityFinder.App.Services;
using CityFinder.App.ViewModels;
using CityFinder.Infrastructure.Geocoding;
using CityFinder.Infrastructure.Renaming;
using CityFinder.Infrastructure.Scanning;
using CityFinder.Infrastructure.Session;
using CityFinder.Infrastructure.Updates;

namespace CityFinder.App;

public partial class App : Application
{
    private HttpClient? _httpClient;
    private HttpClient? _updateHttpClient;
    private DialogService? _dialogService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var culture = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("fr-FR")));

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            FileLog.Write("Exception non gérée.", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            FileLog.Write("Tâche non observée.", args.Exception);
            args.SetObserved();
        };

        try
        {
            var settingsStore = new SettingsStore();
            var settings = settingsStore.Load();
            ThemeManager.Apply(settings.Theme);

            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CityFinder");
            var cacheDirectory = Path.Combine(appData, "cache");

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            var geocoder = new PhotonGeocoder(_httpClient, cacheDirectory);
            var sessionStore = new JsonSessionStore();
            var scanner = new PhotoScanner();
            var executor = new RenameExecutor(sessionStore);
            _dialogService = new DialogService();

            _updateHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            var currentVersion = typeof(App).Assembly.GetName().Version ?? new Version(0, 1, 1);
            var updateApi = Environment.GetEnvironmentVariable("CITYFINDER_UPDATE_API");
            var updateApiUri = Uri.TryCreate(updateApi, UriKind.Absolute, out var parsedApi)
                ? parsedApi
                : null;
            var updateService = new UpdateService(_updateHttpClient, currentVersion, updateApiUri);
            UpdateInstaller.CleanupStaging();

            var viewModel = new MainViewModel(
                scanner,
                geocoder,
                sessionStore,
                executor,
                new ThumbnailLoader(),
                settingsStore,
                settings,
                _dialogService,
                updateService);

            var startupFolder = e.Args.FirstOrDefault(argument =>
                !argument.StartsWith("--", StringComparison.Ordinal) && Directory.Exists(argument));
            var window = new MainWindow(viewModel, startupFolder);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            FileLog.Write("Démarrage impossible.", exception);
            TryShowError(
                "Démarrage impossible",
                "CityFinder n'a pas pu démarrer.\n\n" + exception.Message +
                "\n\nLe journal se trouve dans %LOCALAPPDATA%\\CityFinder\\logs.");
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        FileLog.Write("Exception d'interface non gérée.", e.Exception);
        e.Handled = true;
        TryShowError(
            "Erreur inattendue",
            "Une erreur inattendue est survenue.\n\n" + e.Exception.Message +
            "\n\nLe journal se trouve dans %LOCALAPPDATA%\\CityFinder\\logs.");
    }

    private void TryShowError(string title, string message)
    {
        try
        {
            (_dialogService ??= new DialogService()).ShowError(title, message);
        }
        catch (Exception exception)
        {
            // Ultime secours si les ressources WPF elles-mêmes ne sont pas
            // disponibles : aucun autre moyen fiable d'afficher l'erreur.
            FileLog.Write("Impossible d'afficher la fenêtre d'erreur.", exception);
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        _updateHttpClient?.Dispose();
        base.OnExit(e);
    }
}
