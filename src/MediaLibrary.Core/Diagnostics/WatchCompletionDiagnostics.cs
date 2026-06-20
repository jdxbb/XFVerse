using System.Diagnostics;
using System.IO;
using System.Text;

namespace MediaLibrary.Core.Diagnostics;

internal static class WatchCompletionDiagnostics
{
    private static readonly object SyncRoot = new();
    private static string? _logPath;

    public static void Write(string message)
    {
        if (!DiagnosticMessageFilter.ShouldWrite(message, "XFVERSE_WATCH_COMPLETION_DIAGNOSTICS"))
        {
            return;
        }

        try
        {
            var safeMessage = DiagnosticLogSanitizer.Sanitize(message);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [WATCH-COMPLETION] {safeMessage}";
            Debug.WriteLine(line);

            var logPath = _logPath ??= ResolveLogPath();
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
            // Completion diagnostics must never affect playback progress persistence.
        }
    }

    private static string ResolveLogPath()
    {
        return DiagnosticLogPathResolver.Resolve("watch-completion.log");
    }
}
