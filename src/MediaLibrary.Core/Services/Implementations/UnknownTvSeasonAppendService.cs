using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class UnknownTvSeasonAppendService : IUnknownTvSeasonAppendService
{
    public async Task<UnknownTvSeasonAppendResult> TryAppendScanPathsAsync(
        IReadOnlyCollection<int> scanPathIds,
        string sourceKind,
        CancellationToken cancellationToken = default)
    {
        var ids = scanPathIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            var empty = new UnknownTvSeasonAppendResult();
            WriteSummary(sourceKind, empty);
            return empty;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFileIds = await dbContext.MediaFiles
            .AsNoTracking()
            .Include(x => x.Movie)
            .Where(
                x => x.ScanPathId.HasValue
                     && ids.Contains(x.ScanPathId.Value)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.EpisodeId.HasValue
                     && (!x.MovieId.HasValue
                         || (x.Movie != null && x.Movie.IdentificationStatus == IdentificationStatus.Failed)))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        return await TryAppendAsync(mediaFileIds, sourceKind, cancellationToken);
    }

    public async Task<UnknownTvSeasonAppendResult> TryAppendAsync(
        IReadOnlyCollection<int> mediaFileIds,
        string sourceKind,
        CancellationToken cancellationToken = default)
    {
        var ids = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var result = new UnknownTvSeasonAppendResult();
        if (ids.Length == 0)
        {
            WriteSummary(sourceKind, result);
            return result;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFiles = await dbContext.MediaFiles
            .Include(x => x.Movie)
            .Include(x => x.SourceConnection)
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var hiddenMovieIds = await ScanCandidateVisibilityGuard.LoadHiddenMovieIdsAsync(
            dbContext,
            mediaFiles.Select(x => x.MovieId),
            cancellationToken);

        foreach (var mediaFile in mediaFiles)
        {
            if (!IsEligibleAppendCandidate(mediaFile, hiddenMovieIds, out var skippedReason))
            {
                WriteSkipped(sourceKind, mediaFile, UnknownTvGroupingContext.Empty, null, null, skippedReason, 0, 0);
                result.SkippedCount++;
                continue;
            }

            result.CandidateCount++;
            if (!UnknownTvGroupingKeyHelper.TryBuildContext(mediaFile, out var context, out skippedReason))
            {
                WriteSkipped(sourceKind, mediaFile, context, null, null, skippedReason, 0, 0);
                result.SkippedCount++;
                continue;
            }

            if (UnknownTvGroupingKeyHelper.HasSpecialDirectoryToken(context))
            {
                WriteCandidate(sourceKind, mediaFile, context, null);
                WriteSkipped(
                    sourceKind,
                    mediaFile,
                    context,
                    null,
                    null,
                    "special-directory-auto-append-disabled",
                    0,
                    0);
                result.SkippedCount++;
                continue;
            }

            var parseResult = TvEpisodeFileNameParser.Parse(
                mediaFile.FileName,
                allowSeasonContextOnly: true,
                seasonNumberHint: ResolveDirectorySeasonNumber(mediaFile.FilePath),
                allowStrongContextFallbacks: true);
            skippedReason = GetEpisodeCandidateSkipReason(mediaFile.FileName, parseResult);
            WriteCandidate(sourceKind, mediaFile, context, parseResult.EpisodeNumber > 0 ? parseResult.EpisodeNumber : null);
            if (!string.IsNullOrWhiteSpace(skippedReason))
            {
                WriteSkipped(
                    sourceKind,
                    mediaFile,
                    context,
                    parseResult.SeasonNumber >= 0 ? parseResult.SeasonNumber : null,
                    parseResult.EpisodeNumber > 0 ? parseResult.EpisodeNumber : null,
                    skippedReason,
                    0,
                    0);
                result.SkippedCount++;
                continue;
            }

            result.EpisodeCandidateCount++;
            var appendResult = await TryAppendToExistingSeasonAsync(
                dbContext,
                mediaFile,
                context,
                parseResult.EpisodeNumber,
                sourceKind,
                cancellationToken);
            if (appendResult.Succeeded)
            {
                result.SucceededCount++;
                result.AppendedSourceCount++;
                if (appendResult.CreatedEpisode)
                {
                    result.CreatedEpisodeCount++;
                }

                continue;
            }

            result.SkippedCount++;
        }

        WriteSummary(sourceKind, result);
        return result;
    }

    private static async Task<AppendAttemptResult> TryAppendToExistingSeasonAsync(
        AppDbContext dbContext,
        MediaFile mediaFile,
        UnknownTvGroupingContext context,
        int episodeNumber,
        string sourceKind,
        CancellationToken cancellationToken)
    {
        var candidateSeasons = await dbContext.TvSeasons
            .Include(x => x.Series)
            .Include(x => x.Episodes)
            .ThenInclude(x => x.MediaFiles)
            .Where(
                x => !x.TmdbSeasonId.HasValue
                     && x.IdentificationStatus == IdentificationStatus.Failed
                     && x.Series != null
                     && !x.Series.TmdbSeriesId.HasValue
                     && x.Episodes.Any(
                         episode => episode.MediaFiles.Any(
                             source => source.SourceConnectionId == context.SourceConnectionId
                                       && source.MediaType == MediaType.Video
                                       && !source.IsDeleted
                                       && source.ScanPathId == context.ScanPathId)))
            .ToListAsync(cancellationToken);

        var compatibleSeasons = new List<TvSeason>();
        var skippedReasons = new List<string>();
        foreach (var season in candidateSeasons)
        {
            if (UnknownTvGroupingKeyHelper.IsCompatibleSeason(season, context, [episodeNumber], out var skippedReason))
            {
                compatibleSeasons.Add(season);
            }
            else if (!string.IsNullOrWhiteSpace(skippedReason))
            {
                skippedReasons.Add(skippedReason);
            }
        }

        var existingSeriesCandidates = candidateSeasons.Select(x => x.TvSeriesId).Distinct().Count();
        if (compatibleSeasons.Count != 1)
        {
            WriteSkipped(
                sourceKind,
                mediaFile,
                context,
                null,
                episodeNumber,
                SelectAppendSkippedReason(skippedReasons, compatibleSeasons.Count),
                candidateSeasons.Count,
                existingSeriesCandidates);
            return AppendAttemptResult.Skipped;
        }

        var now = DateTime.UtcNow;
        var targetSeason = compatibleSeasons[0];
        var seriesNamePreserved = false;
        if (targetSeason.Series is not null && !string.IsNullOrWhiteSpace(context.SeriesDisplayTitle))
        {
            if (string.IsNullOrWhiteSpace(targetSeason.Series.Name))
            {
                targetSeason.Series.Name = TruncateRequired(context.SeriesDisplayTitle, 300);
            }
            else
            {
                seriesNamePreserved = true;
            }
        }

        var seasonNamePreserved = false;
        if (!string.IsNullOrWhiteSpace(context.SeasonDisplayTitle))
        {
            if (string.IsNullOrWhiteSpace(targetSeason.Name))
            {
                targetSeason.Name = TruncateRequired(context.SeasonDisplayTitle, 300);
            }
            else
            {
                seasonNamePreserved = true;
            }
        }

        var targetEpisode = targetSeason.Episodes.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);
        var createdEpisode = false;
        if (targetEpisode is null)
        {
            targetEpisode = new TvEpisode
            {
                TvSeasonId = targetSeason.Id,
                EpisodeNumber = episodeNumber,
                Title = TruncateRequired(UnknownTvGroupingKeyHelper.BuildEpisodeTitle(mediaFile.FileName, episodeNumber), 300),
                CreatedAt = now,
                UpdatedAt = now,
                DefaultMediaFileId = mediaFile.Id
            };
            dbContext.TvEpisodes.Add(targetEpisode);
            targetSeason.Episodes.Add(targetEpisode);
            createdEpisode = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!targetEpisode.DefaultMediaFileId.HasValue)
        {
            targetEpisode.DefaultMediaFileId = mediaFile.Id;
        }

        var oldMovieId = mediaFile.MovieId;
        var defaultOwners = await dbContext.Movies
            .Where(x => x.DefaultMediaFileId == mediaFile.Id)
            .ToListAsync(cancellationToken);
        foreach (var owner in defaultOwners)
        {
            owner.DefaultMediaFileId = null;
            owner.UpdatedAt = now;
        }

        mediaFile.MovieId = null;
        mediaFile.Movie = null;
        mediaFile.EpisodeId = targetEpisode.Id;
        mediaFile.Episode = targetEpisode;
        mediaFile.UpdatedAt = now;

        targetEpisode.UpdatedAt = now;
        targetSeason.TmdbEpisodeCount = Math.Max(
            targetSeason.TmdbEpisodeCount.GetValueOrDefault(),
            targetSeason.Episodes.Select(x => x.EpisodeNumber).Distinct().Count());
        targetSeason.UpdatedAt = now;
        if (targetSeason.Series is not null)
        {
            targetSeason.Series.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (oldMovieId.HasValue)
        {
            await CleanupMovieIfOrphanedAsync(dbContext, oldMovieId.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        ScanIdentificationDiagnostics.Write(
            $"event=unknown-season-append-succeeded sourceKind={FormatValue(sourceKind)} sourceConnectionId={context.SourceConnectionId} scanPathId={FormatNullable(context.ScanPathId)} parentDirectoryHash={FormatValue(context.ParentDirectoryHash)} normalizedTitle={FormatValue(context.NormalizedSeriesTitle)} groupingKey={FormatValue(context.SeasonGroupingKeyHash)} episodeNumber={episodeNumber} targetSeriesId={targetSeason.TvSeriesId} targetSeasonId={targetSeason.Id} targetEpisodeId={targetEpisode.Id} createdEpisodeCount={(createdEpisode ? 1 : 0)} appendedSourceCount=1 namePreserved={FormatBool(seriesNamePreserved || seasonNamePreserved)}");
        return new AppendAttemptResult(true, createdEpisode);
    }

    private static string SelectAppendSkippedReason(IReadOnlyCollection<string> skippedReasons, int compatibleSeasonCount)
    {
        if (compatibleSeasonCount > 1)
        {
            return "multiple-compatible-unknown-seasons";
        }

        if (skippedReasons.Contains("ambiguous-existing-unknown-season-context", StringComparer.OrdinalIgnoreCase))
        {
            return "ambiguous-existing-unknown-season-context";
        }

        if (skippedReasons.Contains("ambiguous-existing-unknown-series-context", StringComparer.OrdinalIgnoreCase))
        {
            return "ambiguous-existing-unknown-series-context";
        }

        if (skippedReasons.Contains("strict-season-key-mismatch", StringComparer.OrdinalIgnoreCase))
        {
            return "strict-season-key-mismatch";
        }

        if (skippedReasons.Contains("strict-series-key-mismatch", StringComparer.OrdinalIgnoreCase))
        {
            return "strict-series-key-mismatch";
        }

        if (skippedReasons.Contains("no-existing-unknown-season-context", StringComparer.OrdinalIgnoreCase))
        {
            return "no-existing-unknown-season-context";
        }

        if (skippedReasons.Contains("no-existing-unknown-series-context", StringComparer.OrdinalIgnoreCase))
        {
            return "no-existing-unknown-series-context";
        }

        return "no-compatible-unknown-season";
    }

    private static bool IsEligibleAppendCandidate(
        MediaFile mediaFile,
        IReadOnlySet<int> hiddenMovieIds,
        out string skippedReason)
    {
        if (mediaFile.MediaType != MediaType.Video)
        {
            skippedReason = "not-video";
            return false;
        }

        if (mediaFile.IsDeleted)
        {
            skippedReason = "deleted-source";
            return false;
        }

        if (mediaFile.EpisodeId.HasValue)
        {
            skippedReason = "already-bound-episode";
            return false;
        }

        if (ScanCandidateVisibilityGuard.IsHiddenFailedMoviePlaceholder(mediaFile, hiddenMovieIds))
        {
            skippedReason = ScanCandidateVisibilityGuard.HiddenFailedPlaceholderSkipReason;
            return false;
        }

        if (mediaFile.MovieId.HasValue
            && mediaFile.Movie?.IdentificationStatus != IdentificationStatus.Failed)
        {
            skippedReason = "already-bound-stable-or-pending-movie";
            return false;
        }

        skippedReason = string.Empty;
        return true;
    }

    private static string GetEpisodeCandidateSkipReason(string fileName, TvEpisodeFileNameParseResult parseResult)
    {
        if (UnknownTvGroupingKeyHelper.ContainsSpecialEpisodeToken(fileName))
        {
            return "special-episode-token";
        }

        if (UnknownTvGroupingKeyHelper.ContainsStructuralNonEpisodeToken(fileName))
        {
            return "structural-non-episode-token";
        }

        if (parseResult.IsMultiEpisode)
        {
            return "multi-episode";
        }

        if (!parseResult.IsEpisodeLike || parseResult.EpisodeNumber <= 0)
        {
            return "episode-number-not-parsed";
        }

        if (parseResult.PartHintDetected)
        {
            return "part-hint-deferred";
        }

        return string.Empty;
    }

    private static int? ResolveDirectorySeasonNumber(string filePath)
    {
        var directory = UnknownTvGroupingKeyHelper.GetDirectoryPath(filePath);
        var parent = Path.GetFileName(directory.Replace('\\', '/').TrimEnd('/'));
        var seasonNumber = TvEpisodeFileNameParser.TryParseSeasonNumber(parent);
        if (seasonNumber.HasValue)
        {
            return seasonNumber.Value;
        }

        var grandParentPath = UnknownTvGroupingKeyHelper.GetDirectoryPath(directory);
        var grandParent = Path.GetFileName(grandParentPath.Replace('\\', '/').TrimEnd('/'));
        return TvEpisodeFileNameParser.TryParseSeasonNumber(grandParent);
    }

    private static async Task CleanupMovieIfOrphanedAsync(
        AppDbContext dbContext,
        int movieId,
        CancellationToken cancellationToken)
    {
        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Include(x => x.RatingSources)
            .Include(x => x.WatchHistories)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
        if (movie is null
            || movie.MediaFiles.Count > 0
            || movie.WatchHistories.Count > 0
            || movie.IsFavorite
            || movie.IsWatched)
        {
            return;
        }

        if (movie.RatingSources.Count > 0)
        {
            dbContext.RatingSources.RemoveRange(movie.RatingSources);
        }

        dbContext.Movies.Remove(movie);
    }

    private static void WriteCandidate(
        string sourceKind,
        MediaFile mediaFile,
        UnknownTvGroupingContext context,
        int? episodeNumber)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-season-append-candidate sourceKind={FormatValue(sourceKind)} sourceConnectionId={context.SourceConnectionId} scanPathId={FormatNullable(context.ScanPathId)} mediaFileId={mediaFile.Id} parentDirectoryHash={FormatValue(context.ParentDirectoryHash)} normalizedTitle={FormatValue(context.NormalizedSeriesTitle)} groupingKey={FormatValue(context.SeasonGroupingKeyHash)} episodeNumber={FormatNullable(episodeNumber)}");
    }

    private static void WriteSkipped(
        string sourceKind,
        MediaFile mediaFile,
        UnknownTvGroupingContext context,
        int? seasonNumber,
        int? episodeNumber,
        string skippedReason,
        int existingSeasonCandidates,
        int existingSeriesCandidates)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-season-append-skipped sourceKind={FormatValue(sourceKind)} sourceConnectionId={FormatNullable(context.SourceConnectionId > 0 ? context.SourceConnectionId : null)} scanPathId={FormatNullable(context.ScanPathId)} mediaFileId={mediaFile.Id} parentDirectoryHash={FormatValue(context.ParentDirectoryHash)} normalizedTitle={FormatValue(context.NormalizedSeriesTitle)} groupingKey={FormatValue(context.SeasonGroupingKeyHash)} seasonNumber={FormatNullable(seasonNumber)} episodeNumber={FormatNullable(episodeNumber)} existingSeriesCandidates={existingSeriesCandidates} existingSeasonCandidates={existingSeasonCandidates} skippedReason={FormatValue(skippedReason)}");
    }

    private static void WriteSummary(string sourceKind, UnknownTvSeasonAppendResult result)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=unknown-season-append-summary sourceKind={FormatValue(sourceKind)} candidateCount={result.CandidateCount} episodeCandidateCount={result.EpisodeCandidateCount} succeeded={result.SucceededCount} skipped={result.SkippedCount} createdEpisodeCount={result.CreatedEpisodeCount} appendedSourceCount={result.AppendedSourceCount}");
    }

    private static string TruncateRequired(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string FormatNullable(int? value)
    {
        return ScanIdentificationDiagnostics.FormatNullable(value);
    }

    private static string FormatValue(string? value)
    {
        return ScanIdentificationDiagnostics.FormatValue(value ?? string.Empty);
    }

    private static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    private sealed record AppendAttemptResult(bool Succeeded, bool CreatedEpisode)
    {
        public static AppendAttemptResult Skipped { get; } = new(false, false);
    }
}
