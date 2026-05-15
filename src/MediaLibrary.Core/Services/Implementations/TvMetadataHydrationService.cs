using System.Collections.Concurrent;
using System.Globalization;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class TvMetadataHydrationService : ITvMetadataHydrationService
{
    private static readonly TimeSpan AttemptCooldown = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<int, DateTime> RecentAttempts = new();
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> HydrationLocks = new();
    private readonly ITmdbService _tmdbService;

    public TvMetadataHydrationService(ITmdbService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    public async Task<TvMetadataHydrationResult> EnsureHydratedBySeriesIdAsync(
        int tvSeriesId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (tvSeriesId <= 0)
        {
            return new TvMetadataHydrationResult { Skipped = true };
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var series = await dbContext.TvSeries
            .AsNoTracking()
            .Where(x => x.Id == tvSeriesId)
            .Select(x => new { x.Id, x.TmdbSeriesId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (series?.TmdbSeriesId is not > 0)
        {
            return new TvMetadataHydrationResult
            {
                TvSeriesId = series?.Id,
                Skipped = true
            };
        }

        return await HydrateSeriesAsync(series.TmdbSeriesId.Value, force, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TvMetadataHydrationResult> HydrateSeriesAsync(
        int tmdbSeriesId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var result = new TvMetadataHydrationResult { TmdbSeriesId = tmdbSeriesId };
        if (tmdbSeriesId <= 0)
        {
            result.AddError("TMDB Series ID 无效。");
            return result;
        }

        var gate = HydrationLocks.GetOrAdd(tmdbSeriesId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!force && RecentAttempts.TryGetValue(tmdbSeriesId, out var lastAttempt)
                && DateTime.UtcNow - lastAttempt < AttemptCooldown)
            {
                var existingSeriesId = await FindSeriesIdAsync(tmdbSeriesId, cancellationToken).ConfigureAwait(false);
                if (existingSeriesId.HasValue)
                {
                    result.Skipped = true;
                    result.TvSeriesId = existingSeriesId;
                    return result;
                }

                RecentAttempts.TryRemove(tmdbSeriesId, out _);
            }

            var seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(tmdbSeriesId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (seriesDetails is null)
            {
                result.AddError("无法读取 TMDB TV Series metadata。");
                result.TvSeriesId = await FindSeriesIdAsync(tmdbSeriesId, cancellationToken).ConfigureAwait(false);
                return result;
            }

            var seasonSummaries = NormalizeSeasonSummaries(seriesDetails.Seasons);
            var seasonDetailsByNumber = new Dictionary<int, TmdbTvSeasonDetailResult?>();
            foreach (var summary in seasonSummaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var seasonDetail = await _tmdbService.GetTvSeasonDetailsAsync(
                        tmdbSeriesId,
                        summary.SeasonNumber,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (seasonDetail is null)
                {
                    result.AddError($"{FormatSeasonLabel(summary.SeasonNumber)} metadata 暂不可用。");
                }

                seasonDetailsByNumber[summary.SeasonNumber] = seasonDetail;
            }

            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            var tvSeries = await UpsertSeriesAsync(dbContext, seriesDetails, cancellationToken).ConfigureAwait(false);
            result.TvSeriesId = tvSeries.Id;

            foreach (var summary in seasonSummaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var seasonDetail = seasonDetailsByNumber.GetValueOrDefault(summary.SeasonNumber);

                var seasonResult = await UpsertSeasonAsync(
                        dbContext,
                        tvSeries,
                        summary,
                        seasonDetail,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (seasonResult.IsAdded)
                {
                    result.AddedSeasonCount++;
                }
                else
                {
                    result.UpdatedSeasonCount++;
                }

                if (seasonDetail is null)
                {
                    continue;
                }

                foreach (var episode in NormalizeEpisodeMetadata(seasonDetail.Episodes))
                {
                    var episodeAdded = await UpsertEpisodeAsync(
                            dbContext,
                            seasonResult.Season,
                            episode,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (episodeAdded)
                    {
                        result.AddedEpisodeCount++;
                    }
                    else
                    {
                        result.UpdatedEpisodeCount++;
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            RecentAttempts[tmdbSeriesId] = DateTime.UtcNow;
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<int?> FindSeriesIdAsync(int tmdbSeriesId, CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        return await dbContext.TvSeries
            .AsNoTracking()
            .Where(x => x.TmdbSeriesId == tmdbSeriesId)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<TvSeries> UpsertSeriesAsync(
        AppDbContext dbContext,
        TmdbTvSeriesDetailResult details,
        CancellationToken cancellationToken)
    {
        var tvSeries = await dbContext.TvSeries
            .FirstOrDefaultAsync(x => x.TmdbSeriesId == details.TmdbId, cancellationToken)
            .ConfigureAwait(false);
        if (tvSeries is null)
        {
            tvSeries = new TvSeries
            {
                TmdbSeriesId = details.TmdbId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvSeries.Add(tvSeries);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        tvSeries.Name = TruncateRequired(FirstNonEmpty(details.Name, $"TV {details.TmdbId}"), 300);
        tvSeries.OriginalName = Truncate(FirstNonEmpty(details.OriginalName), 300);
        tvSeries.Overview = Truncate(details.Overview, 5000);
        tvSeries.PosterRemoteUrl = EmptyToNull(details.PosterRemoteUrl);
        tvSeries.Country = Truncate(string.Join(", ", details.OriginCountries), 120);
        tvSeries.Language = Truncate(details.OriginalLanguage, 120);
        tvSeries.FirstAirDate = ParseDate(details.FirstAirDate);
        tvSeries.FirstAirYear = details.FirstAirYear;
        tvSeries.GenresText = Truncate(details.GenresText, 1000);
        tvSeries.UpdatedAt = DateTime.UtcNow;
        return tvSeries;
    }

    private static async Task<SeasonUpsertResult> UpsertSeasonAsync(
        AppDbContext dbContext,
        TvSeries series,
        TmdbTvSeasonSummaryItem summary,
        TmdbTvSeasonDetailResult? detail,
        CancellationToken cancellationToken)
    {
        var season = await dbContext.TvSeasons
            .FirstOrDefaultAsync(
                x => x.TvSeriesId == series.Id && x.SeasonNumber == summary.SeasonNumber,
                cancellationToken)
            .ConfigureAwait(false);
        var isAdded = season is null;
        if (season is null)
        {
            season = new TvSeason
            {
                TvSeriesId = series.Id,
                SeasonNumber = summary.SeasonNumber,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvSeasons.Add(season);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        season.TmdbSeasonId = PositiveOrNull(detail?.TmdbId) ?? PositiveOrNull(summary.TmdbId);
        season.Name = TruncateRequired(
            FirstNonEmpty(
                detail?.Name,
                summary.Name,
                summary.SeasonNumber == 0 ? "Specials / 特别篇" : $"Season {summary.SeasonNumber}"),
            300);
        season.Overview = Truncate(FirstNonEmpty(detail?.Overview, summary.Overview), 5000);
        season.PosterRemoteUrl = EmptyToNull(FirstNonEmpty(detail?.PosterRemoteUrl, summary.PosterRemoteUrl));
        season.AirDate = ParseDate(FirstNonEmpty(detail?.AirDate, summary.AirDate));
        season.TmdbEpisodeCount = detail is not null && detail.EpisodeCount > 0
            ? detail.EpisodeCount
            : summary.EpisodeCount;
        season.IdentifiedConfidence = 1d;
        season.IdentificationStatus = IdentificationStatus.Matched;
        season.UpdatedAt = DateTime.UtcNow;
        return new SeasonUpsertResult(season, isAdded);
    }

    private static async Task<bool> UpsertEpisodeAsync(
        AppDbContext dbContext,
        TvSeason season,
        TmdbTvEpisodeMetadataItem metadata,
        CancellationToken cancellationToken)
    {
        var episode = await dbContext.TvEpisodes
            .FirstOrDefaultAsync(
                x => x.TvSeasonId == season.Id && x.EpisodeNumber == metadata.EpisodeNumber,
                cancellationToken)
            .ConfigureAwait(false);
        var isAdded = episode is null;
        if (episode is null)
        {
            episode = new TvEpisode
            {
                TvSeasonId = season.Id,
                EpisodeNumber = metadata.EpisodeNumber,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvEpisodes.Add(episode);
        }

        episode.TmdbEpisodeId = PositiveOrNull(metadata.TmdbId);
        episode.Title = TruncateRequired(FirstNonEmpty(metadata.Name, episode.Title, $"E{metadata.EpisodeNumber:D2}"), 300);
        episode.Overview = Truncate(metadata.Overview, 5000);
        episode.StillRemoteUrl = EmptyToNull(metadata.StillRemoteUrl);
        episode.AirDate = ParseDate(metadata.AirDate);
        episode.RuntimeMinutes = metadata.RuntimeMinutes;
        episode.UpdatedAt = DateTime.UtcNow;
        return isAdded;
    }

    private static IReadOnlyList<TmdbTvSeasonSummaryItem> NormalizeSeasonSummaries(
        IReadOnlyList<TmdbTvSeasonSummaryItem> summaries)
    {
        return summaries
            .Where(x => x.SeasonNumber >= 0)
            .GroupBy(x => x.SeasonNumber)
            .Select(x => x.OrderByDescending(y => y.TmdbId ?? 0).First())
            .OrderBy(x => x.SeasonNumber)
            .ToList();
    }

    private static IReadOnlyList<TmdbTvEpisodeMetadataItem> NormalizeEpisodeMetadata(
        IReadOnlyList<TmdbTvEpisodeMetadataItem> episodes)
    {
        return episodes
            .Where(x => x.EpisodeNumber > 0)
            .GroupBy(x => x.EpisodeNumber)
            .Select(x => x.OrderByDescending(y => y.TmdbId).First())
            .OrderBy(x => x.EpisodeNumber)
            .ToList();
    }

    private static string FormatSeasonLabel(int seasonNumber)
    {
        return seasonNumber == 0 ? "特别篇" : $"S{seasonNumber:D2}";
    }

    private static DateTime? ParseDate(string? value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static int? PositiveOrNull(int? value)
    {
        return value is > 0 ? value : null;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TruncateRequired(string value, int maxLength)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed record SeasonUpsertResult(TvSeason Season, bool IsAdded);
}
