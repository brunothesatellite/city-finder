using CityFinder.Core.Models;
using CityFinder.Infrastructure.Session;

namespace CityFinder.Infrastructure.Tests;

public class JsonSessionStoreTests
{
    [Fact]
    public async Task RoundTripsSessionInPhotoFolder()
    {
        var folder = Directory.CreateTempSubdirectory("cityfinder-session-").FullName;
        try
        {
            var store = new JsonSessionStore(Path.Combine(folder, "fallback"));
            var document = new SessionDocument
            {
                FolderPath = folder,
                Entries =
                {
                    new SessionEntry
                    {
                        OriginalName = "IMG_0001.jpg",
                        CurrentName = "IMG_0001 - Annecy.jpg",
                        City = "Annecy",
                        Text = "été",
                        Length = 123,
                        LastWriteUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    },
                },
            };

            var saved = await store.SaveAsync(folder, document, CancellationToken.None);
            Assert.True(saved.Success);
            Assert.False(saved.UsedFallback);
            Assert.True(File.Exists(Path.Combine(folder, JsonSessionStore.FileName)));

            var loaded = await store.LoadAsync(folder, CancellationToken.None);
            Assert.NotNull(loaded.Document);
            var entry = Assert.Single(loaded.Document!.Entries);
            Assert.Equal("IMG_0001.jpg", entry.OriginalName);
            Assert.Equal("IMG_0001 - Annecy.jpg", entry.CurrentName);
            Assert.Equal("Annecy", entry.City);
            Assert.Equal("été", entry.Text);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task RemovesSessionFileWhenNoEntryRemains()
    {
        var folder = Directory.CreateTempSubdirectory("cityfinder-session-").FullName;
        try
        {
            var store = new JsonSessionStore(Path.Combine(folder, "fallback"));
            var document = new SessionDocument
            {
                Entries =
                {
                    new SessionEntry
                    {
                        OriginalName = "A.jpg",
                        CurrentName = "A - Paris.jpg",
                        City = "Paris",
                    },
                },
            };

            await store.SaveAsync(folder, document, CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(folder, JsonSessionStore.FileName)));

            document.Entries.Clear();
            var saved = await store.SaveAsync(folder, document, CancellationToken.None);
            Assert.True(saved.Success);
            Assert.False(File.Exists(Path.Combine(folder, JsonSessionStore.FileName)));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task UsesFallbackWhenPrimaryFileIsLocked()
    {
        var folder = Directory.CreateTempSubdirectory("cityfinder-session-").FullName;
        try
        {
            var primary = Path.Combine(folder, JsonSessionStore.FileName);
            await File.WriteAllTextAsync(primary, "{}");

            var store = new JsonSessionStore(Path.Combine(folder, "fallback"));
            var document = new SessionDocument
            {
                FolderPath = folder,
                Entries =
                {
                    new SessionEntry
                    {
                        OriginalName = "A.jpg",
                        CurrentName = "A - Paris.jpg",
                        City = "Paris",
                    },
                },
            };

            await using (var locked = new FileStream(primary, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var saved = await store.SaveAsync(folder, document, CancellationToken.None);
                Assert.True(saved.Success);
                Assert.True(saved.UsedFallback);
                Assert.NotNull(saved.Path);
                Assert.True(File.Exists(saved.Path));
            }
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void FallbackPathIsStable()
    {
        var store = new JsonSessionStore(Path.Combine(Path.GetTempPath(), "cityfinder-fallback"));
        var first = store.GetFallbackPath(@"C:\Photos\Voyage");
        var second = store.GetFallbackPath(@"C:\Photos\Voyage\");
        Assert.Equal(first, second);
    }

    private static void DeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                    }
                }

                Directory.Delete(folder, recursive: true);
            }
        }
        catch
        {
        }
    }
}
