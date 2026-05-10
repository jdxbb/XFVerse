using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MediaLibrary.Core.Diagnostics;

public static class AiPerfDiagnostics
{
    private const string LogPath = "C:\\Users\\32184\\Desktop\\\u5f71\u97f3\u7ba1\u7406\u7cfb\u7edf1.0\\logs\\ai-perf-debug.log";
    private static readonly object FileLock = new();
    private static readonly AsyncLocal<AiPerfScope?> CurrentScope = new();

    public static AiPerfScope? Current => CurrentScope.Value;

    public static AiPerfScope BeginScope(
        string operation,
        string combinationKey,
        bool forceRefresh,
        string? fingerprint = null)
    {
        var parent = CurrentScope.Value;
        var scope = new AiPerfScope(operation, combinationKey, forceRefresh, fingerprint, parent);
        CurrentScope.Value = scope;
        WriteEvent(
            $"event=start operation={FormatValue(operation)} combination={FormatValue(combinationKey)} forceRefresh={forceRefresh} fp={ShortFingerprint(fingerprint)}");
        return scope;
    }

    public static void RecordPhase(string name, TimeSpan elapsed)
    {
        CurrentScope.Value?.RecordPhase(name, elapsed);
    }

    public static void RecordExternalCall(string name, TimeSpan elapsed, bool isError)
    {
        CurrentScope.Value?.RecordExternalCall(name, elapsed, isError);
    }

    public static void RecordConcurrencySample(string name, int current)
    {
        CurrentScope.Value?.RecordConcurrencySample(name, current);
    }

    public static void RecordCandidateProcessed()
    {
        CurrentScope.Value?.RecordCandidateProcessed();
    }

    public static void RecordRecommendationBuilt()
    {
        CurrentScope.Value?.RecordRecommendationBuilt();
    }

    public static void RecordFilterDrop(string reason)
    {
        CurrentScope.Value?.RecordFilterDrop(reason);
    }

    public static void RecordDetails(int written, int reused)
    {
        CurrentScope.Value?.RecordDetails(written, reused);
    }

    public static void RecordCacheState(int currentItemKeys, int candidatePoolKeys, int detailsByKey)
    {
        CurrentScope.Value?.RecordCacheState(currentItemKeys, candidatePoolKeys, detailsByKey);
    }

