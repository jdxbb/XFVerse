using System.Globalization;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class UnknownSeasonCorrectionService : IUnknownSeasonCorrectionService
{
    private readonly ITmdbService _tmdbService;

    public UnknownSeasonCorrectionService(ITmdbService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    public Task<UnknownSeasonCorrectionApplyResult> ApplyUnknownSeasonToRecognizedSeasonAsync(
        int sourceSeasonId,
        int targetSeriesTmdbId,
        int targetSeasonNumber,
        IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping>? episodeMappings = null,
        CancellationToken cancellationToken = default)
    {
        return ApplySeasonToRecognizedSeasonAsync(
            sourceSeasonId,
            targetSeriesTmdbId,
            targetSeasonNumber,
            episodeMappings,
            cancellationToken);
    }

    public async Task<UnknownSeasonCorrectionApplyResult> ApplySeasonToRecognizedSeasonAsync(
        int sourceSeasonId,
        int targetSeriesTmdbId,
        int targetSeasonNumber,
        IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping>? episodeMappings = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceSeasonId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSeasonId));
        }

        if (targetSeriesTmdbId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSeriesTmdbId));
        }

        if (targetSeasonNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSeasonNumber));
        }

        ScanIdentificationDiagnostics.Write(
            $"event=season-correction-apply-started sourceSeasonId={sourceSeasonId} targetKind=recognized targetSeriesTmdbId={targetSeriesTmdbId} targetSeasonNumber={targetSeasonNumber} mappingCount={episodeMappings?.Count ?? 0}");

        try
        {
            var seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(
                    targetSeriesTmdbId,
                    cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Unable to load target TV series details.");
            var seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(
                targetSeriesTmdbId,
                targetSeasonNumber,
                cancellationToken: cancellationToken);
            if (seasonDetails is null)
            {
                ScanIdentificationDiagnostics.Write(
                    $"event=season-correction-local-season-fallback sourceSeasonId={sourceSeasonId} targetSeriesTmdbId={targetSeriesTmdbId} targetSeasonNumber={targetSeasonNumber} reason=\"target-season-detail-unavailable\"");
            }

            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var sourceSeason = await dbContext.TvSeasons
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                .ThenInclude(x => x.MediaFiles)
                .ThenInclude(x => x.SourceConnection)
                .FirstOrDefaultAsync(x => x.Id == sourceSeasonId, cancellationToken)
                ?? throw new InvalidOperationException("Source season does not exist.");
            var sourceSeasonKind = ResolveSeasonKind(sourceSeason);

            var sourceRows = sourceSeason.Episodes
                .OrderBy(x => x.EpisodeNumber)
                .ThenBy(x => x.Id)
                .SelectMany(episode => episode.MediaFiles
                    .Where(IsActiveVideo)
                    .OrderBy(mediaFile => mediaFile.Id)
                    .Select(mediaFile => new SourceMoveRow(episode.EpisodeNumber, episode.EpisodeNumber, mediaFile)))
                .ToList();
            if (sourceRows.Count == 0)
            {
                throw new InvalidOperationException("Source unknown season has no playable sources to correct.");
            }

            var mappingLookup = BuildValidatedEpisodeMappingLookup(episodeMappings, sourceRows);
            if (mappingLookup.Count > 0)
            {
                sourceRows = sourceRows
                    .Select(row => mappingLookup.TryGetValue(row.MediaFile.Id, out var targetEpisodeNumber)
                        ? row with { TargetEpisodeNumber = targetEpisodeNumber }
                        : row)
                    .ToList();
            }

            var remappedSourceCount = sourceRows.Count(x => x.OriginalEpisodeNumber != x.TargetEpisodeNumber);
            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-mapping-validated sourceSeasonId={sourceSeasonId} targetSeasonNumber={targetSeasonNumber} movedSourceCount={sourceRows.Count} remappedSourceCount={remappedSourceCount} mappingSummary={ScanIdentificationDiagnostics.FormatValue(BuildMappingSummary(sourceRows), 240)}");

            var targetSeries = await UpsertTargetSeriesAsync(dbContext, seriesDetails, now, cancellationToken);
            var targetSeason = await UpsertTargetSeasonAsync(
                dbContext,
                targetSeries,
                seriesDetails,
                targetSeasonNumber,
                seasonDetails,
                now,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            var targetMetadataByEpisodeNumber = seasonDetails is null
                ? new Dictionary<int, TmdbTvEpisodeMetadataItem>()
                : seasonDetails.Episodes
                    .Where(x => x.EpisodeNumber > 0)
                    .GroupBy(x => x.EpisodeNumber)
                    .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.TmdbId).First());
            var moveResult = await MoveSourcesToTargetSeasonAsync(
                dbContext,
                targetSeason,
                sourceRows,
                targetMetadataByEpisodeNumber,
                now,
                cancellationToken);

            sourceSeason.UpdatedAt = now;
            if (sourceSeason.Series is not null)
            {
                sourceSeason.Series.UpdatedAt = now;
            }

            var oldContainerHidden = await HideSourceSeasonIfEmptyAsync(
                dbContext,
                sourceSeason,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-apply-succeeded sourceSeasonId={sourceSeasonId} sourceSeasonKind={sourceSeasonKind} targetKind=recognized targetSeriesId={targetSeries.Id} targetSeasonId={targetSeason.Id} targetLocalSeasonFallback={(seasonDetails is null).ToString().ToLowerInvariant()} movedSourceCount={sourceRows.Count} createdEpisodeCount={moveResult.CreatedEpisodeCount} appendedSourceCount={moveResult.AppendedSourceCount} remappedSourceCount={remappedSourceCount} oldDefaultFallback={moveResult.OldDefaultFallback.ToString().ToLowerInvariant()} sourceContainerHidden={oldContainerHidden.ToString().ToLowerInvariant()} sourceContainerPreserved={(!oldContainerHidden).ToString().ToLowerInvariant()}");

            return new UnknownSeasonCorrectionApplyResult
            {
                SourceSeasonId = sourceSeasonId,
                TargetSeriesId = targetSeries.Id,
                TargetSeasonId = targetSeason.Id,
                SourceSeasonKind = sourceSeasonKind,
                TargetSeasonKind = "recognized",
                MovedSourceCount = sourceRows.Count,
                CreatedEpisodeCount = moveResult.CreatedEpisodeCount,
                AppendedSourceCount = moveResult.AppendedSourceCount,
                OldContainerHidden = oldContainerHidden,
                OldContainerPreserved = !oldContainerHidden,
                OldDefaultFallback = moveResult.OldDefaultFallback,
                RemappedSourceCount = remappedSourceCount
            };
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-apply-failed sourceSeasonId={sourceSeasonId} targetKind=recognized targetSeriesTmdbId={targetSeriesTmdbId} targetSeasonNumber={targetSeasonNumber} failureReason={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 260)}");
            throw;
        }
    }

    public async Task<UnknownSeasonCorrectionApplyResult> ApplySeasonToUnknownSeasonAsync(
        int sourceSeasonId,
        int targetSeasonId,
        IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping>? episodeMappings = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceSeasonId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSeasonId));
        }

        if (targetSeasonId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSeasonId));
        }

        if (sourceSeasonId == targetSeasonId)
        {
            throw new InvalidOperationException("Source season and target unknown season must be different.");
        }

        ScanIdentificationDiagnostics.Write(
            $"event=season-correction-apply-started sourceSeasonId={sourceSeasonId} targetKind=unknown targetSeasonId={targetSeasonId} mappingCount={episodeMappings?.Count ?? 0}");

        try
        {
            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var sourceSeason = await dbContext.TvSeasons
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                .ThenInclude(x => x.MediaFiles)
                .ThenInclude(x => x.SourceConnection)
                .FirstOrDefaultAsync(x => x.Id == sourceSeasonId, cancellationToken)
                ?? throw new InvalidOperationException("Source season does not exist.");
            var sourceSeasonKind = ResolveSeasonKind(sourceSeason);

            var targetSeason = await dbContext.TvSeasons
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                .ThenInclude(x => x.MediaFiles)
                .ThenInclude(x => x.SourceConnection)
                .FirstOrDefaultAsync(x => x.Id == targetSeasonId, cancellationToken)
                ?? throw new InvalidOperationException("Target unknown season does not exist.");

            if (!IsUnknownSeason(targetSeason))
            {
                throw new InvalidOperationException("Target season must be a no-TMDB unknown season.");
            }

            var sourceRows = BuildSourceMoveRows(sourceSeason);
            if (sourceRows.Count == 0)
            {
                throw new InvalidOperationException("Source season has no playable sources to correct.");
            }

            var mappingLookup = BuildValidatedEpisodeMappingLookup(episodeMappings, sourceRows);
            if (mappingLookup.Count > 0)
            {
                sourceRows = sourceRows
                    .Select(row => mappingLookup.TryGetValue(row.MediaFile.Id, out var targetEpisodeNumber)
                        ? row with { TargetEpisodeNumber = targetEpisodeNumber }
                        : row)
                    .ToList();
            }

            var remappedSourceCount = sourceRows.Count(x => x.OriginalEpisodeNumber != x.TargetEpisodeNumber);
            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-mapping-validated sourceSeasonId={sourceSeasonId} targetKind=unknown targetSeasonId={targetSeasonId} movedSourceCount={sourceRows.Count} remappedSourceCount={remappedSourceCount} mappingSummary={ScanIdentificationDiagnostics.FormatValue(BuildMappingSummary(sourceRows), 240)}");

            var moveResult = await MoveSourcesToTargetSeasonAsync(
                dbContext,
                targetSeason,
                sourceRows,
                targetMetadataByEpisodeNumber: new Dictionary<int, TmdbTvEpisodeMetadataItem>(),
                now,
                cancellationToken);

            sourceSeason.UpdatedAt = now;
            if (sourceSeason.Series is not null)
            {
                sourceSeason.Series.UpdatedAt = now;
            }

            targetSeason.UpdatedAt = now;
            if (targetSeason.Series is not null)
            {
                targetSeason.Series.UpdatedAt = now;
            }

            var oldContainerHidden = await HideSourceSeasonIfEmptyAsync(
                dbContext,
                sourceSeason,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-apply-succeeded sourceSeasonId={sourceSeasonId} sourceSeasonKind={sourceSeasonKind} targetKind=unknown targetSeriesId={targetSeason.TvSeriesId} targetSeasonId={targetSeason.Id} movedSourceCount={sourceRows.Count} createdEpisodeCount={moveResult.CreatedEpisodeCount} appendedSourceCount={moveResult.AppendedSourceCount} remappedSourceCount={remappedSourceCount} oldDefaultFallback={moveResult.OldDefaultFallback.ToString().ToLowerInvariant()} sourceContainerHidden={oldContainerHidden.ToString().ToLowerInvariant()} sourceContainerPreserved={(!oldContainerHidden).ToString().ToLowerInvariant()}");

            return new UnknownSeasonCorrectionApplyResult
            {
                SourceSeasonId = sourceSeasonId,
                TargetSeriesId = targetSeason.TvSeriesId,
                TargetSeasonId = targetSeason.Id,
                SourceSeasonKind = sourceSeasonKind,
                TargetSeasonKind = "unknown",
                MovedSourceCount = sourceRows.Count,
                CreatedEpisodeCount = moveResult.CreatedEpisodeCount,
                AppendedSourceCount = moveResult.AppendedSourceCount,
                OldContainerHidden = oldContainerHidden,
                OldContainerPreserved = !oldContainerHidden,
                OldDefaultFallback = moveResult.OldDefaultFallback,
                RemappedSourceCount = remappedSourceCount
            };
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-apply-failed sourceSeasonId={sourceSeasonId} targetKind=unknown targetSeasonId={targetSeasonId} failureReason={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 260)}");
            throw;
        }
    }

    private static Dictionary<int, int> BuildValidatedEpisodeMappingLookup(
        IReadOnlyCollection<UnknownSeasonCorrectionEpisodeMapping>? episodeMappings,
        IReadOnlyCollection<SourceMoveRow> sourceRows)
    {
        var lookup = new Dictionary<int, int>();
        if (episodeMappings is null || episodeMappings.Count == 0)
        {
            return lookup;
        }

        var sourceRowsByMediaFileId = sourceRows.ToDictionary(x => x.MediaFile.Id);
        foreach (var mapping in episodeMappings)
        {
            if (mapping.MediaFileId <= 0)
            {
                throw new InvalidOperationException("Invalid episode mapping: media file id is required.");
            }

            if (mapping.TargetEpisodeNumber <= 0)
            {
                throw new InvalidOperationException("Invalid episode mapping: target episode number must be a positive integer.");
            }

            if (!sourceRowsByMediaFileId.TryGetValue(mapping.MediaFileId, out var sourceRow))
            {
                throw new InvalidOperationException("Invalid episode mapping: source media file is not part of the unknown season.");
            }

            if (mapping.OriginalEpisodeNumber > 0
                && mapping.OriginalEpisodeNumber != sourceRow.OriginalEpisodeNumber)
            {
                throw new InvalidOperationException("Invalid episode mapping: source episode number no longer matches the unknown season.");
            }

            if (lookup.TryGetValue(mapping.MediaFileId, out var existingTargetEpisodeNumber)
                && existingTargetEpisodeNumber != mapping.TargetEpisodeNumber)
            {
                throw new InvalidOperationException("Invalid episode mapping: duplicate source media file has conflicting target episode numbers.");
            }

            lookup[mapping.MediaFileId] = mapping.TargetEpisodeNumber;
        }

        return lookup;
    }

    private static List<SourceMoveRow> BuildSourceMoveRows(TvSeason sourceSeason)
    {
        return sourceSeason.Episodes
            .OrderBy(x => x.EpisodeNumber)
            .ThenBy(x => x.Id)
            .SelectMany(episode => episode.MediaFiles
                .Where(IsActiveVideo)
                .OrderBy(mediaFile => mediaFile.Id)
                .Select(mediaFile => new SourceMoveRow(episode.EpisodeNumber, episode.EpisodeNumber, mediaFile)))
            .ToList();
    }

    private static async Task<MoveSourcesResult> MoveSourcesToTargetSeasonAsync(
        AppDbContext dbContext,
        TvSeason targetSeason,
        IReadOnlyCollection<SourceMoveRow> sourceRows,
        IReadOnlyDictionary<int, TmdbTvEpisodeMetadataItem> targetMetadataByEpisodeNumber,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var targetEpisodes = await dbContext.TvEpisodes
            .Include(x => x.MediaFiles)
            .Where(x => x.TvSeasonId == targetSeason.Id)
            .ToListAsync(cancellationToken);
        var targetEpisodesByNumber = targetEpisodes
            .GroupBy(x => x.EpisodeNumber)
            .ToDictionary(x => x.Key, x => x.First());
        var targetSourceCountsByEpisodeNumber = targetEpisodesByNumber
            .ToDictionary(
                x => x.Key,
                x => x.Value.MediaFiles.Count(IsActiveVideo));
        var movedMediaFileIds = sourceRows
            .Select(x => x.MediaFile.Id)
            .ToHashSet();
        var sourceEpisodeIds = sourceRows
            .Select(x => x.MediaFile.EpisodeId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var targetDefaultMediaFileIdsByEpisodeNumber = new Dictionary<int, int>();

        var createdEpisodeCount = 0;
        var appendedSourceCount = 0;
        foreach (var row in sourceRows)
        {
            if (!targetEpisodesByNumber.TryGetValue(row.TargetEpisodeNumber, out var targetEpisode))
            {
                targetMetadataByEpisodeNumber.TryGetValue(row.TargetEpisodeNumber, out var metadata);
                targetEpisode = CreateTargetEpisode(targetSeason, row.TargetEpisodeNumber, metadata, now);
                dbContext.TvEpisodes.Add(targetEpisode);
                targetEpisodesByNumber[row.TargetEpisodeNumber] = targetEpisode;
                targetSourceCountsByEpisodeNumber[row.TargetEpisodeNumber] = 0;
                createdEpisodeCount++;
            }
            else if (targetMetadataByEpisodeNumber.TryGetValue(row.TargetEpisodeNumber, out var metadata))
            {
                ApplyEpisodeMetadata(targetEpisode, metadata, now);
            }

            var existingSourceCount = targetSourceCountsByEpisodeNumber.GetValueOrDefault(row.TargetEpisodeNumber);
            if (existingSourceCount > 0)
            {
                appendedSourceCount++;
            }

            var previousEpisodeId = row.MediaFile.EpisodeId;
            MoveSourceToTargetEpisode(row.MediaFile, targetEpisode, now);
            targetDefaultMediaFileIdsByEpisodeNumber[row.TargetEpisodeNumber] = row.MediaFile.Id;
            if (previousEpisodeId.HasValue && !sourceEpisodeIds.Contains(previousEpisodeId.Value))
            {
                sourceEpisodeIds.Add(previousEpisodeId.Value);
            }
            targetSourceCountsByEpisodeNumber[row.TargetEpisodeNumber] = existingSourceCount + 1;
        }

        var sourceEpisodeIdsNeedingFallback = await ClearMovedDefaultSourcesAsync(
            dbContext,
            sourceEpisodeIds,
            targetEpisodesByNumber.Values.Select(x => x.Id).Where(x => x > 0),
            movedMediaFileIds,
            now,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var oldDefaultFallback = await ReconcileEpisodeDefaultsAfterSourceMoveAsync(
            dbContext,
            sourceEpisodeIdsNeedingFallback,
            movedMediaFileIds,
            now,
            cancellationToken);

        foreach (var (targetEpisodeNumber, defaultMediaFileId) in targetDefaultMediaFileIdsByEpisodeNumber)
        {
            if (!targetEpisodesByNumber.TryGetValue(targetEpisodeNumber, out var targetEpisode))
            {
                continue;
            }

            targetEpisode.DefaultMediaFileId = defaultMediaFileId;
            targetEpisode.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new MoveSourcesResult(createdEpisodeCount, appendedSourceCount, oldDefaultFallback);
    }

    private static string BuildMappingSummary(IReadOnlyCollection<SourceMoveRow> sourceRows)
    {
        if (sourceRows.Count == 0)
        {
            return "none";
        }

        var mappings = sourceRows
            .GroupBy(x => new { x.OriginalEpisodeNumber, x.TargetEpisodeNumber })
            .OrderBy(x => x.Key.OriginalEpisodeNumber)
            .ThenBy(x => x.Key.TargetEpisodeNumber)
            .Take(20)
            .Select(x => $"E{x.Key.OriginalEpisodeNumber}->E{x.Key.TargetEpisodeNumber}x{x.Count()}");
        var suffix = sourceRows
            .GroupBy(x => new { x.OriginalEpisodeNumber, x.TargetEpisodeNumber })
            .Count() > 20
                ? ",..."
                : string.Empty;
        return string.Join(",", mappings) + suffix;
    }

    private static bool IsUnknownSeason(TvSeason season)
    {
        return season.Series?.TmdbSeriesId is null
               && season.TmdbSeasonId is null
               && season.IdentificationStatus == IdentificationStatus.Failed;
    }

    private static string ResolveSeasonKind(TvSeason season)
    {
        return IsUnknownSeason(season) ? "unknown" : "recognized";
    }

    private static bool IsActiveVideo(MediaFile mediaFile)
    {
        return mediaFile.MediaType == MediaType.Video && !mediaFile.IsDeleted;
    }

    private static async Task<TvSeries> UpsertTargetSeriesAsync(
        AppDbContext dbContext,
        TmdbTvSeriesDetailResult details,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var series = await dbContext.TvSeries
            .FirstOrDefaultAsync(x => x.TmdbSeriesId == details.TmdbId, cancellationToken);
        if (series is null)
        {
            series = new TvSeries
            {
                TmdbSeriesId = details.TmdbId,
                CreatedAt = now
            };
            dbContext.TvSeries.Add(series);
        }

        series.Name = TruncateRequired(FirstNonEmpty(details.Name, $"TV {details.TmdbId}"), 300);
        series.OriginalName = Truncate(FirstNonEmpty(details.OriginalName), 300);
        series.Overview = Truncate(details.Overview, 5000);
        series.PosterRemoteUrl = EmptyToNull(details.PosterRemoteUrl);
        series.Country = Truncate(string.Join(", ", details.OriginCountries), 120);
        series.Language = Truncate(details.OriginalLanguage, 120);
        series.FirstAirDate = ParseDate(details.FirstAirDate);
        series.FirstAirYear = details.FirstAirYear;
        series.GenresText = Truncate(details.GenresText, 1000);
        series.ProductionStatus = Truncate(details.ProductionStatus, 120);
        series.NetworksText = Truncate(details.NetworksText, 1000);
        series.ProductionCompaniesText = Truncate(details.ProductionCompaniesText, 1000);
        series.UpdatedAt = now;
        return series;
    }

    private static async Task<TvSeason> UpsertTargetSeasonAsync(
        AppDbContext dbContext,
        TvSeries series,
        TmdbTvSeriesDetailResult seriesDetails,
        int seasonNumber,
        TmdbTvSeasonDetailResult? detail,
        DateTime now,
        CancellationToken cancellationToken)
    {
        TvSeason? season = null;
        if (series.Id > 0)
        {
            season = await dbContext.TvSeasons
                .FirstOrDefaultAsync(
                    x => x.TvSeriesId == series.Id && x.SeasonNumber == seasonNumber,
                    cancellationToken);
        }

        if (season is null)
        {
            season = new TvSeason
            {
                Series = series,
                SeasonNumber = seasonNumber,
                CreatedAt = now
            };
            dbContext.TvSeasons.Add(season);
        }

        var summary = seriesDetails.Seasons.FirstOrDefault(x => x.SeasonNumber == seasonNumber);
        season.TmdbSeasonId = PositiveOrNull(detail?.TmdbId) ?? PositiveOrNull(summary?.TmdbId);
        season.Name = TruncateRequired(
            FirstNonEmpty(detail?.Name, summary?.Name, seasonNumber == 0 ? "Specials" : $"Season {seasonNumber}"),
            300);
        season.Overview = Truncate(FirstNonEmpty(detail?.Overview, summary?.Overview), 5000);
        season.PosterRemoteUrl = EmptyToNull(FirstNonEmpty(detail?.PosterRemoteUrl, summary?.PosterRemoteUrl));
        season.AirDate = ParseDate(FirstNonEmpty(detail?.AirDate, summary?.AirDate));
        season.TmdbEpisodeCount = detail?.EpisodeCount > 0
            ? detail.EpisodeCount
            : summary?.EpisodeCount;
        season.IdentifiedConfidence = 1d;
        season.IdentificationStatus = IdentificationStatus.ManualConfirmed;
        season.UpdatedAt = now;
        return season;
    }

    private static TvEpisode CreateTargetEpisode(
        TvSeason targetSeason,
        int episodeNumber,
        TmdbTvEpisodeMetadataItem? metadata,
        DateTime now)
    {
        var episode = new TvEpisode
        {
            Season = targetSeason,
            EpisodeNumber = episodeNumber,
            Title = $"E{episodeNumber:00}",
            CreatedAt = now,
            UpdatedAt = now
        };
        if (metadata is not null)
        {
            ApplyEpisodeMetadata(episode, metadata, now);
        }

        return episode;
    }

    private static void ApplyEpisodeMetadata(
        TvEpisode episode,
        TmdbTvEpisodeMetadataItem metadata,
        DateTime now)
    {
        episode.TmdbEpisodeId = PositiveOrNull(metadata.TmdbId);
        episode.Title = TruncateRequired(FirstNonEmpty(metadata.Name, episode.Title, $"E{metadata.EpisodeNumber:00}"), 300);
        episode.Overview = Truncate(metadata.Overview, 5000);
        episode.StillRemoteUrl = EmptyToNull(metadata.StillRemoteUrl);
        episode.AirDate = ParseDate(metadata.AirDate);
        episode.RuntimeMinutes = metadata.RuntimeMinutes;
        episode.UpdatedAt = now;
    }

    private static void MoveSourceToTargetEpisode(MediaFile mediaFile, TvEpisode targetEpisode, DateTime now)
    {
        mediaFile.MovieId = null;
        mediaFile.Movie = null;
        mediaFile.EpisodeId = targetEpisode.Id == 0 ? null : targetEpisode.Id;
        mediaFile.Episode = targetEpisode;
        mediaFile.UpdatedAt = now;
        targetEpisode.UpdatedAt = now;
    }

    private static async Task<IReadOnlyCollection<int>> ClearMovedDefaultSourcesAsync(
        AppDbContext dbContext,
        IEnumerable<int> sourceEpisodeIds,
        IEnumerable<int> targetEpisodeIds,
        IReadOnlySet<int> movedMediaFileIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sourceEpisodeIdSet = sourceEpisodeIds
            .Where(x => x > 0)
            .ToHashSet();
        var affectedEpisodeIds = sourceEpisodeIdSet
            .Concat(targetEpisodeIds.Where(x => x > 0))
            .Distinct()
            .ToArray();
        if (affectedEpisodeIds.Length == 0 || movedMediaFileIds.Count == 0)
        {
            return [];
        }

        var episodes = await dbContext.TvEpisodes
            .Where(x => affectedEpisodeIds.Contains(x.Id)
                        && x.DefaultMediaFileId.HasValue
                        && movedMediaFileIds.Contains(x.DefaultMediaFileId.Value))
            .ToListAsync(cancellationToken);
        var sourceEpisodeIdsNeedingFallback = new HashSet<int>();
        foreach (var episode in episodes)
        {
            if (sourceEpisodeIdSet.Contains(episode.Id))
            {
                sourceEpisodeIdsNeedingFallback.Add(episode.Id);
            }

            episode.DefaultMediaFileId = null;
            episode.UpdatedAt = now;
        }

        return sourceEpisodeIdsNeedingFallback;
    }

    private static async Task<bool> ReconcileEpisodeDefaultsAfterSourceMoveAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> episodeIds,
        IReadOnlySet<int> movedMediaFileIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var episodeId in episodeIds.Where(x => x > 0).Distinct())
        {
            var episode = await dbContext.TvEpisodes
                .FirstOrDefaultAsync(x => x.Id == episodeId, cancellationToken);
            if (episode is null)
            {
                continue;
            }

            var remainingSources = await dbContext.MediaFiles
                .Include(x => x.SourceConnection)
                .Where(x => x.EpisodeId == episodeId
                            && !movedMediaFileIds.Contains(x.Id)
                            && x.MediaType == MediaType.Video
                            && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            var fallbackMediaFileId = SelectPreferredDefaultMediaFileId(remainingSources, movedMediaFileIds);
            if (episode.DefaultMediaFileId == fallbackMediaFileId)
            {
                continue;
            }

            episode.DefaultMediaFileId = fallbackMediaFileId;
            episode.UpdatedAt = now;
            changed = true;
        }

        return changed;
    }

    private static int? SelectPreferredDefaultMediaFileId(
        IEnumerable<MediaFile> mediaFiles,
        int excludedMediaFileId)
    {
        var candidates = mediaFiles
            .Where(x => x.Id != excludedMediaFileId && x.MediaType == MediaType.Video && !x.IsDeleted)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .Where(IsPlayableLocalVideo)
            .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? candidates
                .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
    }

    private static int? SelectPreferredDefaultMediaFileId(
        IEnumerable<MediaFile> mediaFiles,
        IReadOnlySet<int> excludedMediaFileIds)
    {
        var candidates = mediaFiles
            .Where(x => !excludedMediaFileIds.Contains(x.Id) && x.MediaType == MediaType.Video && !x.IsDeleted)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .Where(IsPlayableLocalVideo)
            .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? candidates
                .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();
    }

    private static bool IsPlayableLocalVideo(MediaFile mediaFile)
    {
        return mediaFile.SourceConnection?.ProtocolType == ProtocolType.Local
               && IsExistingLocalFile(mediaFile.FilePath);
    }

    private static bool IsExistingLocalFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            return File.Exists(filePath);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> HideSourceSeasonIfEmptyAsync(
        AppDbContext dbContext,
        TvSeason sourceSeason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!IsUnknownSeason(sourceSeason))
        {
            return false;
        }

        return await HideSourceSeasonAsync(dbContext, sourceSeason, now, cancellationToken);
    }

    private static async Task<bool> HideSourceSeasonAsync(
        AppDbContext dbContext,
        TvSeason sourceSeason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var collectionItem = await dbContext.UserTvSeasonCollectionItems
            .FirstOrDefaultAsync(x => x.TvSeasonId == sourceSeason.Id, cancellationToken);
        if (collectionItem is null)
        {
            collectionItem = new UserTvSeasonCollectionItem
            {
                TvSeasonId = sourceSeason.Id,
                TvSeriesId = sourceSeason.TvSeriesId,
                TmdbSeriesId = sourceSeason.Series?.TmdbSeriesId,
                TmdbSeasonId = sourceSeason.TmdbSeasonId,
                SeasonNumber = sourceSeason.SeasonNumber,
                SeriesTitle = sourceSeason.Series?.Name ?? string.Empty,
                OriginalSeriesTitle = sourceSeason.Series?.OriginalName ?? string.Empty,
                SeasonTitle = sourceSeason.Name,
                FirstAirYear = sourceSeason.Series?.FirstAirYear,
                AirDate = sourceSeason.AirDate,
                PosterRemoteUrl = sourceSeason.PosterRemoteUrl ?? sourceSeason.Series?.PosterRemoteUrl ?? string.Empty,
                Overview = sourceSeason.Overview ?? sourceSeason.Series?.Overview ?? string.Empty,
                GenresText = sourceSeason.Series?.GenresText ?? string.Empty,
                Country = sourceSeason.Series?.Country ?? string.Empty,
                Language = sourceSeason.Series?.Language ?? string.Empty,
                CreatedAt = now
            };
            dbContext.UserTvSeasonCollectionItems.Add(collectionItem);
        }

        collectionItem.LibraryVisibilityState = LibraryVisibilityState.Hidden;
        collectionItem.UpdatedAt = now;
        return true;
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

    private static string DescribeException(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message)
                && (messages.Count == 0 || !string.Equals(messages[^1], current.Message, StringComparison.Ordinal)))
            {
                messages.Add(current.Message.Trim());
            }
        }

        return string.Join(" | ", messages);
    }

    private sealed record SourceMoveRow(int OriginalEpisodeNumber, int TargetEpisodeNumber, MediaFile MediaFile);

    private sealed record MoveSourcesResult(int CreatedEpisodeCount, int AppendedSourceCount, bool OldDefaultFallback);
}
