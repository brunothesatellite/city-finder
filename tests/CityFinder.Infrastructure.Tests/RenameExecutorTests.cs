using CityFinder.Core.Models;
using CityFinder.Core.Naming;
using CityFinder.Infrastructure.Renaming;
using CityFinder.Infrastructure.Session;

namespace CityFinder.Infrastructure.Tests;

public class RenameExecutorTests
{
    [Fact]
    public async Task RenamesFilesUpdatesSessionAndUndoes()
    {
        var folder = Directory.CreateTempSubdirectory("cityfinder-rename-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "A.jpg"), "a");
            File.WriteAllText(Path.Combine(folder, "B.JPG"), "b");

            var plan = RenamePlanner.CreatePlan(
                new[]
                {
                    Candidate(folder, "A.jpg", "A", "Paris"),
                    Candidate(folder, "B.JPG", "B", "Lyon"),
                },
                Directory.EnumerateFiles(folder).Select(Path.GetFileName)!);

            Assert.Equal(2, plan.ReadyCount);

            var store = new JsonSessionStore(Path.Combine(folder, "fallback"));
            var session = new SessionDocument { FolderPath = folder };
            var executor = new RenameExecutor(store);
            var result = await executor.ExecuteAsync(folder, plan, session, cancellationToken: CancellationToken.None);

            Assert.Equal(2, result.SuccessCount);
            Assert.True(File.Exists(Path.Combine(folder, "A - Paris.jpg")));
            Assert.True(File.Exists(Path.Combine(folder, "B - Lyon.JPG")));
            Assert.Equal(2, session.Entries.Count);

            var loaded = await store.LoadAsync(folder, CancellationToken.None);
            Assert.Equal(2, loaded.Document!.Entries.Count);

            var undo = await executor.UndoAsync(folder, session, cancellationToken: CancellationToken.None);
            Assert.Equal(2, undo.RestoredCount);
            Assert.True(File.Exists(Path.Combine(folder, "A.jpg")));
            Assert.True(File.Exists(Path.Combine(folder, "B.JPG")));
            Assert.False(File.Exists(Path.Combine(folder, "A - Paris.jpg")));
            Assert.Empty(session.Entries);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task RenumbersLateCollisionWithoutOverwriting()
    {
        var folder = Directory.CreateTempSubdirectory("cityfinder-rename-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "A.jpg"), "source");
            var plan = RenamePlanner.CreatePlan(
                new[] { Candidate(folder, "A.jpg", "A", "Paris") },
                new[] { "A.jpg" });

            File.WriteAllText(Path.Combine(folder, "A - Paris.jpg"), "do not touch");

            var store = new JsonSessionStore(Path.Combine(folder, "fallback"));
            var session = new SessionDocument { FolderPath = folder };
            var executor = new RenameExecutor(store);
            var result = await executor.ExecuteAsync(folder, plan, session, cancellationToken: CancellationToken.None);

            Assert.Equal(1, result.SuccessCount);
            Assert.True(result.Outcomes[0].Renumbered);
            Assert.Equal("A - Paris (2).jpg", result.Outcomes[0].TargetName);
            Assert.Equal("do not touch", File.ReadAllText(Path.Combine(folder, "A - Paris.jpg")));
            Assert.True(File.Exists(Path.Combine(folder, "A - Paris (2).jpg")));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task UndoRefusesToOverwriteExistingOriginal()
    {
        var folder = Directory.CreateTempSubdirectory("cityfinder-rename-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "A.jpg"), "source");
            var plan = RenamePlanner.CreatePlan(
                new[] { Candidate(folder, "A.jpg", "A", "Paris") },
                new[] { "A.jpg" });

            var store = new JsonSessionStore(Path.Combine(folder, "fallback"));
            var session = new SessionDocument { FolderPath = folder };
            var executor = new RenameExecutor(store);
            await executor.ExecuteAsync(folder, plan, session, cancellationToken: CancellationToken.None);

            File.WriteAllText(Path.Combine(folder, "A.jpg"), "new file");

            var undo = await executor.UndoAsync(folder, session, cancellationToken: CancellationToken.None);

            Assert.Equal(1, undo.FailureCount);
            Assert.True(File.Exists(Path.Combine(folder, "A.jpg")));
            Assert.True(File.Exists(Path.Combine(folder, "A - Paris.jpg")));
            Assert.Single(session.Entries);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    private static RenameCandidate Candidate(string folder, string fileName, string baseName, string city) =>
        new(
            Path.Combine(folder, fileName),
            Path.Combine(folder, fileName),
            fileName,
            Path.GetExtension(fileName),
            baseName,
            city,
            string.Empty);

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
