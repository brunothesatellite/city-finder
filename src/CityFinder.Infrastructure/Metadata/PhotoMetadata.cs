namespace CityFinder.Infrastructure.Metadata;

public sealed record PhotoMetadata(
    double? Latitude,
    double? Longitude,
    DateTime? TakenAt,
    int? Width,
    int? Height,
    int? Orientation,
    string? CameraMake,
    string? CameraModel,
    string? Warning = null);
