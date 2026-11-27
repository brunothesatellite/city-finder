using System.Diagnostics;
using System.IO;
using System.Text;

namespace CityFinder.App.Services;

/// <summary>
/// Replaces the portable application files after it has exited. A small
/// PowerShell script is used so the running executable is not overwritten.
/// </summary>
public static class UpdateInstaller
{
    public static string UpdatesRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CityFinder",
        "updates");

    public static string InstallDirectory =>
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static void CleanupStaging()
    {
        var stagingRoot = Path.Combine(UpdatesRoot, "staging");
        if (!Directory.Exists(stagingRoot))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(stagingRoot))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // A previous updater may still be closing; retry next launch.
            }
        }
    }

    public static bool CanWriteToInstallDirectory()
    {
        var probe = Path.Combine(InstallDirectory, $".cityfinder-write-{Guid.NewGuid():N}");
        try
        {
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(probe))
                {
                    File.Delete(probe);
                }
            }
            catch
            {
                // Best effort.
            }

            return false;
        }
    }

    public static void Launch(string stagingDirectory)
    {
        if (!Directory.Exists(stagingDirectory) ||
            !File.Exists(Path.Combine(stagingDirectory, "CityFinder.exe")))
        {
            throw new DirectoryNotFoundException(
                "Le dossier de mise à jour ne contient pas CityFinder.exe.");
        }

        if (!CanWriteToInstallDirectory())
        {
            throw new UnauthorizedAccessException(
                "Le dossier d'installation est en lecture seule. Téléchargez l'archive et remplacez le dossier manuellement.");
        }

        Directory.CreateDirectory(UpdatesRoot);
        var scriptPath = Path.Combine(
            UpdatesRoot,
            $"apply-update-{DateTime.UtcNow:yyyyMMddHHmmss}.ps1");
        var logPath = Path.Combine(UpdatesRoot, "update-session.log");

        File.WriteAllText(
            scriptPath,
            BuildScript(stagingDirectory, InstallDirectory, logPath, Environment.ProcessId),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments =
                $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = UpdatesRoot,
        };

        if (Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException("Le programme d'installation n'a pas pu démarrer.");
        }
    }

    private static string BuildScript(
        string stagingDirectory,
        string targetDirectory,
        string logPath,
        int processId)
    {
        var source = Escape(stagingDirectory);
        var target = Escape(targetDirectory);
        var log = Escape(logPath);

        return $$"""
            $ErrorActionPreference = 'Stop'
            try { Start-Transcript -Path '{{log}}' -Append | Out-Null } catch { }

            $appPid = {{processId}}
            $deadline = (Get-Date).AddSeconds(90)
            while ((Get-Process -Id $appPid -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $deadline)) {
                Start-Sleep -Milliseconds 400
            }

            # Let file handles and the WebView2 process release the application folder.
            Start-Sleep -Seconds 2

            $source = '{{source}}'
            $target = '{{target}}'

            & robocopy.exe $source $target /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
            if ($LASTEXITCODE -ge 8) {
                # Keep the user on a working version if the replacement failed.
                Start-Process -FilePath (Join-Path $source 'CityFinder.exe') -WorkingDirectory $source
                throw "robocopy a échoué avec le code $LASTEXITCODE."
            }

            Start-Process -FilePath (Join-Path $target 'CityFinder.exe') -WorkingDirectory $target
            Remove-Item -LiteralPath $source -Recurse -Force -ErrorAction SilentlyContinue

            try { Stop-Transcript | Out-Null } catch { }
            Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
            """;
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
