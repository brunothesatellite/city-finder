using CityFinder.Core.Models;
using CityFinder.Core.Session;

namespace CityFinder.Core.Tests;

public class SessionMatcherTests
{
    private static Photo Photo(string name, long length = 100) => new()
    {
        Id = @"C:\photos\" + name,
        FullPath = @"C:\photos\" + name,
        FileName = name,
        Extension = ".jpg",
        FolderPath = @"C:\photos",
        Length = length,
        LastWriteUtc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void MatchesCurrentNameSizeAndTime()
    {
        var photo = Photo("IMG_0001 - Annecy.jpg");
        var document = new SessionDocument
        {
            Entries =
            {
                new SessionEntry
                {
                    OriginalName = "IMG_0001.jpg",
                    CurrentName = "IMG_0001 - Annecy.jpg",
                    City = "Annecy",
                    Length = 100,
                    LastWriteUtc = photo.LastWriteUtc,
                },
            },
        };

        var result = SessionMatcher.Match(new[] { photo }, document);

        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(0, result.StaleCount);
        Assert.True(result.MatchesByPhotoId.ContainsKey(photo.Id));
    }

    [Fact]
    public void RejectsWrongSize()
    {
        var photo = Photo("IMG_0001 - Annecy.jpg");
        var document = new SessionDocument
        {
            Entries =
            {
                new SessionEntry
                {
                    OriginalName = "IMG_0001.jpg",
                    CurrentName = "IMG_0001 - Annecy.jpg",
                    Length = 999,
                    LastWriteUtc = photo.LastWriteUtc,
                },
            },
        };

        var result = SessionMatcher.Match(new[] { photo }, document);

        Assert.Equal(0, result.MatchedCount);
        Assert.Equal(1, result.StaleCount);
    }

    [Fact]
    public void MatchesNameCaseInsensitively()
    {
        var photo = Photo("img_0001 - annecy.JPG");
        var document = new SessionDocument
        {
            Entries =
            {
                new SessionEntry
                {
                    OriginalName = "IMG_0001.JPG",
                    CurrentName = "IMG_0001 - Annecy.JPG",
                    Length = 100,
                    LastWriteUtc = photo.LastWriteUtc,
                },
            },
        };

        Assert.Equal(1, SessionMatcher.Match(new[] { photo }, document).MatchedCount);
    }

    [Fact]
    public void HandlesNullSession()
    {
        var result = SessionMatcher.Match(new[] { Photo("A.jpg") }, null);
        Assert.Equal(0, result.MatchedCount);
        Assert.Equal(0, result.StaleCount);
    }
}
