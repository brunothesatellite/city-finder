namespace CityFinder.App.Services;

public interface IMapService
{
    event EventHandler<string>? PhotoFocused;
    event EventHandler<string>? PhotoDetailsRequested;
    event EventHandler<string>? ThumbnailRequested;
    event EventHandler<string>? BasemapChanged;

    bool IsReady { get; }

    Task SetPhotosAsync(
        IReadOnlyList<MapPointDto> points,
        string theme,
        bool fit,
        CancellationToken cancellationToken = default);

    Task FocusPhotoAsync(string id);

    Task FitAllAsync();

    Task SetThemeAsync(string theme);

    Task SetBasemapAsync(string mode);

    Task SetThumbnailAsync(string id, string dataUrl);
}

public sealed class NullMapService : IMapService
{
    public event EventHandler<string>? PhotoFocused
    {
        add { }
        remove { }
    }

    public event EventHandler<string>? PhotoDetailsRequested
    {
        add { }
        remove { }
    }

    public event EventHandler<string>? ThumbnailRequested
    {
        add { }
        remove { }
    }

    public event EventHandler<string>? BasemapChanged
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

    public Task SetBasemapAsync(string mode) => Task.CompletedTask;

    public Task SetThumbnailAsync(string id, string dataUrl) => Task.CompletedTask;
}
