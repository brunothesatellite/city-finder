namespace CityFinder.App.Services;

public interface IMapService
{
    event EventHandler<string>? PhotoFocused;

    bool IsReady { get; }

    Task SetPhotosAsync(
        IReadOnlyList<MapPointDto> points,
        string theme,
        bool fit,
        CancellationToken cancellationToken = default);

    Task FocusPhotoAsync(string id);

    Task FitAllAsync();

    Task SetThemeAsync(string theme);
}

public sealed class NullMapService : IMapService
{
    public event EventHandler<string>? PhotoFocused
    {
        add { }
        remove { }
    }

    public bool IsReady => false;

    public Task SetPhotosAsync(
        IReadOnlyList<MapPointDto> points,
        string theme,
        bool fit,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task FocusPhotoAsync(string id) => Task.CompletedTask;

    public Task FitAllAsync() => Task.CompletedTask;

    public Task SetThemeAsync(string theme) => Task.CompletedTask;
}
