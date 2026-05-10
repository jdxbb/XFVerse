using System.Diagnostics;
using System.IO;
using System.Text;

namespace MediaLibrary.App.Helpers;

public static class MpvPlaybackDiagnostics
{
    private static readonly object SyncRoot = new();
    private static string? _logPath;

    public static string LogPath => _logPath ??= ResolveLogPath();

    public static void Write(string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [MPV] {message}";
            Debug.WriteLine(line);

            var logPath = LogPath;
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (SyncRoot)
            {
                File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never affect playback or window creation.
        }
    }

    private static string ResolveLogPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MediaLibrary.sln")))
            {
                return Path.Combine(directory.FullName, "logs", "mpv-playback.log");
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "logs", "mpv-playback.log");
    }
}
