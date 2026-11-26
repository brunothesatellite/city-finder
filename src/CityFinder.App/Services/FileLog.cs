using System.IO;

namespace CityFinder.App.Services;

public static class FileLog
{
    private static readonly object Gate = new();

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CityFinder",
                "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"cityfinder-{DateTime.Now:yyyy-MM-dd}.log");
            var line = $"{DateTime.Now:O} {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (Gate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never break the app.
        }
    }
}
