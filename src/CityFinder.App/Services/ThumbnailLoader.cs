using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CityFinder.App.Services;

public sealed class ThumbnailLoader
{
    public BitmapSource? Load(string path, int? orientation, int decodeWidth = 480)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = decodeWidth;
            image.StreamSource = stream;
            image.EndInit();

            BitmapSource source = image;
            var transform = OrientationTransform(orientation);
            if (transform is not null)
            {
                source = new TransformedBitmap(source, transform);
            }

            if (source.CanFreeze)
            {
                source.Freeze();
            }

            return source;
        }
        catch
        {
            return null;
        }
    }

    private static Transform? OrientationTransform(int? orientation) => orientation switch
    {
        2 => new ScaleTransform(-1, 1),
        3 => new RotateTransform(180),
        4 => new ScaleTransform(1, -1),
        5 => Combine(new ScaleTransform(-1, 1), new RotateTransform(90)),
        6 => new RotateTransform(90),
        7 => Combine(new ScaleTransform(-1, 1), new RotateTransform(270)),
        8 => new RotateTransform(270),
        _ => null,
    };

    private static Transform Combine(Transform first, Transform second)
    {
        var group = new TransformGroup();
        group.Children.Add(first);
        group.Children.Add(second);
        return group;
    }
}
