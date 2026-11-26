using System.Diagnostics;
using System.Net;
using System.Text.Json;
using CityFinder.Core;
using CityFinder.Core.Models;

namespace CityFinder.Infrastructure.Geocoding;

/// <summary>
/// Reverse geocoding through Photon (komoot). Requests are spaced to respect
/// the public service, results are cached locally, and only coordinates are
/// sent (no file name, path or image).
/// </summary>
public sealed class PhotonGeocoder : IGeocoder
{
    private const string Endpoint = "https://photon.komoot.io/reverse";
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(1050);

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly HttpClient _httpClient;
    private readonly string? _cachePath;
    private readonly SemaphoreSlim _rateGate = new(1, 1);
    private readonly SemaphoreSlim _cacheIoGate = new(1, 1);
    private readonly Dictionary<string, PhotonCacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly object _cacheLock = new();

    private bool _cacheLoaded;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public PhotonGeocoder(HttpClient httpClient, string? cacheDirectory = null)
    {
        _httpClient = httpClient;
        if (!string.IsNullOrWhiteSpace(cacheDirectory))
        {
            _cachePath = Path.Combine(cacheDirectory, "photon-v1.json");
        }
    }

    public async Task<GeocodeResult> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) ||
            Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
        {
            return GeocodeResult.Failed("Coordonnées invalides.");
        }

        var key = CacheKey(latitude, longitude);
        if (TryGetCached(key, out var cached))
        {
            return cached.Found
                ? GeocodeResult.Found(cached.Place!)
                : GeocodeResult.NotFound();
        }

        await _rateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wait = MinimumInterval - (DateTime.UtcNow - _lastRequestUtc);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }

            _lastRequestUtc = DateTime.UtcNow;
            var result = await RequestAsync(latitude, longitude, cancellationToken).ConfigureAwait(false);

            if (result.Status != GeocodeStatus.Failed)
            {
                await StoreAsync(key, result, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            _rateGate.Release();
        }
    }

    private async Task<GeocodeResult> RequestAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var uri = FormattableString.Invariant(
            $"{Endpoint}?lat={latitude:0.000000}&lon={longitude:0.000000}&lang=fr&limit=3");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd(
                    "CityFinder/0.1 (+https://github.com/brunothesatellite/city-finder)");
                request.Headers.Accept.ParseAdd("application/json");

                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return GeocodeResult.Failed($"Photon a répondu {(int)response.StatusCode}.");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return Parse(document, latitude, longitude);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return GeocodeResult.Failed("Délai dépassé en contactant Photon.");
            }
            catch (HttpRequestException exception)
            {
                if (attempt == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return GeocodeResult.Failed("Réseau indisponible : " + exception.Message);
            }
            catch (JsonException exception)
            {
                return GeocodeResult.Failed("Réponse Photon illisible : " + exception.Message);
            }
        }

        return GeocodeResult.Failed("Photon n'a pas répondu.");
    }

    private static GeocodeResult Parse(JsonDocument document, double latitude, double longitude)
    {
        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            return GeocodeResult.Failed("Réponse Photon inattendue.");
        }

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var properties) ||
                properties.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var city = FirstNonEmpty(
                ReadString(properties, "city"),
                ReadString(properties, "town"),
                ReadString(properties, "village"),
                ReadString(properties, "municipality"),
                ReadString(properties, "locality"));

            if (city is null &&
                string.Equals(ReadString(properties, "osm_key"), "place", StringComparison.OrdinalIgnoreCase))
            {
                var value = ReadString(properties, "osm_value");
                if (value is "city" or "town" or "village" or "municipality")
                {
                    city = ReadString(properties, "name");
                }
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                continue;
            }

            return GeocodeResult.Found(new GeoPlace(
                city.Trim(),
                ReadString(properties, "country"),
                ReadString(properties, "state"),
                ReadString(properties, "county"),
                latitude,
                longitude));
        }

        return GeocodeResult.NotFound();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string CacheKey(double latitude, double longitude) =>
        FormattableString.Invariant($"{Math.Round(latitude, 5):0.00000}|{Math.Round(longitude, 5):0.00000}");

    private bool TryGetCached(string key, out PhotonCacheEntry entry)
    {
        EnsureCacheLoaded();
        lock (_cacheLock)
        {
            return _cache.TryGetValue(key, out entry!);
        }
    }

    private async Task StoreAsync(string key, GeocodeResult result, CancellationToken cancellationToken)
    {
        EnsureCacheLoaded();
        lock (_cacheLock)
        {
            _cache[key] = new PhotonCacheEntry(result.Status == GeocodeStatus.Found, result.Place);
        }

        if (_cachePath is null)
        {
            return;
        }

        await _cacheIoGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PhotonCacheFile file;
            lock (_cacheLock)
            {
                file = new PhotonCacheFile
                {
                    Entries = new Dictionary<string, PhotonCacheEntry>(_cache, StringComparer.Ordinal),
                };
            }

            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(file, CacheJsonOptions);
            await File.WriteAllTextAsync(_cachePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.WriteLine("Photon cache write failed: " + exception.Message);
        }
        finally
        {
            _cacheIoGate.Release();
        }
    }

    private void EnsureCacheLoaded()
    {
        lock (_cacheLock)
        {
            if (_cacheLoaded)
            {
                return;
            }

            _cacheLoaded = true;
            if (_cachePath is null || !File.Exists(_cachePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_cachePath);
                var file = JsonSerializer.Deserialize<PhotonCacheFile>(json, CacheJsonOptions);
                if (file?.Entries is null)
                {
                    return;
                }

                foreach (var pair in file.Entries)
                {
                    _cache[pair.Key] = pair.Value;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Photon cache read failed: " + exception.Message);
            }
        }
    }

    private sealed class PhotonCacheFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, PhotonCacheEntry> Entries { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class PhotonCacheEntry
    {
        public PhotonCacheEntry()
        {
        }

        public PhotonCacheEntry(bool found, GeoPlace? place)
        {
            Found = found;
            Place = place;
        }

        public bool Found { get; set; }
        public GeoPlace? Place { get; set; }
    }
}
