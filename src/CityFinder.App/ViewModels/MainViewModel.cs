using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CityFinder.App.Models;
using CityFinder.App.Services;
using CityFinder.Core;
using CityFinder.Core.Models;
using CityFinder.Core.Naming;
using CityFinder.Core.Selection;
using CityFinder.Core.Session;
using CityFinder.Infrastructure.Geocoding;
using CityFinder.Infrastructure.Renaming;
using CityFinder.Infrastructure.Scanning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CityFinder.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly PhotoScanner _scanner;
    private readonly PhotonGeocoder _geocoder;
    private readonly ISessionStore _sessionStore;
    private readonly RenameExecutor _executor;
    private readonly ThumbnailLoader _thumbnails;
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _mapDebounce;

    private IMapService _map = new NullMapService();
    private CancellationTokenSource? _loadCts;
    private SessionDocument _session = new();
    private FilterOption? _selectedFilter;
    private PhotoViewModel? _selectedPhoto;
    private string _folderPath = string.Empty;
    private string _searchText = string.Empty;
    private string _statusMessage = "Prêt.";
    private string _geocodeStatusText = string.Empty;
    private string _busyText = string.Empty;
    private string _busySubtext = string.Empty;
    private double _busyProgress;
    private bool _isBusy;
    private bool _isGeocoding;
    private bool _isMapVisible;
    private bool _isMapReady;
    private bool _isDarkTheme;
    private bool _mapUnavailable;
    private string _mapErrorMessage = string.Empty;
    private bool _suppressStats;
    private int _matchedSessionCount;
    private int _sessionEntryCount;
    private int _totalCount;
    private int _locatedCount;
    private int _missingCount;
    private int _cityCount;
    private int _renamedCount;
    private int _selectedCount;
    private int _visibleCount;
    private int _mapPointCount;
    private int _mapCityCount;
    private string _recentFolderLabel = string.Empty;

    public MainViewModel(
        PhotoScanner scanner,
        PhotonGeocoder geocoder,
        ISessionStore sessionStore,
        RenameExecutor executor,
        ThumbnailLoader thumbnails,
        SettingsStore settingsStore,
        AppSettings settings,
        IDialogService dialogs)
    {
        _scanner = scanner;
        _geocoder = geocoder;
        _sessionStore = sessionStore;
        _executor = executor;
        _thumbnails = thumbnails;
        _settingsStore = settingsStore;
        _settings = settings;
        _dialogs = dialogs;

        _isMapVisible = settings.MapVisible;
        _isDarkTheme = ThemeManager.IsDark(settings.Theme);

        Filters = new ObservableCollection<FilterOption>
        {
            new(PhotoFilter.All, "Toutes"),
            new(PhotoFilter.ToDo, "À traiter"),
            new(PhotoFilter.MissingGps, "Sans GPS"),
            new(PhotoFilter.Renamed, "Renommées"),
        };
        _selectedFilter = Filters[0];

        PhotosView = CollectionViewSource.GetDefaultView(AllPhotos);
        PhotosView.Filter = FilterPhoto;
        PhotosView.SortDescriptions.Add(new SortDescription(nameof(PhotoViewModel.FileName),
            ListSortDirection.Ascending));

        _mapDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _mapDebounce.Tick += (_, _) =>
        {
            _mapDebounce.Stop();
            _ = PushMapAsync(false);
        };

        UpdateRecentFolder();
    }

    public ObservableCollection<PhotoViewModel> AllPhotos { get; } = new();
    public ICollectionView PhotosView { get; }
    public ObservableCollection<FilterOption> Filters { get; }

    public event Action<PhotoViewModel>? FocusPhotoRequested;

    public string FolderPath
    {
        get => _folderPath;
        private set
        {
            if (SetProperty(ref _folderPath, value))
            {
                OnPropertyChanged(nameof(FolderName));
                OnPropertyChanged(nameof(FolderTooltip));
                NotifyViewState();
            }
        }
    }

    public string FolderName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                return string.Empty;
            }

            var trimmed = FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? FolderPath : name;
        }
    }

    public string FolderTooltip => FolderPath;

    public bool HasFolder => !string.IsNullOrWhiteSpace(FolderPath);
    public bool ShowWelcome => !HasFolder;
    public bool ShowResults => HasFolder;
    public bool ShowEmptyResults => HasFolder && AllPhotos.Count == 0 && !IsBusy;
    public bool ShowNoMatches => HasFolder && AllPhotos.Count > 0 && VisibleCount == 0 && !IsBusy;
    public bool HasPhotos => AllPhotos.Count > 0;

    public FilterOption? SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value) && value is not null)
            {
                RefreshView();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshView();
            }
        }
    }

    public PhotoViewModel? SelectedPhoto
    {
        get => _selectedPhoto;
        set
        {
            if (!SetProperty(ref _selectedPhoto, value))
            {
                return;
            }

            if (value is not null)
            {
                _ = _map.FocusPhotoAsync(value.Id);
                FocusPhotoRequested?.Invoke(value);
            }

            ScheduleMap();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string GeocodeStatusText
    {
        get => _geocodeStatusText;
        private set => SetProperty(ref _geocodeStatusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            NotifyViewState();
            NotifyCommands();
        }
    }

    public bool IsGeocoding
    {
        get => _isGeocoding;
        private set => SetProperty(ref _isGeocoding, value);
    }

    public double BusyProgress
    {
        get => _busyProgress;
        private set => SetProperty(ref _busyProgress, value);
    }

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    public string BusySubtext
    {
        get => _busySubtext;
        private set => SetProperty(ref _busySubtext, value);
    }

    public bool IsMapVisible
    {
        get => _isMapVisible;
        set
        {
            if (!SetProperty(ref _isMapVisible, value))
            {
                return;
            }

            _settings.MapVisible = value;
            _settingsStore.Save(_settings);
            if (value)
            {
                _ = PushMapAsync(true);
            }
        }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        private set
        {
            if (!SetProperty(ref _isDarkTheme, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ThemeName));
            OnPropertyChanged(nameof(ThemeIcon));
        }
    }

    public string ThemeName => IsDarkTheme ? "dark" : "light";
    public string ThemeIcon => IsDarkTheme ? "☀" : "☾";

    public bool IsMapReady
    {
        get => _isMapReady;
        private set
        {
            if (SetProperty(ref _isMapReady, value))
            {
                OnPropertyChanged(nameof(MapLoading));
            }
        }
    }

    public bool MapLoading => !IsMapReady && !MapUnavailable;

    public bool MapUnavailable
    {
        get => _mapUnavailable;
        private set
        {
            if (SetProperty(ref _mapUnavailable, value))
            {
                OnPropertyChanged(nameof(MapLoading));
            }
        }
    }

    public string MapErrorMessage
    {
        get => _mapErrorMessage;
        private set => SetProperty(ref _mapErrorMessage, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public int LocatedCount
    {
        get => _locatedCount;
        private set => SetProperty(ref _locatedCount, value);
    }

    public int MissingCount
    {
        get => _missingCount;
        private set => SetProperty(ref _missingCount, value);
    }

    public int CityCount
    {
        get => _cityCount;
        private set => SetProperty(ref _cityCount, value);
    }

    public int RenamedCount
    {
        get => _renamedCount;
        private set => SetProperty(ref _renamedCount, value);
    }

    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                OnPropertyChanged(nameof(RenameButtonText));
                OnPropertyChanged(nameof(SelectionSummary));
                OnPropertyChanged(nameof(CanRename));
                NotifyCommands();
            }
        }
    }

    public int VisibleCount
    {
        get => _visibleCount;
        private set
        {
            if (SetProperty(ref _visibleCount, value))
            {
                OnPropertyChanged(nameof(ShowNoMatches));
            }
        }
    }

    public int MapPointCount
    {
        get => _mapPointCount;
        private set => SetProperty(ref _mapPointCount, value);
    }

    public int MapCityCount
    {
        get => _mapCityCount;
        private set => SetProperty(ref _mapCityCount, value);
    }

    public string RecentFolderLabel
    {
        get => _recentFolderLabel;
        private set => SetProperty(ref _recentFolderLabel, value);
    }

    public bool CanResumeLast =>
        !string.IsNullOrWhiteSpace(_settings.LastFolder) && Directory.Exists(_settings.LastFolder);

    public string HeaderTitle
    {
        get
        {
            var communes = CityCount == 1 ? "1 commune" : $"{CityCount} communes";
            var located = LocatedCount == 1 ? "1 photo localisée" : $"{LocatedCount} photos localisées";
            return $"{communes}, {located}.";
        }
    }

    public string SelectionSummary =>
        SelectedCount switch
        {
            0 => "Aucune photo sélectionnée",
            1 => "1 photo sélectionnée",
            _ => $"{SelectedCount} photos sélectionnées",
        };

    public string RenameButtonText =>
        SelectedCount switch
        {
            0 => "Renommer",
            1 => "Renommer 1 photo",
            _ => $"Renommer {SelectedCount} photos",
        };

    public bool CanRename => SelectedCount > 0 && !IsBusy;

    public int SessionEntryCount
    {
        get => _sessionEntryCount;
        private set
        {
            if (SetProperty(ref _sessionEntryCount, value))
            {
                OnPropertyChanged(nameof(HasUndoEntries));
                OnPropertyChanged(nameof(ShowResumeBanner));
                OnPropertyChanged(nameof(ResumeSummary));
                NotifyCommands();
            }
        }
    }

    public int MatchedSessionCount
    {
        get => _matchedSessionCount;
        private set
        {
            if (SetProperty(ref _matchedSessionCount, value))
            {
                OnPropertyChanged(nameof(ResumeSummary));
            }
        }
    }

    public bool HasUndoEntries => SessionEntryCount > 0 && HasFolder && !IsBusy;
    public bool ShowResumeBanner => HasFolder && SessionEntryCount > 0 && !IsBusy;

    public string ResumeSummary
    {
        get
        {
            if (SessionEntryCount == 0)
            {
                return string.Empty;
            }

            if (MatchedSessionCount == 0)
            {
                return $"{SessionEntryCount} renommage(s) enregistré(s), aucun fichier correspondant retrouvé.";
            }

            return $"{MatchedSessionCount} fichier(s) déjà renommé(s) · {SessionEntryCount} entrée(s) de session.";
        }
    }

    public string MapPointLabel => MapPointCount == 1 ? "1 point" : $"{MapPointCount} points";
    public string MapCityLabel => MapCityCount == 1 ? "1 commune" : $"{MapCityCount} communes";

    public bool CanOpenFolder => !IsBusy;
    public bool CanUndo => HasUndoEntries;

    public async Task InitializeAsync(string? startupFolder = null)
    {
        UpdateRecentFolder();
        if (!string.IsNullOrWhiteSpace(startupFolder) && Directory.Exists(startupFolder))
        {
            await LoadFolderAsync(startupFolder);
        }
    }

    public void AttachMap(IMapService map)
    {
        _map.PhotoFocused -= OnMapPhotoFocused;
        _map = map;
        _map.PhotoFocused += OnMapPhotoFocused;
        if (HasFolder)
        {
            _ = PushMapAsync(true);
        }
    }

    public void SetMapReady()
    {
        IsMapReady = true;
        MapUnavailable = false;
        if (HasFolder)
        {
            _ = PushMapAsync(true);
        }
    }

    public void SetMapUnavailable(string message)
    {
        MapErrorMessage = "La carte n'a pas pu démarrer : " + message;
        IsMapReady = false;
        MapUnavailable = true;
    }

    [RelayCommand]
    private async Task FitMapAsync()
    {
        await _map.FitAllAsync();
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Choisir le dossier de photos",
                Multiselect = false,
            };

            if (!string.IsNullOrWhiteSpace(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            {
                dialog.InitialDirectory = _settings.LastFolder;
            }

            if (dialog.ShowDialog() == true)
            {
                await LoadFolderAsync(dialog.FolderName);
            }
        }
        catch (Exception exception)
        {
            FileLog.Write("Sélecteur de dossier indisponible.", exception);
            _dialogs.ShowError("Dossier", "Le sélecteur de dossier n'a pas pu s'ouvrir.\n" + exception.Message);
        }
    }

    [RelayCommand]
    private async Task OpenFolderPathAsync(string? path)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!Directory.Exists(path))
        {
            _dialogs.ShowError("Dossier introuvable", $"Le dossier n'existe plus :\n{path}");
            UpdateRecentFolder();
            return;
        }

        await LoadFolderAsync(path);
    }

    [RelayCommand]
    private async Task ResumeLastAsync()
    {
        if (CanResumeLast)
        {
            await OpenFolderPathAsync(_settings.LastFolder);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (HasFolder && !IsBusy)
        {
            await LoadFolderAsync(FolderPath);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRename))]
    private async Task RenameAsync()
    {
        if (!HasFolder || IsBusy)
        {
            return;
        }

        var selected = AllPhotos.Where(photo => photo.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _dialogs.ShowInfo("Aucune sélection", "Cochez au moins une photo à renommer.");
            return;
        }

        RenamePlan plan;
        try
        {
            var existingNames = Directory.EnumerateFiles(FolderPath)
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToList();

            plan = RenamePlanner.CreatePlan(
                selected.Select(photo => RenamePlanner.ToCandidate(photo.Model)),
                existingNames);
        }
        catch (Exception exception)
        {
            _dialogs.ShowError("Préparation du renommage", exception.Message);
            return;
        }

        if (!plan.HasWork)
        {
            var details = new List<string>();
            if (plan.AlreadyNamedCount > 0)
            {
                details.Add($"{plan.AlreadyNamedCount} fichier(s) portent déjà le nom cible.");
            }

            if (plan.MissingCityCount > 0)
            {
                details.Add($"{plan.MissingCityCount} photo(s) n'ont pas de ville.");
            }

            _dialogs.ShowInfo("Aucun renommage",
                "Aucun fichier ne peut être renommé pour le moment." +
                (details.Count > 0 ? "\n\n" + string.Join("\n", details) : string.Empty),
                DialogTone.Warning);
            return;
        }

        if (!_dialogs.ConfirmRename(plan, FolderPath))
        {
            return;
        }

        IsBusy = true;
        BusyText = "Renommage des photos…";
        BusySubtext = string.Empty;
        BusyProgress = 0;

        try
        {
            var progress = new Progress<int>(done =>
            {
                BusyText = $"Renommage {done}/{plan.ReadyCount}";
                BusyProgress = plan.ReadyCount == 0 ? 0 : done * 100.0 / plan.ReadyCount;
            });

            var result = await Task.Run(() =>
                _executor.ExecuteAsync(FolderPath, plan, _session, progress, CancellationToken.None));

            foreach (var outcome in result.Outcomes.Where(outcome => outcome.Success))
            {
                AllPhotos.FirstOrDefault(photo => photo.Id == outcome.PhotoId)
                    ?.ApplyRenameSuccess(outcome.TargetName);
            }

            _suppressStats = true;
            PhotosView.Refresh();
            _suppressStats = false;

            SessionEntryCount = _session.Entries.Count;
            MatchedSessionCount = AllPhotos.Count(photo => photo.IsFromSession);
            NotifyStats();
            ScheduleMap();

            StatusMessage = result.FailureCount == 0
                ? $"{result.SuccessCount} photo(s) renommée(s)."
                : $"{result.SuccessCount} renommée(s), {result.FailureCount} échec(s).";

            var undoRequested = _dialogs.ShowRenameResult(result);
            if (undoRequested && result.SuccessCount > 0)
            {
                await UndoBatchAsync(result.BatchId);
            }
        }
        catch (Exception exception)
        {
            FileLog.Write("Échec du renommage.", exception);
            _dialogs.ShowError("Renommage", exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAllAsync()
    {
        if (!HasFolder || _session.Entries.Count == 0)
        {
            return;
        }

        if (!_dialogs.ConfirmUndo(_session.Entries.Count))
        {
            return;
        }

        await UndoCoreAsync(null);
    }

    private async Task UndoBatchAsync(string batchId)
    {
        await UndoCoreAsync(batchId);
    }

    private async Task UndoCoreAsync(string? batchId)
    {
        IsBusy = true;
        BusyText = "Restauration des noms d'origine…";
        BusySubtext = string.Empty;
        BusyProgress = 0;

        try
        {
            var progress = new Progress<int>(done =>
            {
                BusyText = $"Restauration {done}";
                BusyProgress = Math.Min(95, done * 8);
            });

            var result = await Task.Run(() =>
                _executor.UndoAsync(FolderPath, _session, batchId, progress, CancellationToken.None));

            IsBusy = false;
            var summary = new List<string>
            {
                $"{result.RestoredCount} nom(s) d'origine restauré(s).",
            };
            if (result.MissingCount > 0)
            {
                summary.Add($"{result.MissingCount} fichier(s) introuvable(s).");
            }

            if (result.FailureCount > 0)
            {
                summary.Add($"{result.FailureCount} échec(s).");
            }

            foreach (var failure in result.Outcomes.Where(outcome => !outcome.Success).Take(8))
            {
                summary.Add($"• {failure.CurrentName} : {failure.Error}");
            }

            foreach (var warning in result.Warnings.Take(3))
            {
                summary.Add("• " + warning);
            }

            _dialogs.ShowInfo("Annulation terminée", string.Join("\n", summary), DialogTone.Success);
            await LoadFolderAsync(FolderPath);
        }
        catch (Exception exception)
        {
            FileLog.Write("Échec de l'annulation.", exception);
            _dialogs.ShowError("Annulation", exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ThemeManager.Apply(ThemeName);
        _settings.Theme = ThemeName;
        _settingsStore.Save(_settings);
        _ = _map.SetThemeAsync(ThemeName);
    }

    [RelayCommand]
    private void ToggleMap()
    {
        IsMapVisible = !IsMapVisible;
    }

    private async Task LoadFolderAsync(string folderPath)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var token = cts.Token;

        IsBusy = true;
        IsGeocoding = false;
        GeocodeStatusText = string.Empty;
        BusyProgress = 0;
        BusyText = "Analyse du dossier…";
        BusySubtext = folderPath;
        StatusMessage = "Analyse des métadonnées…";
        _mapDebounce.Stop();
        SelectedPhoto = null;

        FolderPath = folderPath;
        foreach (var photo in AllPhotos)
        {
            photo.Changed -= OnPhotoChanged;
        }

        AllPhotos.Clear();
        VisibleCount = 0;
        _session = new SessionDocument { FolderPath = folderPath };
        SessionEntryCount = 0;
        MatchedSessionCount = 0;
        PhotosView.Refresh();
        NotifyViewState();
        NotifyStats();

        try
        {
            var sessionLoad = await _sessionStore.LoadAsync(folderPath, token);
            if (sessionLoad.Document is not null)
            {
                _session = sessionLoad.Document;
            }

            if (!string.IsNullOrWhiteSpace(sessionLoad.Warning))
            {
                StatusMessage = sessionLoad.Warning;
            }

            var progress = new Progress<ScanProgress>(value =>
            {
                BusyText = value.Total == 0
                    ? "Analyse du dossier…"
                    : $"Analyse {value.Processed}/{value.Total}";
                BusySubtext = value.FileName;
                BusyProgress = value.Total == 0 ? 0 : value.Processed * 100.0 / value.Total;
            });

            var scan = await _scanner.ScanAsync(
                folderPath,
                _settings.IncludeSubfolders,
                progress,
                token);

            token.ThrowIfCancellationRequested();

            var match = SessionMatcher.Match(scan.Photos, _session);
            var catalog = KnownCities.CreateCatalog(_session.Entries.Select(entry => entry.City));
            foreach (var photo in scan.Photos)
            {
                match.MatchesByPhotoId.TryGetValue(photo.Id, out var entry);
                InitialStatePolicy.Apply(photo, entry, catalog);
            }

            _suppressStats = true;
            foreach (var photo in scan.Photos)
            {
                var viewModel = new PhotoViewModel(photo);
                viewModel.Changed += OnPhotoChanged;
                AllPhotos.Add(viewModel);
            }

            _suppressStats = false;

            SessionEntryCount = _session.Entries.Count;
            MatchedSessionCount = match.MatchedCount;
            PhotosView.Refresh();
            NotifyViewState();
            NotifyStats();

            _settings.LastFolder = folderPath;
            _settingsStore.Save(_settings);
            UpdateRecentFolder();

            StatusMessage = scan.Photos.Count == 0
                ? "Aucune photo compatible dans ce dossier."
                : $"{scan.Photos.Count} fichier(s) analysé(s) · {scan.Photos.Count(photo => photo.HasGps)} géolocalisé(s).";

            if (scan.Warnings.Count > 0)
            {
                StatusMessage += $" {scan.Warnings.Count} avertissement(s).";
                FileLog.Write("Avertissements d'analyse : " + string.Join(" | ", scan.Warnings.Take(10)));
            }

            IsBusy = false;
            await PushMapAsync(true);

            if (AllPhotos.Count > 0)
            {
                _ = LoadThumbnailsAsync(cts);
                _ = GeocodeAsync(cts);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            FileLog.Write("Analyse du dossier impossible.", exception);
            _dialogs.ShowError("Analyse du dossier", exception.Message);
            FolderPath = string.Empty;
            AllPhotos.Clear();
            PhotosView.Refresh();
            NotifyViewState();
            NotifyStats();
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                IsBusy = false;
            }
        }
    }

    private async Task LoadThumbnailsAsync(CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            foreach (var photo in AllPhotos.ToList())
            {
                token.ThrowIfCancellationRequested();
                var path = photo.FullPath;
                var orientation = photo.Model.Orientation;
                var bitmap = await Task.Run(() => _thumbnails.Load(path, orientation), token)
                    .ConfigureAwait(true);
                token.ThrowIfCancellationRequested();
                photo.SetThumbnail(bitmap);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            FileLog.Write("Chargement des vignettes interrompu.", exception);
        }
    }

    private async Task GeocodeAsync(CancellationTokenSource cts)
    {
        var token = cts.Token;
        var candidates = AllPhotos
            .Where(photo => photo.HasGps && photo.Model.Latitude is not null &&
                            photo.Model.Longitude is not null && !photo.IsFromSession)
            .ToList();

        var groups = candidates
            .GroupBy(photo => (
                Lat: Math.Round(photo.Model.Latitude!.Value, 5),
                Lon: Math.Round(photo.Model.Longitude!.Value, 5)))
            .ToList();

        if (groups.Count == 0)
        {
            return;
        }

        IsGeocoding = true;
        GeocodeStatusText = $"Recherche des communes… 0/{groups.Count}";

        var done = 0;
        var failedPhotos = 0;
        var consecutiveFailures = 0;

        try
        {
            foreach (var group in groups)
            {
                token.ThrowIfCancellationRequested();
                var result = await _geocoder.ReverseAsync(group.Key.Lat, group.Key.Lon, token);
                done++;

                if (result.Status == GeocodeStatus.Found && result.Place is not null)
                {
                    consecutiveFailures = 0;
                    _suppressStats = true;
                    try
                    {
                        foreach (var photo in group)
                        {
                            photo.ApplyPhoton(result.Place);
                        }
                    }
                    finally
                    {
                        _suppressStats = false;
                    }
                }
                else if (result.Status == GeocodeStatus.Failed)
                {
                    failedPhotos += group.Count();
                    consecutiveFailures++;
                }
                else
                {
                    consecutiveFailures = 0;
                }

                GeocodeStatusText = $"Recherche des communes… {done}/{groups.Count}";

                if (done % 4 == 0)
                {
                    NotifyStats();
                }

                if (consecutiveFailures >= 3)
                {
                    GeocodeStatusText = "Photon indisponible — saisissez les villes manuellement.";
                    break;
                }
            }

            var catalog = KnownCities.CreateCatalog(
                AllPhotos.Select(photo => photo.Model.PhotonCity)
                    .Concat(_session.Entries.Select(entry => entry.City)));

            _suppressStats = true;
            try
            {
                foreach (var photo in AllPhotos)
                {
                    photo.ApplyExistingCityFromName(catalog);
                }
            }
            finally
            {
                _suppressStats = false;
            }

            PhotosView.Refresh();
            NotifyStats();
            ScheduleMap();

            if (failedPhotos > 0)
            {
                StatusMessage = $"{failedPhotos} photo(s) sans réponse de Photon — la ville peut être saisie manuellement.";
            }
            else
            {
                StatusMessage = $"{groups.Count} recherche(s) de commune effectuée(s).";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            FileLog.Write("Recherche Photon interrompue.", exception);
            StatusMessage = "Recherche des communes interrompue. Vous pouvez saisir les villes manuellement.";
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                IsGeocoding = false;
                GeocodeStatusText = string.Empty;
            }
        }
    }

    private void OnPhotoChanged(PhotoViewModel photo)
    {
        if (_suppressStats)
        {
            return;
        }

        NotifyStats();
        ScheduleMap();
    }

    private void NotifyStats()
    {
        var stats = PhotoStatisticsCalculator.Calculate(AllPhotos.Select(photo => photo.Model));
        TotalCount = stats.Total;
        LocatedCount = stats.Located;
        MissingCount = stats.MissingGps;
        CityCount = stats.Cities;
        RenamedCount = stats.Renamed;
        SelectedCount = stats.Selected;

        if (Filters.Count == 4)
        {
            Filters[0].Count = stats.Total;
            Filters[1].Count = AllPhotos.Count(photo => !photo.IsAlreadyRenamed);
            Filters[2].Count = stats.MissingGps;
            Filters[3].Count = stats.Renamed;
        }

        var visible = PhotosView.Cast<PhotoViewModel>().ToList();
        VisibleCount = visible.Count;
        var locatedVisible = visible.Where(photo => photo.HasGps).ToList();
        MapPointCount = locatedVisible.Count;
        MapCityCount = locatedVisible
            .Select(photo => photo.City?.Trim())
            .Where(city => !string.IsNullOrWhiteSpace(city))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(MapPointLabel));
        OnPropertyChanged(nameof(MapCityLabel));
        OnPropertyChanged(nameof(ShowEmptyResults));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(HasUndoEntries));
        OnPropertyChanged(nameof(ShowResumeBanner));
        OnPropertyChanged(nameof(ResumeSummary));
        NotifyCommands();
    }

    private void NotifyViewState()
    {
        OnPropertyChanged(nameof(HasFolder));
        OnPropertyChanged(nameof(ShowWelcome));
        OnPropertyChanged(nameof(ShowResults));
        OnPropertyChanged(nameof(ShowEmptyResults));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(HasPhotos));
        OnPropertyChanged(nameof(HasUndoEntries));
        OnPropertyChanged(nameof(ShowResumeBanner));
        OnPropertyChanged(nameof(CanResumeLast));
        OnPropertyChanged(nameof(CanOpenFolder));
    }

    private void NotifyCommands()
    {
        RenameCommand.NotifyCanExecuteChanged();
        UndoAllCommand.NotifyCanExecuteChanged();
    }

    private void UpdateRecentFolder()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
        {
            var trimmed = _settings.LastFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            RecentFolderLabel = string.IsNullOrWhiteSpace(name) ? _settings.LastFolder : name;
        }
        else
        {
            RecentFolderLabel = string.Empty;
        }

        OnPropertyChanged(nameof(CanResumeLast));
    }

    private void RefreshView()
    {
        PhotosView.Refresh();
        if (SelectedPhoto is not null && !PhotosView.Contains(SelectedPhoto))
        {
            SelectedPhoto = null;
        }

        VisibleCount = PhotosView.Cast<PhotoViewModel>().Count();
        NotifyStats();
        ScheduleMap();
    }

    private bool FilterPhoto(object item)
    {
        if (item is not PhotoViewModel photo)
        {
            return false;
        }

        var filter = SelectedFilter?.Kind ?? PhotoFilter.All;
        var matchesFilter = filter switch
        {
            PhotoFilter.ToDo => !photo.IsAlreadyRenamed,
            PhotoFilter.MissingGps => !photo.HasGps,
            PhotoFilter.Renamed => photo.IsAlreadyRenamed,
            _ => true,
        };

        if (!matchesFilter)
        {
            return false;
        }

        var query = NameText.Fold(SearchText);
        if (query.Length == 0)
        {
            return true;
        }

        return NameText.Fold(photo.FileName).Contains(query, StringComparison.Ordinal) ||
               NameText.Fold(photo.City).Contains(query, StringComparison.Ordinal) ||
               NameText.Fold(photo.Text).Contains(query, StringComparison.Ordinal);
    }

    private void ScheduleMap()
    {
        if (!HasFolder)
        {
            return;
        }

        _mapDebounce.Stop();
        _mapDebounce.Start();
    }

    private async Task PushMapAsync(bool fit)
    {
        if (!HasFolder)
        {
            return;
        }

        var points = PhotosView.Cast<PhotoViewModel>()
            .Where(photo => photo.HasGps)
            .Select(photo => new MapPointDto(
                photo.Id,
                photo.Model.Latitude!.Value,
                photo.Model.Longitude!.Value,
                photo.FileName,
                photo.City,
                photo.Text,
                photo.IsSelected,
                ReferenceEquals(photo, SelectedPhoto),
                photo.StatusText,
                photo.DateLabel))
            .ToList();

        await _map.SetPhotosAsync(points, ThemeName, fit);
    }

    private void OnMapPhotoFocused(object? sender, string id)
    {
        var photo = AllPhotos.FirstOrDefault(candidate => candidate.Id == id);
        if (photo is null)
        {
            return;
        }

        if (!PhotosView.Contains(photo))
        {
            SelectedFilter = Filters[0];
            SearchText = string.Empty;
        }

        SelectedPhoto = photo;
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _mapDebounce.Stop();
        _map.PhotoFocused -= OnMapPhotoFocused;
        foreach (var photo in AllPhotos)
        {
            photo.Changed -= OnPhotoChanged;
        }
    }
}
