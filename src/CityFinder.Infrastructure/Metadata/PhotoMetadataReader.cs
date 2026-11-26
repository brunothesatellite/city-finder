using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Heif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;
using MetadataDirectory = MetadataExtractor.Directory;

namespace CityFinder.Infrastructure.Metadata;

/// <summary>Reads EXIF/XMP metadata with MetadataExtractor. Never throws to the caller.</summary>
public sealed class PhotoMetadataReader
{
    public PhotoMetadata Read(string path)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);

            double? latitude = null;
            double? longitude = null;
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            var location = gps?.GetGeoLocation();
            if (location is { } geo &&
                !geo.IsZero &&
                double.IsFinite(geo.Latitude) &&
                double.IsFinite(geo.Longitude) &&
                Math.Abs(geo.Latitude) <= 90 &&
                Math.Abs(geo.Longitude) <= 180)
            {
                latitude = geo.Latitude;
                longitude = geo.Longitude;
            }

            DateTime? takenAt = null;
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd is not null &&
                subIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original) &&
                original.Year >= 1900)
            {
                takenAt = original;
            }
            else
            {
                var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0 is not null &&
                    ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var modified) &&
                    modified.Year >= 1900)
                {
                    takenAt = modified;
                }
            }

            var dimensions = ReadDimensions(directories);
            var orientation = ReadOrientation(directories);
            var make = Clean(ReadString(directories.OfType<ExifIfd0Directory>().FirstOrDefault(),
                ExifDirectoryBase.TagMake));
            var model = Clean(ReadString(directories.OfType<ExifIfd0Directory>().FirstOrDefault(),
                ExifDirectoryBase.TagModel));

            return new PhotoMetadata(latitude, longitude, takenAt, dimensions.Width, dimensions.Height,
                orientation, make, model);
        }
        catch (Exception exception)
        {
            return new PhotoMetadata(null, null, null, null, null, null, null, null,
                "Métadonnées illisibles : " + exception.Message);
        }
    }

    private static (int? Width, int? Height) ReadDimensions(IReadOnlyList<MetadataDirectory> directories)
    {
        var jpeg = directories.OfType<JpegDirectory>().FirstOrDefault();
        if (TryReadDimension(jpeg, JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight, out var width, out var height))
        {
            return (width, height);
        }

        var png = directories.OfType<PngDirectory>().FirstOrDefault();
        if (TryReadDimension(png, PngDirectory.TagImageWidth, PngDirectory.TagImageHeight, out width, out height))
        {
            return (width, height);
        }

        var webp = directories.OfType<WebPDirectory>().FirstOrDefault();
        if (TryReadDimension(webp, WebPDirectory.TagImageWidth, WebPDirectory.TagImageHeight, out width, out height))
        {
            return (width, height);
        }

        var heic = directories.OfType<HeicImagePropertiesDirectory>().FirstOrDefault();
        if (TryReadDimension(heic, HeicImagePropertiesDirectory.TagImageWidth,
                HeicImagePropertiesDirectory.TagImageHeight, out width, out height))
        {
            return (width, height);
        }

        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        return TryReadDimension(ifd0, ExifDirectoryBase.TagImageWidth, ExifDirectoryBase.TagImageHeight,
            out width, out height)
            ? (width, height)
            : (null, null);
    }

    private static bool TryReadDimension(
        MetadataDirectory? directory,
        int widthTag,
        int heightTag,
        out int? width,
        out int? height)
    {
        width = null;
        height = null;
        if (directory is null)
        {
            return false;
        }

        if (directory.TryGetInt32(widthTag, out var parsedWidth) && parsedWidth > 0)
        {
            width = parsedWidth;
        }

        if (directory.TryGetInt32(heightTag, out var parsedHeight) && parsedHeight > 0)
        {
            height = parsedHeight;
        }

        return width is not null || height is not null;
    }

    private static int? ReadOrientation(IReadOnlyList<MetadataDirectory> directories)
    {
        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (ifd0 is not null &&
            ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientation) &&
            orientation is >= 1 and <= 8)
        {
            return orientation;
        }

        return null;
    }

    private static string? ReadString(MetadataDirectory? directory, int tag)
    {
        if (directory is null)
        {
            return null;
        }

        try
        {
            return directory.GetString(tag);
        }
        catch
        {
            return null;
        }
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }
}
