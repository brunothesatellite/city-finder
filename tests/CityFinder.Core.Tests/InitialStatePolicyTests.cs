using CityFinder.Core.Models;
using CityFinder.Core.Naming;
using CityFinder.Core.Selection;

namespace CityFinder.Core.Tests;

public class InitialStatePolicyTests
{
    private static readonly KnownCityCatalog Catalog = KnownCityCatalog.Create(new[]
    {
        "Annecy", "Chambéry", "Paris",
    });

    private static Photo Photo(string fileName, string? photonCity, bool hasGps = true) => new()
    {
        Id = @"C:\photos\" + fileName,
        FullPath = @"C:\photos\" + fileName,
        FileName = fileName,
        Extension = Path.GetExtension(fileName),
        FolderPath = @"C:\photos",
        Length = 100,
        LastWriteUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Latitude = hasGps ? 48.89 : null,
        Longitude = hasGps ? -0.2 : null,
        PhotonCity = photonCity,
    };

    [Fact]
    public void SelectsGeotaggedPhotoWithPhotonCity()
    {
        var photo = Photo("IMG_0001.jpg", "Annecy");
        InitialStatePolicy.Apply(photo, null, Catalog);

        Assert.Equal("Annecy", photo.City);
        Assert.Equal("IMG_0001", photo.BaseName);
        Assert.True(photo.IsSelected);
        Assert.False(photo.IsAlreadyRenamed);
    }

    [Fact]
    public void DoesNotSelectPhotoWithoutGps()
    {
        var photo = Photo("IMG_0001.jpg", "Annecy", hasGps: false);
        InitialStatePolicy.Apply(photo, null, Catalog);

        Assert.False(photo.IsSelected);
    }

    [Fact]
    public void DetectsExistingCityAndSkipsAlreadyNamedFile()
    {
        var photo = Photo("IMG_0001 - Annecy.jpg", "Annecy");
        InitialStatePolicy.Apply(photo, null, Catalog);

        Assert.Equal("IMG_0001", photo.BaseName);
        Assert.Equal("Annecy", photo.City);
        Assert.Equal("Annecy", photo.ExistingCity);
        Assert.True(photo.IsAlreadyRenamed);
        Assert.False(photo.IsSelected);
    }

    [Fact]
    public void KeepsExistingCityAndSuggestsDifferentPhotonCity()
    {
        var photo = Photo("IMG_0001 - Annecy.jpg", "Chambéry");
        InitialStatePolicy.Apply(photo, null, Catalog);

        Assert.Equal("Annecy", photo.City);
        Assert.Equal("Annecy", photo.ExistingCity);
        Assert.Equal("Chambéry", photo.SuggestionCity);
        Assert.False(photo.IsSelected);
        Assert.True(photo.IsAlreadyRenamed);
    }

    [Fact]
    public void SelectsExternalCityWhenNameIsNotFinalYet()
    {
        var photo = Photo("IMG_0001 - Annecy vacances.jpg", "Chambéry");
        InitialStatePolicy.Apply(photo, null, Catalog);

        Assert.Equal("Annecy", photo.City);
        Assert.Equal("vacances", photo.Text);
        Assert.Equal("Chambéry", photo.SuggestionCity);
        Assert.True(photo.IsSelected);
        Assert.False(photo.IsAlreadyRenamed);
    }

    [Fact]
    public void ExistingCityWithTextIsParsed()
    {
        var photo = Photo("IMG_0001 - Annecy , été.jpg", "Annecy");
        InitialStatePolicy.Apply(photo, null, Catalog);

        Assert.Equal("IMG_0001", photo.BaseName);
        Assert.Equal("été", photo.Text);
        Assert.Equal("été", photo.ExistingText);
    }

    [Fact]
    public void SessionEntryIsAlreadyRenamedAndNotSelected()
    {
        var photo = Photo("IMG_0001 - Annecy.jpg", null);
        var entry = new SessionEntry
        {
            OriginalName = "IMG_0001.jpg",
            CurrentName = "IMG_0001 - Annecy.jpg",
            City = "Annecy",
            Text = string.Empty,
        };

        InitialStatePolicy.Apply(photo, entry, Catalog);

        Assert.True(photo.IsFromSession);
        Assert.True(photo.IsAlreadyRenamed);
        Assert.False(photo.IsSelected);
        Assert.Equal("IMG_0001", photo.BaseName);
        Assert.Equal("IMG_0001.jpg", photo.SessionOriginalName);
    }

    [Fact]
    public void UnexpectedSuffixIsNotTreatedAsCity()
    {
        var photo = Photo("IMG_0001 - Village-Inconnu.jpg", "Annecy");
        InitialStatePolicy.Apply(photo, null, Catalog);

        Assert.Null(photo.ExistingCity);
        Assert.Equal("Annecy", photo.City);
        Assert.Equal("IMG_0001 - Village-Inconnu", photo.BaseName);
    }
}
