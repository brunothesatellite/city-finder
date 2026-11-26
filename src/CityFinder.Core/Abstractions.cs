using CityFinder.Core.Models;

namespace CityFinder.Core;

public interface IGeocoder
{
    Task<GeocodeResult> ReverseAsync(double latitude, double longitude, CancellationToken cancellationToken);
}

public sealed record SessionLoadResult(SessionDocument? Document, string? Path, string? Warning);

public sealed record SessionSaveResult(bool Success, string? Path, bool UsedFallback, string? Warning);

public interface ISessionStore
{
    Task<SessionLoadResult> LoadAsync(string folderPath, CancellationToken cancellationToken);

    Task<SessionSaveResult> SaveAsync(
        string folderPath,
        SessionDocument document,
        CancellationToken cancellationToken);
}
