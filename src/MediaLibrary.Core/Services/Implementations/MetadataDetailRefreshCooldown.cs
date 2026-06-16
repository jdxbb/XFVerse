namespace MediaLibrary.Core.Services.Implementations;

internal static class MetadataDetailRefreshCooldown
{
    private const string Provider = "TMDB";
    private const string CacheType = "DetailRefreshCooldown";
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(4);

    public static Task<bool> IsMovieCoolingDownAsync(int movieId, CancellationToken cancellationToken)
    {
        return IsCoolingDownAsync(BuildMovieKey(movieId), cancellationToken);
    }

    public static Task MarkMovieSucceededAsync(int movieId, CancellationToken cancellationToken)
    {
        return MarkSucceededAsync(BuildMovieKey(movieId), cancellationToken);
    }

    public static async Task<bool> IsTvSeriesSummaryCoolingDownAsync(
        int tmdbSeriesId,
        CancellationToken cancellationToken)
    {
        if (await IsCoolingDownAsync(BuildTvSeriesFullKey(tmdbSeriesId), cancellationToken))
        {
            return true;
        }

        return await IsCoolingDownAsync(BuildTvSeriesSummaryKey(tmdbSeriesId), cancellationToken);
    }

    public static Task MarkTvSeriesSummarySucceededAsync(
        int tmdbSeriesId,
        CancellationToken cancellationToken)
    {
        return MarkSucceededAsync(BuildTvSeriesSummaryKey(tmdbSeriesId), cancellationToken);
    }

    public static Task<bool> IsTvSeriesFullCoolingDownAsync(
        int tmdbSeriesId,
        CancellationToken cancellationToken)
    {
        return IsCoolingDownAsync(BuildTvSeriesFullKey(tmdbSeriesId), cancellationToken);
    }

    public static Task MarkTvSeriesFullSucceededAsync(
        int tmdbSeriesId,
        CancellationToken cancellationToken)
    {
        return MarkSucceededAsync(BuildTvSeriesFullKey(tmdbSeriesId), cancellationToken);
    }

    private static async Task<bool> IsCoolingDownAsync(string cacheKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        var marker = await ExternalMetadataPersistentCache.TryGetAsync<RefreshCooldownMarker>(
            Provider,
            CacheType,
            cacheKey,
            cancellationToken);
        return marker.IsHit;
    }

    private static Task MarkSucceededAsync(string cacheKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return Task.CompletedTask;
        }

        return ExternalMetadataPersistentCache.SetAsync(
            Provider,
            CacheType,
            cacheKey,
            new RefreshCooldownMarker(DateTime.UtcNow),
            Cooldown,
            cancellationToken);
    }

    private static string BuildMovieKey(int movieId)
    {
        return movieId > 0 ? $"movie|id={movieId}" : string.Empty;
    }

    private static string BuildTvSeriesSummaryKey(int tmdbSeriesId)
    {
        return tmdbSeriesId > 0 ? $"tv-summary|tmdbSeries={tmdbSeriesId}" : string.Empty;
    }

    private static string BuildTvSeriesFullKey(int tmdbSeriesId)
    {
        return tmdbSeriesId > 0 ? $"tv-full|tmdbSeries={tmdbSeriesId}" : string.Empty;
    }

    private sealed record RefreshCooldownMarker(DateTime RefreshedAtUtc);
}
