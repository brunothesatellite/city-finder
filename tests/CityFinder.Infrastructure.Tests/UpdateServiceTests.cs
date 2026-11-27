using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CityFinder.Infrastructure.Updates;

namespace CityFinder.Infrastructure.Tests;

public sealed class UpdateServiceTests
{
    private const string AssetUrl =
        "https://github.com/brunothesatellite/city-finder/releases/download/v0.2.0/CityFinder-0.2.0-win-x64.zip";

    [Fact]
    public async Task CheckAsync_DetectsNewerReleaseAndWindowsAsset()
    {
        var payload = Encoding.UTF8.GetBytes("portable archive");
        var sha = Convert.ToHexString(SHA256.HashData(payload));
        var apiUri = new Uri("https://api.test/releases/latest");
        var json = BuildReleaseJson(sha, payload.Length);
        using var client = new HttpClient(new StubHandler(request =>
            request.RequestUri == apiUri
                ? JsonResponse(json)
                : new HttpResponseMessage(HttpStatusCode.NotFound)));

        var service = new UpdateService(client, new Version(0, 1, 1), apiUri);
        var result = await service.CheckAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(0, 2, 0), result.Release.Version);
        Assert.Equal("v0.2.0", result.Release.Tag);
        Assert.NotNull(result.Release.Asset);
        Assert.Equal("CityFinder-0.2.0-win-x64.zip", result.Release.Asset!.Name);
        Assert.Equal(sha, result.Release.Asset.Sha256);
    }

    [Fact]
    public async Task CheckAsync_ReportsUpToDateWhenVersionsMatch()
    {
        var apiUri = new Uri("https://api.test/releases/latest");
        var json = BuildReleaseJson(new string('A', 64), 1024);
        using var client = new HttpClient(new StubHandler(_ => JsonResponse(json)));

        var service = new UpdateService(client, new Version(0, 2, 0), apiUri);
        var result = await service.CheckAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task DownloadAsync_WritesAndVerifiesArchive()
    {
        var payload = Encoding.UTF8.GetBytes("downloaded archive content");
        var sha = Convert.ToHexString(SHA256.HashData(payload));
        using var client = new HttpClient(new StubHandler(request =>
            request.RequestUri!.AbsoluteUri == AssetUrl
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(payload),
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound)));

        var service = new UpdateService(client, new Version(0, 1, 1));
        var directory = Path.Combine(Path.GetTempPath(), "cityfinder-update-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var progress = new ImmediateProgress<UpdateDownloadProgress>();
            var path = await service.DownloadAsync(
                new ReleaseAsset("CityFinder-0.2.0-win-x64.zip", payload.Length, AssetUrl, sha),
                directory,
                progress);

            Assert.True(File.Exists(path));
            Assert.Equal(payload, await File.ReadAllBytesAsync(path));
            Assert.NotEmpty(progress.Values);
            Assert.Equal(payload.Length, progress.Values[^1].BytesReceived);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadAsync_RejectsWrongHashAndDeletesFile()
    {
        var payload = Encoding.UTF8.GetBytes("corrupted");
        using var client = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            }));

        var service = new UpdateService(client, new Version(0, 1, 1));
        var directory = Path.Combine(Path.GetTempPath(), "cityfinder-update-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var asset = new ReleaseAsset(
                "CityFinder-0.2.0-win-x64.zip",
                payload.Length,
                AssetUrl,
                new string('0', 64));

            await Assert.ThrowsAsync<InvalidDataException>(
                () => service.DownloadAsync(asset, directory));
            Assert.False(File.Exists(Path.Combine(directory, asset.Name)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractAppDirectory_FindsExecutableInNestedFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "cityfinder-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "update.zip");
        try
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("CityFinder-0.2.0-win-x64/CityFinder.exe");
                using (var stream = entry.Open())
                {
                    stream.WriteByte(1);
                }

                archive.CreateEntry("CityFinder-0.2.0-win-x64/Map/map.js");
            }

            var service = new UpdateService(new HttpClient(), new Version(0, 1, 1));
            var appDirectory = service.ExtractAppDirectory(zipPath, Path.Combine(root, "staging"));

            Assert.Equal(
                Path.Combine(root, "staging", "CityFinder-0.2.0-win-x64"),
                appDirectory);
            Assert.True(File.Exists(Path.Combine(appDirectory, "CityFinder.exe")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string BuildReleaseJson(string sha, long size)
    {
        var body = $"# CityFinder 0.2.0\n\nNouveautés.\n\nSHA-256 : `{sha}`";
        return JsonSerializer.Serialize(new
        {
            tag_name = "v0.2.0",
            name = "CityFinder 0.2.0",
            body,
            published_at = "2026-11-27T10:00:00Z",
            assets = new[]
            {
                new
                {
                    name = "CityFinder-0.2.0-win-x64.zip",
                    size,
                    browser_download_url = AssetUrl,
                },
            },
        });
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_factory(request));
    }

    private sealed class ImmediateProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = new();

        public void Report(T value) => Values.Add(value);
    }
}
