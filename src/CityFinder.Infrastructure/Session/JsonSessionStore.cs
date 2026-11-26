using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CityFinder.Core;
using CityFinder.Core.Models;

namespace CityFinder.Infrastructure.Session;

/// <summary>
/// Stores the resume/undo manifest in <c>.cityfinder.json</c> inside the photo
/// folder. If that folder is read-only, a deterministic fallback file is used
/// under %LOCALAPPDATA%\CityFinder\sessions.
/// </summary>
public sealed class JsonSessionStore : ISessionStore
{
    public const string FileName = ".cityfinder.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _fallbackDirectory;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonSessionStore(string? fallbackDirectory = null)
    {
        _fallbackDirectory = fallbackDirectory ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CityFinder",
                "sessions");
    }

    public async Task<SessionLoadResult> LoadAsync(string folderPath, CancellationToken cancellationToken)
    {
        var primary = GetPrimaryPath(folderPath);
        var fallback = GetFallbackPath(folderPath);
        var warning = default(string);

        foreach (var path in new[] { primary, fallback })
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
                    .ConfigureAwait(false);
                var document = JsonSerializer.Deserialize<SessionDocument>(json, JsonOptions);
                if (document is null)
                {
                    warning = "Le fichier de session est vide.";
                    continue;
                }

                document.Entries ??= new List<SessionEntry>();
                if (document.Version > SessionDocument.CurrentVersion)
                {
                    warning = "Session créée par une version plus récente de CityFinder.";
                }

                if (string.IsNullOrWhiteSpace(document.FolderPath))
                {
                    document.FolderPath = folderPath;
                }

                return new SessionLoadResult(document, path, warning);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                warning = $"Session illisible ({Path.GetFileName(path)}) : {exception.Message}";
            }
        }

        return new SessionLoadResult(null, null, warning);
    }

    public async Task<SessionSaveResult> SaveAsync(
        string folderPath,
        SessionDocument document,
        CancellationToken cancellationToken)
    {
        document.FolderPath = folderPath;
        document.UpdatedUtc = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(document, JsonOptions);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var primary = GetPrimaryPath(folderPath);
            var fallback = GetFallbackPath(folderPath);

            if (document.Entries.Count == 0)
            {
                // Nothing left to resume or undo: do not leave an empty hidden
                // file in the photo folder.
                TryDelete(primary);
                TryDelete(fallback);
                return new SessionSaveResult(true, null, false, null);
            }

            var primaryError = await TryWriteAsync(primary, bytes, hide: true, cancellationToken)
                .ConfigureAwait(false);

            if (primaryError is null)
            {
                return new SessionSaveResult(true, primary, false, null);
            }

            var fallbackError = await TryWriteAsync(fallback, bytes, hide: false, cancellationToken)
                .ConfigureAwait(false);

            if (fallbackError is null)
            {
                return new SessionSaveResult(
                    true,
                    fallback,
                    true,
                    $"Impossible d'écrire dans le dossier photo ({primaryError}). Session enregistrée dans %LOCALAPPDATA%.");
            }

            return new SessionSaveResult(
                false,
                null,
                false,
                $"Session non enregistrée : {primaryError} / {fallbackError}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public string GetPrimaryPath(string folderPath) => Path.Combine(folderPath, FileName);

    public string GetFallbackPath(string folderPath)
    {
        var normalized = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant()));
        var suffix = Convert.ToHexString(hash)[..16].ToLowerInvariant();
        return Path.Combine(_fallbackDirectory, suffix + ".json");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
            // A leftover hidden session is harmless.
        }
    }

    private static async Task<string?> TryWriteAsync(
        string path,
        byte[] bytes,
        bool hide,
        CancellationToken cancellationToken)
    {
        string? temporary = null;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return "chemin invalide";
            }

            Directory.CreateDirectory(directory);
            temporary = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);

            if (File.Exists(path))
            {
                File.Replace(temporary, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporary, path);
            }

            temporary = null;
            if (hide)
            {
                try
                {
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
                }
                catch
                {
                    // Hidden is cosmetic; the session still works.
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            if (temporary is not null)
            {
                try
                {
                    File.Delete(temporary);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }
    }
}
