using CityFinder.Core.Models;
using CityFinder.Infrastructure.Metadata;

namespace CityFinder.Infrastructure.Scanning;

public sealed record ScanProgress(int Processed, int Total, string FileName);

public sealed record PhotoScanResult(IReadOnlyList<Photo> Photos, IReadOnlyList<string> Warnings);

public sealed class PhotoScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".mpo", ".tif", ".tiff",
        ".png", ".webp", ".heic", ".heif",
    };

    private readonly PhotoMetadataReader _metadataReader;

    public PhotoScanner(PhotoMetadataReader? metadataReader = null)
    {
        _metadataReader = metadataReader ?? new PhotoMetadataReader();
    }

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    public Task<PhotoScanResult> ScanAsync(
        string folderPath,
        bool includeSubfolders,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(folderPath, includeSubfolders, progress, cancellationToken), cancellationToken);
    }

    private PhotoScanResult Scan(
        string folderPath,
        bool includeSubfolders,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("Le dossier sélectionné n'existe plus.");
        }

        var warnings = new List<string>();
        var files = new List<FileInfo>();
        var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var path in Directory.EnumerateFiles(folderPath, "*", option))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                if (info.Exists)
                {
                    files.Add(info);
                }
            }
            catch (Exception exception)
            {
                warnings.Add($"{Path.GetFileName(path)} : {exception.Message}");
            }
        }

        files.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        var photos = new List<Photo>(files.Count);
        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[index];
            progress?.Report(new ScanProgress(index, files.Count, file.Name));

            PhotoMetadata metadata;
            try
            {
                metadata = _metadataReader.Read(file.FullName);
            }
            catch (Exception exception)
            {
                metadata = new PhotoMetadata(null, null, null, null, null, null, null, null,
                    exception.Message);
            }

            var takenAt = metadata.TakenAt ?? SafeLastWriteTime(file);
            var photo = new Photo
            {
                Id = file.FullName,
                FullPath = file.FullName,
                FileName = file.Name,
                Extension = file.Extension,
                FolderPath = folderPath,
                Length = SafeLength(file),
                LastWriteUtc = SafeLastWriteUtc(file),
                TakenAt = takenAt,
                Latitude = metadata.Latitude,
                Longitude = metadata.Longitude,
                Width = metadata.Width,
                Height = metadata.Height,
                Orientation = metadata.Orientation,
                CameraMake = metadata.CameraMake,
                CameraModel = metadata.CameraModel,
                ReadError = metadata.Warning,
            };

            if (metadata.Warning is not null)
            {
                warnings.Add($"{file.Name} : {metadata.Warning}");
            }

            photos.Add(photo);
            progress?.Report(new ScanProgress(index + 1, files.Count, file.Name));
        }

        return new PhotoScanResult(photos, warnings);
    }

    private static long SafeLength(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime SafeLastWriteUtc(FileInfo file)
    {
        try
        {
            return file.LastWriteTimeUtc;
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }

    private static DateTime SafeLastWriteTime(FileInfo file)
    {
        try
        {
            return file.LastWriteTime;
        }
        catch
        {
            return DateTime.Now;
        }
    }
}
