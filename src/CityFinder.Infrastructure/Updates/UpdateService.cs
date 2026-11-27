using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CityFinder.Infrastructure.Updates;

public sealed record ReleaseAsset(
    string Name,
    long Size,
    string DownloadUrl,
    string? Sha256);

public sealed record ReleaseInfo(
    string Tag,
    Version Version,
    string Name,
    string Notes,
    DateTimeOffset? PublishedAt,
    ReleaseAsset? Asset);

public sealed record UpdateCheckResult(bool IsUpdateAvailable, ReleaseInfo Release);

public sealed record UpdateDownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double Percent => TotalBytes is > 0
        ? Math.Min(100, BytesReceived * 100.0 / TotalBytes.Value)
        : 0;
}

/// <summary>
/// Checks the official GitHub releases and downloads the portable Windows
/// archive. Network access is limited to the GitHub API and the release asset.
/// </summary>
public sealed class UpdateService
{
    public static readonly Uri DefaultApiUri =
        new("https://api.github.com/repos/brunothesatellite/city-finder/releases/latest");

    private static readonly Regex Sha256Pattern = new(
        @"SHA-?256\s*[:=]\s*`?([A-Fa-f0-9]{64})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly Uri _apiUri;

    public UpdateService(HttpClient httpClient, Version currentVersion, Uri? apiUri = null)
    {
        _httpClient = httpClient;
        CurrentVersion = Normalize(currentVersion);
        _apiUri = apiUri ?? DefaultApiUri;
    }

    public Version CurrentVersion { get; }

    public string CurrentVersionLabel => FormatVersion(CurrentVersion);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        // Keep the version check snappy even if GitHub is unreachable; the
        // download itself uses the longer HttpClient timeout.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;

        using var request = new HttpRequestMessage(HttpMethod.Get, _apiUri);
        request.Headers.UserAgent.ParseAdd("CityFinder-Updater/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GitHub a répondu {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(token).ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);

        var root = document.RootElement;
        var tag = GetString(root, "tag_name");
        if (string.IsNullOrWhiteSpace(tag) || ParseVersion(tag) is not { } version)
        {
            throw new InvalidDataException("La release GitHub ne contient pas de version valide.");
        }

        var name = GetString(root, "name") ?? tag;
        var notes = GetString(root, "body") ?? string.Empty;
        var publishedAt = TryGetDate(root, "published_at");
        var sha256 = ParseSha256(notes);
        var asset = SelectAsset(root, sha256);

        var release = new ReleaseInfo(tag, version, name, notes, publishedAt, asset);
        return new UpdateCheckResult(version > CurrentVersion, release);
    }

    public async Task<string> DownloadAsync(
        ReleaseAsset asset,
        string targetDirectory,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !IsGitHubHost(uri.Host))
        {
            throw new InvalidDataException("L'archive de mise à jour ne provient pas de GitHub.");
        }

        if (string.IsNullOrWhiteSpace(asset.Sha256))
        {
            throw new InvalidDataException(
                "La release ne fournit pas d'empreinte SHA-256 ; l'installation est refusée.");
        }

        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, Path.GetFileName(asset.Name));

        try
        {
            using var response = await _httpClient
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength
                             ?? (asset.Size > 0 ? asset.Size : null);

            await using var source = await response.Content
                .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                received += read;
                progress?.Report(new UpdateDownloadProgress(received, totalBytes));
            }
        }
        catch
        {
            TryDelete(targetPath);
            throw;
        }

        var actualHash = await ComputeSha256Async(targetPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualHash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(targetPath);
            throw new InvalidDataException(
                "L'empreinte SHA-256 de l'archive ne correspond pas à celle publiée sur GitHub.");
        }

        return targetPath;
    }

    public string ExtractAppDirectory(string zipPath, string destinationRoot)
    {
        if (Directory.Exists(destinationRoot))
        {
            Directory.Delete(destinationRoot, recursive: true);
        }

        Directory.CreateDirectory(destinationRoot);
        ZipFile.ExtractToDirectory(zipPath, destinationRoot, overwriteFiles: true);

        var executable = Directory
            .EnumerateFiles(destinationRoot, "CityFinder.exe", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault();

        if (executable is null)
        {
            throw new InvalidDataException("L'archive téléchargée ne contient pas CityFinder.exe.");
        }

        return Path.GetDirectoryName(executable)!;
    }

    public static string FormatVersion(Version version) =>
        version.Revision > 0 ? version.ToString() : version.ToString(3);

    private static Version Normalize(Version version) =>
        new(Math.Max(version.Major, 0), Math.Max(version.Minor, 0), Math.Max(version.Build, 0));

    private static Version? ParseVersion(string tag)
    {
        var value = tag.Trim().TrimStart('v', 'V');
        var separator = value.IndexOfAny(['-', '+']);
        if (separator >= 0)
        {
            value = value[..separator];
        }

        return Version.TryParse(value, out var version) ? Normalize(version) : null;
    }

    private static ReleaseAsset? SelectAsset(JsonElement root, string? sha256)
    {
        if (!root.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in assets.EnumerateArray())
        {
            var name = GetString(item, "name");
            var url = GetString(item, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(url) ||
                !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                !name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var size = item.TryGetProperty("size", out var sizeElement) &&
                       sizeElement.TryGetInt64(out var parsedSize)
                ? parsedSize
                : 0;

            return new ReleaseAsset(name, size, url, sha256);
        }

        return null;
    }

    private static string? ParseSha256(string notes)
    {
        var match = Sha256Pattern.Match(notes);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static bool IsGitHubHost(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? TryGetDate(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
