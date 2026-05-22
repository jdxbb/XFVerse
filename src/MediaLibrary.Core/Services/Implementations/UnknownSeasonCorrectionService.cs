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

    public async Task<UnknownSeasonCorrectionApplyResult> ApplyUnknownSeasonToRecognizedSeasonAsync(
        int sourceSeasonId,
        int targetSeriesTmdbId,
        int targetSeasonNumber,
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

        if (targetSeasonNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSeasonNumber));
        }

        ScanIdentificationDiagnostics.Write(
            $"event=season-correction-apply-started sourceSeasonId={sourceSeasonId} targetSeriesTmdbId={targetSeriesTmdbId} targetSeasonNumber={targetSeasonNumber}");

        try
        {
            var seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(
                    targetSeriesTmdbId,
                    cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Unable to load target TV series details.");
            var seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(
                    targetSeriesTmdbId,
                    targetSeasonNumber,
                    cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Unable to load target TV season details.");

            await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var sourceSeason = await dbContext.TvSeasons
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                .ThenInclude(x => x.MediaFiles)
                .ThenInclude(x => x.SourceConnection)
                .FirstOrDefaultAsync(x => x.Id == sourceSeasonId, cancellationToken)
                ?? throw new InvalidOperationException("Source unknown season does not exist.");

            if (!IsUnknownSeason(sourceSeason))
            {
                throw new InvalidOperationException("Only no-TMDB unknown seasons can be corrected to recognized seasons.");
            }

            var sourceRows = sourceSeason.Episodes
                .OrderBy(x => x.EpisodeNumber)
                .ThenBy(x => x.Id)
                .SelectMany(episode => episode.MediaFiles
                    .Where(IsActiveVideo)
                    .OrderBy(mediaFile => mediaFile.Id)
                    .Select(mediaFile => new SourceMoveRow(episode.EpisodeNumber, mediaFile)))
                .ToList();
            if (sourceRows.Count == 0)
            {
                throw new InvalidOperationException("Source unknown season has no playable sources to correct.");
            }

            var targetSeries = await UpsertTargetSeriesAsync(dbContext, seriesDetails, now, cancellationToken);
            var targetSeason = await UpsertTargetSeasonAsync(
                dbContext,
                targetSeries,
                seasonDetails,
                now,
                cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

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
            var targetMetadataByEpisodeNumber = seasonDetails.Episodes
                .Where(x => x.EpisodeNumber > 0)
                .GroupBy(x => x.EpisodeNumber)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.TmdbId).First());

            var createdEpisodeCount = 0;
            var appendedSourceCount = 0;
            foreach (var row in sourceRows)
            {
                if (!targetEpisodesByNumber.TryGetValue(row.EpisodeNumber, out var targetEpisode))
                {
                    targetMetadataByEpisodeNumber.TryGetValue(row.EpisodeNumber, out var metadata);
                    targetEpisode = CreateTargetEpisode(targetSeason, row.EpisodeNumber, metadata, now);
                    dbContext.TvEpisodes.Add(targetEpisode);
                    targetEpisodesByNumber[row.EpisodeNumber] = targetEpisode;
                    targetSourceCountsByEpisodeNumber[row.EpisodeNumber] = 0;
                    createdEpisodeCount++;
                }
                else if (targetMetadataByEpisodeNumber.TryGetValue(row.EpisodeNumber, out var metadata))
                {
                    ApplyEpisodeMetadata(targetEpisode, metadata, now);
                }

                var existingSourceCount = targetSourceCountsByEpisodeNumber.GetValueOrDefault(row.EpisodeNumber);
                if (existingSourceCount > 0)
                {
                    appendedSourceCount++;
                }

                MoveSourceToTargetEpisode(row.MediaFile, targetEpisode, now);
                targetSourceCountsByEpisodeNumber[row.EpisodeNumber] = existingSourceCount + 1;
            }

            sourceSeason.UpdatedAt = now;
            if (sourceSeason.Series is not null)
            {
                sourceSeason.Series.UpdatedAt = now;
            }

            var oldContainerHidden = await HideSourceSeasonAsync(
                dbContext,
                sourceSeason,
                now,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-apply-succeeded sourceSeasonId={sourceSeasonId} targetSeriesId={targetSeries.Id} targetSeasonId={targetSeason.Id} movedSourceCount={sourceRows.Count} createdEpisodeCount={createdEpisodeCount} appendedSourceCount={appendedSourceCount} oldContainerCleaned={oldContainerHidden.ToString().ToLowerInvariant()}");

            return new UnknownSeasonCorrectionApplyResult
            {
                SourceSeasonId = sourceSeasonId,
                TargetSeriesId = targetSeries.Id,
                TargetSeasonId = targetSeason.Id,
                MovedSourceCount = sourceRows.Count,
                CreatedEpisodeCount = createdEpisodeCount,
                AppendedSourceCount = appendedSourceCount,
                OldContainerHidden = oldContainerHidden
            };
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=season-correction-apply-failed sourceSeasonId={sourceSeasonId} targetSeriesTmdbId={targetSeriesTmdbId} targetSeasonNumber={targetSeasonNumber} failureReason={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 260)}");
            throw;
        }
    }

    private static bool IsUnknownSeason(TvSeason season)
    {
        return season.Series?.TmdbSeriesId is null
               && season.TmdbSeasonId is null
               && season.IdentificationStatus == IdentificationStatus.Failed;
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
        series.UpdatedAt = now;
        return series;
    }

    private static async Task<TvSeason> UpsertTargetSeasonAsync(
        AppDbContext dbContext,
        TvSeries series,
        TmdbTvSeasonDetailResult detail,
        DateTime now,
        CancellationToken cancellationToken)
    {
        TvSeason? season = null;
        if (series.Id > 0)
        {
            season = await dbContext.TvSeasons
                .FirstOrDefaultAsync(
                    x => x.TvSeriesId == series.Id && x.SeasonNumber == detail.SeasonNumber,
                    cancellationToken);
        }

        if (season is null)
        {
            season = new TvSeason
            {
                Series = series,
                SeasonNumber = detail.SeasonNumber,
                CreatedAt = now
            };
            dbContext.TvSeasons.Add(season);
        }

        season.TmdbSeasonId = PositiveOrNull(detail.TmdbId);
        season.Name = TruncateRequired(
            FirstNonEmpty(detail.Name, detail.SeasonNumber == 0 ? "Specials" : $"Season {detail.SeasonNumber}"),
            300);
        season.Overview = Truncate(detail.Overview, 5000);
        season.PosterRemoteUrl = EmptyToNull(detail.PosterRemoteUrl);
        season.AirDate = ParseDate(detail.AirDate);
        season.TmdbEpisodeCount = detail.EpisodeCount;
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
        targetEpisode.DefaultMediaFileId = mediaFile.Id;
        targetEpisode.UpdatedAt = now;
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

    private sealed record SourceMoveRow(int EpisodeNumber, MediaFile MediaFile);
}
