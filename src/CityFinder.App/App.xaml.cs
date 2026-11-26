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

namespace CityFinder.App;

public partial class App : Application
{
    private HttpClient? _httpClient;

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

            var viewModel = new MainViewModel(
                scanner,
                geocoder,
                sessionStore,
                executor,
                new ThumbnailLoader(),
                settingsStore,
                settings,
                new DialogService());

            var startupFolder = e.Args.FirstOrDefault(argument =>
                !argument.StartsWith("--", StringComparison.Ordinal) && Directory.Exists(argument));
            var window = new MainWindow(viewModel, startupFolder);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            FileLog.Write("Démarrage impossible.", exception);
            MessageBox.Show(
                "CityFinder n'a pas pu démarrer.\n\n" + exception.Message,
                "CityFinder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        FileLog.Write("Exception d'interface non gérée.", e.Exception);
        e.Handled = true;
        MessageBox.Show(
            "Une erreur inattendue est survenue.\n\n" + e.Exception.Message +
            "\n\nLe journal se trouve dans %LOCALAPPDATA%\\CityFinder\\logs.",
            "CityFinder",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
