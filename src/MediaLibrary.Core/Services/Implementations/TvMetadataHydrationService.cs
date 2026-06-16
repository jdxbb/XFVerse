using System.Collections.Concurrent;
using System.Globalization;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
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
    private readonly IOmdbService _omdbService;

    public TvMetadataHydrationService(ITmdbService tmdbService, IOmdbService omdbService)
    {
        _tmdbService = tmdbService;
        _omdbService = omdbService;
    }

    public async Task<TvMetadataHydrationResult> EnsureSeriesSummaryBySeriesIdAsync(
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

        return await EnsureSeriesSummaryAsync(series.TmdbSeriesId.Value, force, cancellationToken)
            .ConfigureAwait(false);
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

    public async Task<TvMetadataHydrationResult> EnsureSeriesSummaryAsync(
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
            var existingSeriesId = await FindSeriesIdAsync(tmdbSeriesId, cancellationToken).ConfigureAwait(false);
            if (existingSeriesId.HasValue
                && await MetadataDetailRefreshCooldown.IsTvSeriesSummaryCoolingDownAsync(tmdbSeriesId, cancellationToken)
                    .ConfigureAwait(false))
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-series-detail-tmdb-metadata-refresh-skipped tmdbSeriesId={tmdbSeriesId} refreshKind=\"summary\" skippedReason=\"cooldown\"");
                result.Skipped = true;
                result.TvSeriesId = existingSeriesId;
                return result;
            }

            var seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(
                    tmdbSeriesId,
                    cancellationToken: cancellationToken,
                    forceRefresh: force)
                .ConfigureAwait(false);
            if (seriesDetails is null)
            {
                result.AddError("无法读取 TMDB TV Series metadata。");
                result.TvSeriesId = existingSeriesId;
                return result;
            }

            var seasonSummaries = NormalizeSeasonSummaries(seriesDetails.Seasons);
            var omdbRating = await LoadSeriesOmdbRatingAsync(tmdbSeriesId, cancellationToken).ConfigureAwait(false);
            if (!force
                && existingSeriesId.HasValue
                && await HasSeriesSummaryAsync(existingSeriesId.Value, seriesDetails, seasonSummaries, cancellationToken).ConfigureAwait(false))
            {
                result.Skipped = true;
                result.TvSeriesId = existingSeriesId;
                return result;
            }

            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            var seriesResult = await UpsertSeriesAsync(dbContext, seriesDetails, omdbRating, cancellationToken).ConfigureAwait(false);
            var tvSeries = seriesResult.Series;
            result.SeriesChanged = seriesResult.IsChanged;
            result.TvSeriesId = tvSeries.Id;

            foreach (var summary in seasonSummaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var seasonResult = await UpsertSeasonAsync(
                        dbContext,
                        tvSeries,
                        summary,
                        detail: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (seasonResult.IsAdded)
                {
                    result.AddedSeasonCount++;
                }
                else if (seasonResult.IsChanged)
                {
                    result.UpdatedSeasonCount++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await MetadataDetailRefreshCooldown.MarkTvSeriesSummarySucceededAsync(tmdbSeriesId, CancellationToken.None)
                .ConfigureAwait(false);
            ScanIdentificationDiagnostics.Write(
                $"event=tv-series-detail-tmdb-metadata-refresh-succeeded tmdbSeriesId={tmdbSeriesId} refreshKind=\"summary\" changed={FormatBool(result.HasChanges)} seriesChanged={FormatBool(result.SeriesChanged)} addedSeasonCount={result.AddedSeasonCount} updatedSeasonCount={result.UpdatedSeasonCount} cooldownHours=4");
            return result;
        }
        finally
        {
            gate.Release();
        }
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
            var existingSeriesId = await FindSeriesIdAsync(tmdbSeriesId, cancellationToken).ConfigureAwait(false);
            if (existingSeriesId.HasValue
                && await MetadataDetailRefreshCooldown.IsTvSeriesFullCoolingDownAsync(tmdbSeriesId, cancellationToken)
                    .ConfigureAwait(false))
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=tv-series-detail-tmdb-metadata-refresh-skipped tmdbSeriesId={tmdbSeriesId} refreshKind=\"full\" skippedReason=\"cooldown\"");
                result.Skipped = true;
                result.TvSeriesId = existingSeriesId;
                return result;
            }

            if (!force && RecentAttempts.TryGetValue(tmdbSeriesId, out var lastAttempt)
                && DateTime.UtcNow - lastAttempt < AttemptCooldown)
            {
                if (existingSeriesId.HasValue)
                {
                    result.Skipped = true;
                    result.TvSeriesId = existingSeriesId;
                    return result;
                }

                RecentAttempts.TryRemove(tmdbSeriesId, out _);
            }

            var seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(
                    tmdbSeriesId,
                    cancellationToken: cancellationToken,
                    forceRefresh: force)
                .ConfigureAwait(false);
            if (seriesDetails is null)
            {
                result.AddError("无法读取 TMDB TV Series metadata。");
                result.TvSeriesId = await FindSeriesIdAsync(tmdbSeriesId, cancellationToken).ConfigureAwait(false);
                return result;
            }

            var seasonSummaries = NormalizeSeasonSummaries(seriesDetails.Seasons);
            var omdbRating = await LoadSeriesOmdbRatingAsync(tmdbSeriesId, cancellationToken).ConfigureAwait(false);
            var seasonDetailsByNumber = new Dictionary<int, TmdbTvSeasonDetailResult?>();
            foreach (var summary in seasonSummaries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var seasonDetail = await _tmdbService.GetTvSeasonDetailsAsync(
                        tmdbSeriesId,
                        summary.SeasonNumber,
                        cancellationToken: cancellationToken,
                        forceRefresh: force)
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

            var seriesResult = await UpsertSeriesAsync(dbContext, seriesDetails, omdbRating, cancellationToken).ConfigureAwait(false);
            var tvSeries = seriesResult.Series;
            result.SeriesChanged = seriesResult.IsChanged;
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
                else if (seasonResult.IsChanged)
                {
                    result.UpdatedSeasonCount++;
                }

                if (seasonDetail is null)
                {
                    continue;
                }

                foreach (var episode in NormalizeEpisodeMetadata(seasonDetail.Episodes))
                {
                    var episodeResult = await UpsertEpisodeAsync(
                            dbContext,
                            seasonResult.Season,
                            episode,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (episodeResult.IsAdded)
                    {
                        result.AddedEpisodeCount++;
                    }
                    else if (episodeResult.IsChanged)
                    {
                        result.UpdatedEpisodeCount++;
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            RecentAttempts[tmdbSeriesId] = DateTime.UtcNow;
            if (!result.HasErrors)
            {
                await MetadataDetailRefreshCooldown.MarkTvSeriesFullSucceededAsync(tmdbSeriesId, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            ScanIdentificationDiagnostics.Write(
                $"event=tv-series-detail-tmdb-metadata-refresh-succeeded tmdbSeriesId={tmdbSeriesId} refreshKind=\"full\" changed={FormatBool(result.HasChanges)} seriesChanged={FormatBool(result.SeriesChanged)} addedSeasonCount={result.AddedSeasonCount} updatedSeasonCount={result.UpdatedSeasonCount} addedEpisodeCount={result.AddedEpisodeCount} updatedEpisodeCount={result.UpdatedEpisodeCount} hasErrors={FormatBool(result.HasErrors)} cooldownHours=4");
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<TvMetadataHydrationResult> EnsureSeasonEpisodesAsync(
        int tvSeasonId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var result = new TvMetadataHydrationResult();
        if (tvSeasonId <= 0)
        {
            result.AddError("TV Season ID 无效。");
            return result;
        }

        var seasonKey = await LoadSeasonHydrationKeyAsync(tvSeasonId, cancellationToken).ConfigureAwait(false);
        if (seasonKey is null)
        {
            result.AddError("未找到对应的 TV Season。");
            return result;
        }

        result.TmdbSeriesId = seasonKey.TmdbSeriesId;
        result.TvSeriesId = seasonKey.SeriesId;

        if (!force
            && seasonKey.ExistingEpisodeCount > 0
            && seasonKey.TmdbEpisodeCount.GetValueOrDefault() > 0
            && seasonKey.ExistingEpisodeCount >= seasonKey.TmdbEpisodeCount!.Value)
        {
            result.Skipped = true;
            return result;
        }

        var gate = HydrationLocks.GetOrAdd(seasonKey.TmdbSeriesId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var seasonDetail = await _tmdbService.GetTvSeasonDetailsAsync(
                    seasonKey.TmdbSeriesId,
                    seasonKey.SeasonNumber,
                    cancellationToken: cancellationToken,
                    forceRefresh: force)
                .ConfigureAwait(false);
            if (seasonDetail is null)
            {
                result.AddError($"{FormatSeasonLabel(seasonKey.SeasonNumber)} metadata 暂不可用。");
                return result;
            }

            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            var series = await dbContext.TvSeries
                .FirstAsync(x => x.Id == seasonKey.SeriesId, cancellationToken)
                .ConfigureAwait(false);
            var seasonResult = await UpsertSeasonAsync(
                    dbContext,
                    series,
                    BuildSeasonSummaryFromDetail(seasonDetail),
                    seasonDetail,
                    cancellationToken)
                .ConfigureAwait(false);
            if (seasonResult.IsAdded)
            {
                result.AddedSeasonCount++;
            }
            else if (seasonResult.IsChanged)
            {
                result.UpdatedSeasonCount++;
            }

            foreach (var episode in NormalizeEpisodeMetadata(seasonDetail.Episodes))
            {
                var episodeResult = await UpsertEpisodeAsync(
                        dbContext,
                        seasonResult.Season,
                        episode,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (episodeResult.IsAdded)
                {
                    result.AddedEpisodeCount++;
                }
                else if (episodeResult.IsChanged)
                {
                    result.UpdatedEpisodeCount++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

    private static async Task<bool> HasSeriesSummaryAsync(
        int tvSeriesId,
        TmdbTvSeriesDetailResult seriesDetails,
        IReadOnlyCollection<TmdbTvSeasonSummaryItem> seasonSummaries,
        CancellationToken cancellationToken)
    {
        if (seasonSummaries.Count == 0)
        {
            return false;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var localSeries = await dbContext.TvSeries
            .AsNoTracking()
            .Where(x => x.Id == tvSeriesId)
            .Select(x => new
            {
                x.DirectorText,
                x.WriterText,
                x.ActorsText,
                x.ProductionStatus,
                x.NetworksText,
                x.ProductionCompaniesText
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (localSeries is null
            || !string.Equals(localSeries.DirectorText ?? string.Empty, Truncate(seriesDetails.DirectorText, 1000) ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(localSeries.WriterText ?? string.Empty, Truncate(seriesDetails.WriterText, 1000) ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(localSeries.ActorsText ?? string.Empty, Truncate(seriesDetails.ActorsText, 1000) ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(localSeries.ProductionStatus ?? string.Empty, Truncate(seriesDetails.ProductionStatus, 120) ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(localSeries.NetworksText ?? string.Empty, Truncate(seriesDetails.NetworksText, 1000) ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(localSeries.ProductionCompaniesText ?? string.Empty, Truncate(seriesDetails.ProductionCompaniesText, 1000) ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        var localSeasons = await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => x.TvSeriesId == tvSeriesId)
            .Select(x => new { x.SeasonNumber, x.TmdbEpisodeCount })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var localByNumber = localSeasons
            .GroupBy(x => x.SeasonNumber)
            .ToDictionary(x => x.Key, x => x.First().TmdbEpisodeCount);
        return seasonSummaries.All(
            summary =>
            {
                if (!localByNumber.TryGetValue(summary.SeasonNumber, out var localEpisodeCount))
                {
                    return false;
                }

                return summary.EpisodeCount <= 0
                       || localEpisodeCount.GetValueOrDefault() >= summary.EpisodeCount;
            });
    }

    private static async Task<SeasonHydrationKey?> LoadSeasonHydrationKeyAsync(int tvSeasonId, CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        return await dbContext.TvSeasons
            .AsNoTracking()
            .Where(x => x.Id == tvSeasonId && x.Series!.TmdbSeriesId.HasValue)
            .Select(
                x => new SeasonHydrationKey(
                    x.Id,
                    x.TvSeriesId,
                    x.Series!.TmdbSeriesId!.Value,
                    x.SeasonNumber,
                    x.TmdbEpisodeCount,
                    x.Episodes.Count))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<MovieRatingItem?> LoadSeriesOmdbRatingAsync(
        int tmdbSeriesId,
        CancellationToken cancellationToken)
    {
        try
        {
            var externalIds = await _tmdbService.GetTvSeriesExternalIdsAsync(tmdbSeriesId, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(externalIds?.ImdbId))
            {
                return null;
            }

            return await _omdbService.GetSeriesRatingAsync(externalIds.ImdbId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<SeriesUpsertResult> UpsertSeriesAsync(
        AppDbContext dbContext,
        TmdbTvSeriesDetailResult details,
        MovieRatingItem? omdbRating,
        CancellationToken cancellationToken)
    {
        var tvSeries = await dbContext.TvSeries
            .Include(x => x.RatingSources)
            .FirstOrDefaultAsync(x => x.TmdbSeriesId == details.TmdbId, cancellationToken)
            .ConfigureAwait(false);
        var isAdded = tvSeries is null;
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

        var changed = isAdded;
        changed |= SetIfChanged(tvSeries.Name, TruncateRequired(FirstNonEmpty(details.Name, $"TV {details.TmdbId}"), 300), value => tvSeries.Name = value);
        changed |= SetIfChanged(tvSeries.OriginalName, Truncate(FirstNonEmpty(details.OriginalName), 300), value => tvSeries.OriginalName = value);
        changed |= SetIfChanged(tvSeries.Overview, Truncate(details.Overview, 5000), value => tvSeries.Overview = value);
        changed |= SetIfChanged(tvSeries.PosterRemoteUrl, EmptyToNull(details.PosterRemoteUrl), value => tvSeries.PosterRemoteUrl = value);
        changed |= SetIfChanged(tvSeries.Country, Truncate(string.Join(", ", details.OriginCountries), 120), value => tvSeries.Country = value);
        changed |= SetIfChanged(tvSeries.Language, Truncate(details.OriginalLanguage, 120), value => tvSeries.Language = value);
        changed |= SetIfChanged(tvSeries.FirstAirDate, ParseDate(details.FirstAirDate), value => tvSeries.FirstAirDate = value);
        changed |= SetIfChanged(tvSeries.FirstAirYear, details.FirstAirYear, value => tvSeries.FirstAirYear = value);
        changed |= SetIfChanged(tvSeries.GenresText, Truncate(details.GenresText, 1000), value => tvSeries.GenresText = value);
        changed |= SetIfChanged(tvSeries.DirectorText, Truncate(details.DirectorText, 1000), value => tvSeries.DirectorText = value);
        changed |= SetIfChanged(tvSeries.WriterText, Truncate(details.WriterText, 1000), value => tvSeries.WriterText = value);
        changed |= SetIfChanged(tvSeries.ActorsText, Truncate(details.ActorsText, 1000), value => tvSeries.ActorsText = value);
        changed |= SetIfChanged(tvSeries.ProductionStatus, Truncate(details.ProductionStatus, 120), value => tvSeries.ProductionStatus = value);
        changed |= SetIfChanged(tvSeries.NetworksText, Truncate(details.NetworksText, 1000), value => tvSeries.NetworksText = value);
        changed |= SetIfChanged(tvSeries.ProductionCompaniesText, Truncate(details.ProductionCompaniesText, 1000), value => tvSeries.ProductionCompaniesText = value);
        changed |= UpsertSeriesRating(
            tvSeries,
            "TMDB",
            details.TmdbRating,
            10d,
            details.TmdbVoteCount,
            $"https://www.themoviedb.org/tv/{details.TmdbId}");
        if (omdbRating is not null)
        {
            changed |= UpsertSeriesRating(
                tvSeries,
                "OMDb",
                omdbRating.ScoreValue,
                omdbRating.ScoreScale,
                omdbRating.VoteCount,
                omdbRating.SourceUrl);
        }

        if (changed)
        {
            tvSeries.UpdatedAt = DateTime.UtcNow;
        }

        return new SeriesUpsertResult(tvSeries, isAdded, changed);
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

        var changed = isAdded;
        changed |= SetIfChanged(season.TmdbSeasonId, PositiveOrNull(detail?.TmdbId) ?? PositiveOrNull(summary.TmdbId), value => season.TmdbSeasonId = value);
        changed |= SetIfChanged(season.Name, TruncateRequired(
            FirstNonEmpty(
                detail?.Name,
                summary.Name,
                summary.SeasonNumber == 0 ? "Specials / 特别篇" : $"Season {summary.SeasonNumber}"),
            300), value => season.Name = value);
        changed |= SetIfChanged(season.Overview, Truncate(FirstNonEmpty(detail?.Overview, summary.Overview), 5000), value => season.Overview = value);
        changed |= SetIfChanged(season.PosterRemoteUrl, EmptyToNull(FirstNonEmpty(detail?.PosterRemoteUrl, summary.PosterRemoteUrl)), value => season.PosterRemoteUrl = value);
        changed |= SetIfChanged(season.AirDate, ParseDate(FirstNonEmpty(detail?.AirDate, summary.AirDate)), value => season.AirDate = value);
        changed |= SetIfChanged(season.TmdbEpisodeCount, detail is not null && detail.EpisodeCount > 0
            ? detail.EpisodeCount
            : summary.EpisodeCount, value => season.TmdbEpisodeCount = value);
        changed |= SetIfChanged(season.IdentifiedConfidence, 1d, value => season.IdentifiedConfidence = value);
        changed |= SetIfChanged(season.IdentificationStatus, IdentificationStatus.Matched, value => season.IdentificationStatus = value);
        if (changed)
        {
            season.UpdatedAt = DateTime.UtcNow;
        }

        return new SeasonUpsertResult(season, isAdded, changed);
    }

    private static async Task<EpisodeUpsertResult> UpsertEpisodeAsync(
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

        var changed = isAdded;
        changed |= SetIfChanged(episode.TmdbEpisodeId, PositiveOrNull(metadata.TmdbId), value => episode.TmdbEpisodeId = value);
        changed |= SetIfChanged(episode.Title, TruncateRequired(FirstNonEmpty(metadata.Name, episode.Title, $"E{metadata.EpisodeNumber:D2}"), 300), value => episode.Title = value);
        changed |= SetIfChanged(episode.Overview, Truncate(metadata.Overview, 5000), value => episode.Overview = value);
        changed |= SetIfChanged(episode.StillRemoteUrl, EmptyToNull(metadata.StillRemoteUrl), value => episode.StillRemoteUrl = value);
        changed |= SetIfChanged(episode.AirDate, ParseDate(metadata.AirDate), value => episode.AirDate = value);
        changed |= SetIfChanged(episode.RuntimeMinutes, metadata.RuntimeMinutes, value => episode.RuntimeMinutes = value);
        if (changed)
        {
            episode.UpdatedAt = DateTime.UtcNow;
        }

        return new EpisodeUpsertResult(isAdded, changed);
    }

    private static TmdbTvSeasonSummaryItem BuildSeasonSummaryFromDetail(TmdbTvSeasonDetailResult detail)
    {
        return new TmdbTvSeasonSummaryItem
        {
            TmdbId = detail.TmdbId,
            SeasonNumber = detail.SeasonNumber,
            Name = detail.Name,
            Overview = detail.Overview,
            PosterRemoteUrl = detail.PosterRemoteUrl,
            AirDate = detail.AirDate,
            EpisodeCount = detail.EpisodeCount,
            TmdbRating = detail.TmdbRating
        };
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

    private static bool UpsertSeriesRating(
        TvSeries series,
        string sourceName,
        double? scoreValue,
        double scoreScale,
        int? voteCount,
        string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceName) || scoreValue is not > 0 || scoreScale <= 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var nextSourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl;
        var rating = series.RatingSources.FirstOrDefault(
            x => string.Equals(x.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
        if (rating is null)
        {
            rating = new TvSeriesRatingSource
            {
                SourceName = sourceName,
                CreatedAt = now
            };
            series.RatingSources.Add(rating);
        }
        else if (rating.ScoreValue.Equals(scoreValue.Value)
                 && rating.ScoreScale.Equals(scoreScale)
                 && rating.VoteCount == voteCount
                 && string.Equals(rating.SourceUrl, nextSourceUrl, StringComparison.Ordinal))
        {
            return false;
        }

        rating.ScoreValue = scoreValue.Value;
        rating.ScoreScale = scoreScale;
        rating.VoteCount = voteCount;
        rating.SourceUrl = nextSourceUrl;
        rating.LastUpdatedAt = now;
        return true;
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

    private static bool SetIfChanged<T>(T currentValue, T nextValue, Action<T> apply)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, nextValue))
        {
            return false;
        }

        apply(nextValue);
        return true;
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private sealed record SeriesUpsertResult(TvSeries Series, bool IsAdded, bool IsChanged);

    private sealed record SeasonUpsertResult(TvSeason Season, bool IsAdded, bool IsChanged);

    private sealed record EpisodeUpsertResult(bool IsAdded, bool IsChanged);

    private sealed record SeasonHydrationKey(
        int SeasonId,
        int SeriesId,
        int TmdbSeriesId,
        int SeasonNumber,
        int? TmdbEpisodeCount,
        int ExistingEpisodeCount);
}