    public static void WriteEvent(string message)
    {
        var line = $"[AI-PERF] {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
        try
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
        }
        catch
        {
            // Temporary diagnostics must never affect recommendation behavior.
        }
    }

    public static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(none)"
            : value.Trim().Replace(' ', '-');
    }

    public static string ShortFingerprint(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return "(none)";
        }

        var value = fingerprint.Trim();
        var lastSeparator = value.LastIndexOf(':');
        if (lastSeparator >= 0 && lastSeparator < value.Length - 1)
        {
            value = value[(lastSeparator + 1)..];
        }

        return value.Length <= 8 ? value : value[..8];
    }

    public static string SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "(none)";
        }

        var sanitized = message
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
        sanitized = Regex.Replace(
            sanitized,
            "(api[_-]?key|access[_-]?token|authorization|bearer)\\s*[:=]\\s*[^\\s&]+",
            "$1=<redacted>",
            RegexOptions.IgnoreCase);
        var queryIndex = sanitized.IndexOf('?');
        var httpIndex = sanitized.IndexOf("http", StringComparison.OrdinalIgnoreCase);
        if (httpIndex >= 0 && queryIndex > httpIndex)
        {
            sanitized = sanitized[..queryIndex] + "?<redacted>";
        }

        if (sanitized.Length > 120)
        {
            sanitized = sanitized[..120];
        }

        return sanitized.Replace('"', '\'');
    }

    internal static void RestoreScope(AiPerfScope? scope)
    {
        CurrentScope.Value = scope;
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return ((long)Math.Round(elapsed.TotalMilliseconds)).ToString();
    }

    public sealed class AiPerfScope : IDisposable
    {
        private readonly object _sync = new();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly AiPerfScope? _parent;
        private readonly Dictionary<string, long> _phaseTotals = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ExternalCounter> _externalCounters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _filterDrops = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _concurrencyMax = new(StringComparer.Ordinal);
        private bool _completed;
        private string _fingerprint;
        private int _processedCandidates;
        private int _builtRecommendations;
        private int _detailsWritten;
        private int _detailsReused;
        private int _currentItemKeys;
        private int _candidatePoolKeys;
        private int _detailsByKey;

        internal AiPerfScope(
            string operation,
            string combinationKey,
            bool forceRefresh,
            string? fingerprint,
            AiPerfScope? parent)
        {
            Operation = operation;
            CombinationKey = combinationKey;
            ForceRefresh = forceRefresh;
            _fingerprint = fingerprint ?? string.Empty;
            _parent = parent;
        }

        public string Operation { get; }

        public string CombinationKey { get; }

        public bool ForceRefresh { get; }

        public void SetFingerprint(string? fingerprint)
        {
            lock (_sync)
            {
                _fingerprint = fingerprint ?? string.Empty;
            }
        }

        public void RecordPhase(string name, TimeSpan elapsed)
        {
            lock (_sync)
            {
                var elapsedMs = (long)Math.Round(elapsed.TotalMilliseconds);
                _phaseTotals[name] = _phaseTotals.TryGetValue(name, out var existing)
                    ? existing + elapsedMs
                    : elapsedMs;
            }
        }

        public void RecordExternalCall(string name, TimeSpan elapsed, bool isError)
        {
            lock (_sync)
            {
                if (!_externalCounters.TryGetValue(name, out var counter))
                {
                    counter = new ExternalCounter();
                    _externalCounters[name] = counter;
                }

                counter.Count++;
                var elapsedMs = (long)Math.Round(elapsed.TotalMilliseconds);
                counter.TotalMs += elapsedMs;
                counter.MaxMs = Math.Max(counter.MaxMs, elapsedMs);
                if (isError)
                {
                    counter.ErrorCount++;
                }
            }
        }

        public void RecordConcurrencySample(string name, int current)
        {
            if (string.IsNullOrWhiteSpace(name) || current <= 0)
            {
                return;
            }

            lock (_sync)
            {
                _concurrencyMax[name] = _concurrencyMax.TryGetValue(name, out var existing)
                    ? Math.Max(existing, current)
                    : current;
            }
        }

        public void RecordCandidateProcessed()
        {
            lock (_sync)
            {
                _processedCandidates++;
            }
        }

        public void RecordRecommendationBuilt()
        {
            lock (_sync)
            {
                _builtRecommendations++;
            }
        }

        public void RecordFilterDrop(string reason)
        {
            lock (_sync)
            {
                _filterDrops[reason] = _filterDrops.TryGetValue(reason, out var existing)
                    ? existing + 1
                    : 1;
            }
        }

        public void RecordDetails(int written, int reused)
        {
            lock (_sync)
            {
                _detailsWritten += Math.Max(0, written);
                _detailsReused += Math.Max(0, reused);
            }
        }

        public void RecordCacheState(int currentItemKeys, int candidatePoolKeys, int detailsByKey)
        {
            lock (_sync)
            {
                _currentItemKeys = currentItemKeys;
                _candidatePoolKeys = candidatePoolKeys;
                _detailsByKey = detailsByKey;
            }
        }

        public void Complete(
            string outcome,
            string path,
            int itemCount = 0,
            string status = "",
            string error = "")
        {
            if (_completed)
            {
                return;
            }

            _stopwatch.Stop();
            string fingerprint;
            int processedCandidates;
            int builtRecommendations;
            int detailsWritten;
            int detailsReused;
            int currentItemKeys;
            int candidatePoolKeys;
            int detailsByKey;
            string phases;
            string externalCalls;
            string filterDrops;
            string concurrency;
            lock (_sync)
            {
                _completed = true;
                fingerprint = _fingerprint;
                processedCandidates = _processedCandidates;
                builtRecommendations = _builtRecommendations;
                detailsWritten = _detailsWritten;
                detailsReused = _detailsReused;
                currentItemKeys = _currentItemKeys;
                candidatePoolKeys = _candidatePoolKeys;
                detailsByKey = _detailsByKey;
                phases = FormatPhases();
                externalCalls = FormatExternalCalls();
                filterDrops = FormatFilterDrops();
                concurrency = FormatConcurrency();
            }

            var errorSegment = string.IsNullOrWhiteSpace(error)
                ? string.Empty
                : $" error=\"{SanitizeMessage(error)}\"";
            WriteEvent(
                $"event=complete operation={FormatValue(Operation)} path={FormatValue(path)} outcome={FormatValue(outcome)} combination={FormatValue(CombinationKey)} forceRefresh={ForceRefresh} fp={ShortFingerprint(fingerprint)} elapsedMs={FormatElapsed(_stopwatch.Elapsed)} items={itemCount} status={FormatValue(status)} aiCandidatesProcessed={processedCandidates} recommendationsBuilt={builtRecommendations} currentItemKeys={currentItemKeys} candidatePoolKeys={candidatePoolKeys} detailsByKey={detailsByKey} detailsWritten={detailsWritten} detailsReused={detailsReused} phases={FormatValue(phases)} external={FormatValue(externalCalls)} filters={FormatValue(filterDrops)} concurrency={FormatValue(concurrency)}{errorSegment}");
        }

        public void Dispose()
        {
            if (!_completed)
            {
                Complete("disposed", "unknown");
            }

            RestoreScope(_parent);
        }

        private string FormatPhases()
        {
            return _phaseTotals.Count == 0
                ? "none"
                : string.Join(",", _phaseTotals.Select(pair => $"{pair.Key}:{pair.Value}ms"));
        }

        private string FormatExternalCalls()
        {
            return _externalCounters.Count == 0
                ? "none"
                : string.Join(
                    ",",
                    _externalCounters.Select(
                        pair => $"{pair.Key}:count={pair.Value.Count};totalMs={pair.Value.TotalMs};maxMs={pair.Value.MaxMs};errors={pair.Value.ErrorCount}"));
        }

        private string FormatFilterDrops()
        {
            return _filterDrops.Count == 0
                ? "none"
                : string.Join(",", _filterDrops.Select(pair => $"{pair.Key}:{pair.Value}"));
        }

        private string FormatConcurrency()
        {
            return _concurrencyMax.Count == 0
                ? "none"
                : string.Join(",", _concurrencyMax.Select(pair => $"{pair.Key}:max={pair.Value}"));
        }
    }

    private sealed class ExternalCounter
    {
        public int Count { get; set; }

        public long TotalMs { get; set; }

        public long MaxMs { get; set; }

        public int ErrorCount { get; set; }
    }
}
