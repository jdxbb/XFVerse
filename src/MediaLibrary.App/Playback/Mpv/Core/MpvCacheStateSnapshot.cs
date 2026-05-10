using System.Globalization;

namespace MediaLibrary.App.Playback.Mpv.Core;

internal sealed record MpvSeekableRangeSnapshot(double Start, double End);

internal sealed record MpvCacheRangeEvaluation(
    int RangeCount,
    bool? CurrentTimeInSeekableRange,
    double? MinStart,
    double? MaxEnd,
    double? NearestRangeDistance,
    bool? CacheCoversCurrentTime);

internal sealed class MpvCacheStateSnapshot
{
    public static readonly MpvCacheStateSnapshot Empty = new(
        DateTime.MinValue,
        null,
        null,
        null,
        null,
        null,
        []);

    public MpvCacheStateSnapshot(
        DateTime observedAtUtc,
        double? cacheDuration,
        long? fileCacheBytes,
        long? fwBytes,
        double? readerPts,
        double? cacheEnd,
        IReadOnlyList<MpvSeekableRangeSnapshot> seekableRanges)
    {
        ObservedAtUtc = observedAtUtc;
        CacheDuration = cacheDuration;
        FileCacheBytes = fileCacheBytes;
        FwBytes = fwBytes;
        ReaderPts = readerPts;
        CacheEnd = cacheEnd;
        SeekableRanges = seekableRanges;
    }

    public DateTime ObservedAtUtc { get; }

    public double? CacheDuration { get; }

    public long? FileCacheBytes { get; }

    public long? FwBytes { get; }

    public double? ReaderPts { get; }

    public double? CacheEnd { get; }

    public IReadOnlyList<MpvSeekableRangeSnapshot> SeekableRanges { get; }

    public bool HasRanges => SeekableRanges.Count > 0;

    public MpvCacheRangeEvaluation Evaluate(double? currentTime)
    {
        if (!currentTime.HasValue)
        {
            return new MpvCacheRangeEvaluation(SeekableRanges.Count, null, null, null, null, null);
        }

        if (SeekableRanges.Count == 0)
        {
            return new MpvCacheRangeEvaluation(0, false, null, null, null, false);
        }

        var minStart = SeekableRanges.Min(range => range.Start);
        var maxEnd = SeekableRanges.Max(range => range.End);
        var inRange = SeekableRanges.Any(range => currentTime.Value >= range.Start && currentTime.Value <= range.End);
        var nearestDistance = SeekableRanges
            .Select(range =>
            {
                if (currentTime.Value >= range.Start && currentTime.Value <= range.End)
                {
                    return 0d;
                }

                return Math.Min(Math.Abs(currentTime.Value - range.Start), Math.Abs(currentTime.Value - range.End));
            })
            .Min();
        var cacheCoversCurrentTime = inRange
                                     && (CacheDuration is null || CacheDuration.Value > 0.1d)
                                     && (!CacheEnd.HasValue || CacheEnd.Value + 0.5d >= currentTime.Value);
        return new MpvCacheRangeEvaluation(
            SeekableRanges.Count,
            inRange,
            minStart,
            maxEnd,
            nearestDistance,
            cacheCoversCurrentTime);
    }

    public static string FormatDouble(double? value)
    {
        return value.HasValue && double.IsFinite(value.Value)
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : "unknown";
    }

    public static string FormatLong(long? value)
    {
        return value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : "unknown";
    }

    public static string FormatBool(bool? value)
    {
        return value.HasValue ? value.Value.ToString().ToLowerInvariant() : "unknown";
    }
}
