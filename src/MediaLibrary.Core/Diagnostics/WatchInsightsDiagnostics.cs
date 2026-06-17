using System.Diagnostics;
using System.Threading.Channels;

namespace MediaLibrary.Core.Diagnostics;

public static class WatchInsightsDiagnostics
{
    private static readonly string LogPath = DiagnosticLogPathResolver.Resolve("watch-insights-perf.log");
    private static readonly bool IsVerboseEnabled =
        DiagnosticMessageFilter.IsEnabledByEnvironment("XFVERSE_WATCH_INSIGHTS_DIAGNOSTICS");
    private static readonly object SyncRoot = new();
    private static readonly Channel<string> Lines = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    static WatchInsightsDiagnostics()
    {
        if (IsVerboseEnabled)
        {
            _ = Task.Run(WriteLoopAsync);
        }
    }

    public static void Write(string message)
    {
        var safeMessage = string.IsNullOrWhiteSpace(message) ? "event=empty" : message.Trim();
        if (!IsVerboseEnabled && !DiagnosticMessageFilter.ShouldWriteReleaseMessage(safeMessage))
        {
            return;
        }

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [WATCH-INSIGHTS-PERF] {safeMessage}";
        Debug.WriteLine(line);
        if (IsVerboseEnabled)
        {
            Lines.Writer.TryWrite(line);
            return;
        }

        WriteReleaseLine(line);
    }

    private static async Task WriteLoopAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            await using var stream = new FileStream(
                LogPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 16 * 1024,
                useAsync: true);
            await using var writer = new StreamWriter(stream) { AutoFlush = true };

            await foreach (var line in Lines.Reader.ReadAllAsync())
            {
                await writer.WriteLineAsync(line);
            }
        }
        catch
        {
            // Diagnostics must never affect Watch Insights behavior.
        }
    }

    private static void WriteReleaseLine(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never affect Watch Insights behavior.
        }
    }
}
