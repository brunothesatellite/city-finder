using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using CityFinder.Core.Models;
using CityFinder.Core.Naming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CityFinder.App.ViewModels;

public sealed partial class PhotoViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly Photo _photo;
    private string _city;
    private string _text;
    private string? _suggestionCity;
    private bool _isSelected;
    private bool _isAlreadyRenamed;
    private bool _userEdited;
    private bool _selectionTouched;
    private bool _suppressUserEdit;
    private bool _suppressSelection;
    private BitmapSource? _thumbnail;

    public PhotoViewModel(Photo photo)
    {
        _photo = photo;
        _city = photo.City;
        _text = photo.Text;
        _suggestionCity = photo.SuggestionCity;
        _isSelected = photo.IsSelected;
        _isAlreadyRenamed = photo.IsAlreadyRenamed;
    }

    public event Action<PhotoViewModel>? Changed;

    public Photo Model => _photo;
    public string Id => _photo.Id;
    public string FileName => _photo.FileName;
    public string FullPath => _photo.FullPath;
    public string FolderPath => _photo.FolderPath;
    public string Extension => _photo.Extension;
    public bool HasGps => _photo.HasGps;
    public string CoordinateLabel => _photo.CoordinateLabel;
    public string DateLabel => (_photo.TakenAt ?? _photo.LastWriteUtc.ToLocalTime())
        .ToString("d MMM yyyy · HH:mm", French);
    public string? ReadError => _photo.ReadError;
    public bool HasReadError => !string.IsNullOrWhiteSpace(ReadError);
    public string? ExistingCity => _photo.ExistingCity;
    public bool HasExistingCity => !string.IsNullOrWhiteSpace(ExistingCity);
    public string ExistingCityText => $"Ville déjà dans le nom : {ExistingCity}";
    public string? PhotonCity => _photo.PhotonCity;
    public string? Country => _photo.Country;

    public string City
    {
        get => _city;
        set
        {
            var sanitized = value ?? string.Empty;
            if (!SetProperty(ref _city, sanitized))
            {
                return;
            }

            _photo.City = sanitized;
            if (!_suppressUserEdit)
            {
                _userEdited = true;
            }

            NotifyNameChanged();
            if (!_suppressUserEdit && HasGps && !IsAlreadyRenamed && !string.IsNullOrWhiteSpace(sanitized))
            {
                SetSelectedSilently(true);
            }
            else if (!_suppressUserEdit && IsAlreadyRenamed && IsDirty && HasGps &&
                     !string.IsNullOrWhiteSpace(sanitized))
            {
                SetSelectedSilently(true);
            }

            Changed?.Invoke(this);
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            var sanitized = value ?? string.Empty;
            if (!SetProperty(ref _text, sanitized))
            {
                return;
            }

            _photo.Text = sanitized;
            if (!_suppressUserEdit)
            {
                _userEdited = true;
            }

            NotifyNameChanged();
            if (!_suppressUserEdit && IsAlreadyRenamed && IsDirty && HasGps && !string.IsNullOrWhiteSpace(City))
            {
                SetSelectedSilently(true);
            }

            Changed?.Invoke(this);
        }
    }

    public string? SuggestionCity
    {
        get => _suggestionCity;
        private set
        {
            if (SetProperty(ref _suggestionCity, value))
            {
                OnPropertyChanged(nameof(HasCitySuggestion));
            }
        }
    }

    public bool HasCitySuggestion => !string.IsNullOrWhiteSpace(SuggestionCity);

    public bool HasCity => !string.IsNullOrWhiteSpace(City);
    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value && IsSelectionLocked)
            {
                return;
            }

            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            _photo.IsSelected = value;
            if (!_suppressSelection)
            {
                _selectionTouched = true;
            }

            Changed?.Invoke(this);
        }
    }

    public bool IsAlreadyRenamed
    {
        get => _isAlreadyRenamed;
        private set
        {
            if (!SetProperty(ref _isAlreadyRenamed, value))
            {
                return;
            }

            _photo.IsAlreadyRenamed = value;
            OnPropertyChanged(nameof(IsSelectionLocked));
            OnPropertyChanged(nameof(IsSelectionEnabled));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusTone));
        }
    }

    public bool IsFromSession => _photo.IsFromSession;

    public bool IsDirty =>
        !string.Equals(City, _photo.InitialCity, StringComparison.Ordinal) ||
        !string.Equals(Text, _photo.InitialText, StringComparison.Ordinal);

    public bool IsSelectionLocked => IsAlreadyRenamed && !IsDirty;
    public bool IsSelectionEnabled => !IsSelectionLocked;

    public bool HasUserEdited => _userEdited;

    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        private set
        {
            if (SetProperty(ref _thumbnail, value))
            {
                OnPropertyChanged(nameof(HasThumbnail));
            }
        }
    }

    public bool HasThumbnail => _thumbnail is not null;

    public string TargetNamePreview
    {
        get
        {
            var target = FileNameTools.BuildTargetName(_photo.BaseName, City, Text, _photo.Extension);
            return string.IsNullOrWhiteSpace(target)
                ? "Renseignez une ville pour voir le nouveau nom."
                : target;
        }
    }

    public string StatusText
    {
        get
        {
            if (HasReadError)
            {
                return "Fichier illisible";
            }

            if (IsAlreadyRenamed)
            {
                return "Déjà renommée";
            }

            if (!HasGps)
            {
                return "Sans GPS";
            }

            if (!HasCity)
            {
                return "Ville à confirmer";
            }

            return "Prête";
        }
    }

    public string StatusTone
    {
        get
        {
            if (HasReadError)
            {
                return "danger";
            }

            if (IsAlreadyRenamed)
            {
                return "success";
            }

            if (!HasGps || !HasCity)
            {
                return "warning";
            }

            return "muted";
        }
    }

    public string CameraLabel
    {
        get
        {
            var parts = new List<string>();
            if (_photo.Width is > 0 && _photo.Height is > 0)
            {
                parts.Add($"{_photo.Width} × {_photo.Height}");
            }

            var camera = string.Join(' ', new[] { _photo.CameraMake, _photo.CameraModel }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(camera))
            {
                parts.Add(camera);
            }

            return string.Join(" · ", parts);
        }
    }

    public void SetThumbnail(BitmapSource? thumbnail) => Thumbnail = thumbnail;

    public void NormalizeFields()
    {
        City = NameText.SanitizeCity(City);
        Text = NameText.SanitizeText(Text);
    }

    [RelayCommand]
    private void UseSuggestion()
    {
        if (string.IsNullOrWhiteSpace(SuggestionCity))
        {
            return;
        }

        City = SuggestionCity;
        _photo.SuggestionCity = null;
        SuggestionCity = null;
        Changed?.Invoke(this);
    }

    [RelayCommand]
    private void ClearText()
    {
        Text = string.Empty;
    }

    public void ApplyPhoton(GeoPlace place)
    {
        _photo.PhotonCity = place.City;
        _photo.Country = place.Country;
        _photo.State = place.State;
        _photo.County = place.County;
        OnPropertyChanged(nameof(PhotonCity));
        OnPropertyChanged(nameof(Country));

        if (IsFromSession)
        {
            return;
        }

        if (!_userEdited && string.IsNullOrWhiteSpace(ExistingCity) && string.IsNullOrWhiteSpace(City))
        {
            _suppressUserEdit = true;
            try
            {
                City = place.City;
            }
            finally
            {
                _suppressUserEdit = false;
            }

            _photo.InitialCity = City;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(IsSelectionLocked));
            OnPropertyChanged(nameof(IsSelectionEnabled));
            if (!IsAlreadyRenamed && HasGps && !_selectionTouched)
            {
                SetSelectedSilently(true);
            }
        }

        RefreshSuggestion();
        Changed?.Invoke(this);
    }

    public void ApplyExistingCityFromName(KnownCityCatalog catalog)
    {
        if (IsFromSession || _userEdited || HasExistingCity)
        {
            return;
        }

        var parsed = FileNameParser.Parse(FileName, catalog);
        if (parsed is null)
        {
            return;
        }

        _photo.BaseName = parsed.BaseName;
        _photo.ExistingCity = parsed.City;
        _photo.ExistingText = parsed.Text;
        _photo.InitialCity = parsed.City;
        _photo.InitialText = parsed.Text;

        _suppressUserEdit = true;
        try
        {
            City = parsed.City;
            Text = parsed.Text;
        }
        finally
        {
            _suppressUserEdit = false;
        }

        var target = FileNameTools.BuildTargetName(
            _photo.BaseName, City, Text, _photo.Extension);

        var wasAlreadyRenamed = IsAlreadyRenamed;
        IsAlreadyRenamed = string.Equals(target, FileName, StringComparison.OrdinalIgnoreCase);
        if (IsAlreadyRenamed)
        {
            SetSelectedSilently(false);
        }
        else if (!wasAlreadyRenamed && HasGps && !_selectionTouched)
        {
            SetSelectedSilently(true);
        }

        OnPropertyChanged(nameof(ExistingCity));
        OnPropertyChanged(nameof(ExistingCityText));
        OnPropertyChanged(nameof(HasExistingCity));
        RefreshSuggestion();
        NotifyNameChanged();
        Changed?.Invoke(this);
    }

    public void ApplyRenameSuccess(string targetName)
    {
        _photo.FileName = targetName;
        _photo.FullPath = Path.Combine(_photo.FolderPath, targetName);
        _photo.ExistingCity = City;
        _photo.ExistingText = Text;
        _photo.InitialCity = City;
        _photo.InitialText = Text;
        _photo.SuggestionCity = null;
        _photo.IsFromSession = true;
        SuggestionCity = null;
        _userEdited = false;
        _selectionTouched = false;
        IsAlreadyRenamed = true;
        SetSelectedSilently(false);

        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FullPath));
        OnPropertyChanged(nameof(ExistingCity));
        OnPropertyChanged(nameof(ExistingCityText));
        OnPropertyChanged(nameof(HasExistingCity));
        OnPropertyChanged(nameof(IsFromSession));
        NotifyNameChanged();
        Changed?.Invoke(this);
    }

    public void ApplyUndoSuccess(string originalName)
    {
        _photo.FileName = originalName;
        _photo.FullPath = Path.Combine(_photo.FolderPath, originalName);
        _photo.ExistingCity = null;
        _photo.ExistingText = null;
        _photo.InitialCity = string.Empty;
        _photo.InitialText = string.Empty;
        _photo.SuggestionCity = null;
        SuggestionCity = null;
        _userEdited = false;
        _selectionTouched = false;
        IsAlreadyRenamed = false;
        SetSelectedSilently(false);

        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FullPath));
        OnPropertyChanged(nameof(ExistingCity));
        OnPropertyChanged(nameof(HasExistingCity));
        OnPropertyChanged(nameof(IsFromSession));
        NotifyNameChanged();
        Changed?.Invoke(this);
    }

    private void RefreshSuggestion()
    {
        var suggestion = !string.IsNullOrWhiteSpace(_photo.PhotonCity) &&
                         !NameText.EqualsLoosely(_photo.PhotonCity, City)
            ? _photo.PhotonCity
            : null;

        _photo.SuggestionCity = suggestion;
        SuggestionCity = suggestion;
    }

    private void SetSelectedSilently(bool value)
    {
        _suppressSelection = true;
        try
        {
            IsSelected = value;
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    private void NotifyNameChanged()
    {
        OnPropertyChanged(nameof(HasCity));
        OnPropertyChanged(nameof(HasText));
        OnPropertyChanged(nameof(TargetNamePreview));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsSelectionLocked));
        OnPropertyChanged(nameof(IsSelectionEnabled));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusTone));
    }
}
