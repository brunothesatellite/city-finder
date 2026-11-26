namespace CityFinder.Core.Models;

/// <summary>
/// A photo discovered in the selected folder, enriched with metadata and the
/// user's naming choices. This is the domain object used by the rename planner
/// and the session. It deliberately contains no UI types.
/// </summary>
public sealed class Photo
{
    /// <summary>Stable identifier for the lifetime of the scan (initial full path).</summary>
    public required string Id { get; init; }

    public required string FullPath { get; set; }
    public required string FileName { get; set; }
    public required string Extension { get; init; }
    public required string FolderPath { get; init; }

    public long Length { get; init; }
    public DateTime LastWriteUtc { get; init; }

    public DateTime? TakenAt { get; init; }

    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public string? Country { get; set; }
    public string? State { get; set; }
    public string? County { get; set; }

    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Orientation { get; init; }

    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }

    /// <summary>Set when the file could not be read; the photo is still listed.</summary>
    public string? ReadError { get; init; }

    public bool HasGps => Latitude.HasValue && Longitude.HasValue;

    /// <summary>Commune proposed by Photon (may be null when offline or not found).</summary>
    public string? PhotonCity { get; set; }

    /// <summary>Commune retained for the file name.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Optional free text appended after the city.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Original stem, stripped of an already present, recognised city suffix.</summary>
    public string BaseName { get; set; } = string.Empty;

    /// <summary>City found in the existing file name, if any.</summary>
    public string? ExistingCity { get; set; }

    public string? ExistingText { get; set; }

    /// <summary>Photon proposal when it differs from the retained city.</summary>
    public string? SuggestionCity { get; set; }

    public string InitialCity { get; set; } = string.Empty;
    public string InitialText { get; set; } = string.Empty;

    public bool IsSelected { get; set; }

    /// <summary>The file is already in its target form or was renamed by CityFinder.</summary>
    public bool IsAlreadyRenamed { get; set; }

    /// <summary>The file is known from the .cityfinder.json session.</summary>
    public bool IsFromSession { get; set; }

    public string? SessionOriginalName { get; set; }

    public string CoordinateLabel => HasGps
        ? FormattableString.Invariant($"{Latitude:0.00000}, {Longitude:0.00000}")
        : "Sans GPS";
}
