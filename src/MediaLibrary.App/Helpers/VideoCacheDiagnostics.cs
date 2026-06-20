using System.Diagnostics;
using System.IO;
using System.Text;
using MediaLibrary.Core.Diagnostics;

namespace MediaLibrary.App.Helpers;

public static class VideoCacheDiagnostics
{
    private static readonly object SyncRoot = new();
    private static string? _logPath;

    public static string LogPath => _logPath ??= ResolveLogPath();

    public static void Write(string category, string message)
    {
        if (!DiagnosticMessageFilter.ShouldWrite($"{category} {message}", "XFVERSE_VIDEO_CACHE_DIAGNOSTICS"))
        {
            return;
        }

        try
        {
            var safeMessage = DiagnosticLogSanitizer.Sanitize(message);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {safeMessage}";
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
            // Diagnostics must never affect playback.
        }
    }

    private static string ResolveLogPath()
    {
        return DiagnosticLogPathResolver.Resolve("video-cache-debug.log");
    }
}
