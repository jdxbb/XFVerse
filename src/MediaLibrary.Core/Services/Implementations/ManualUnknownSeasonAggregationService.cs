using System.Security.Cryptography;
using System.Text;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class ManualUnknownSeasonAggregationService : IManualUnknownSeasonAggregationService
{
    public async Task<ManualUnknownSeasonAggregationPrepareResult> PrepareAsync(
        IReadOnlyCollection<ManualUnknownSeasonAggregationSelection> selections,
        CancellationToken cancellationToken = default)
    {
        if (selections.Count == 0)
        {
            return Invalid("请先选择要聚合的未识别播放源。");
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var orderedMediaFiles = new List<MediaFile>();
        var seenMediaFileIds = new HashSet<int>();
        var invalidReasons = new List<string>();
        var index = 0;

        foreach (var selection in selections)
        {
            index++;
            var mediaFiles = await ExpandSelectionAsync(dbContext, selection, cancellationToken);
            if (mediaFiles.InvalidReason is not null)
            {
                invalidReasons.Add(mediaFiles.InvalidReason);
                continue;
            }

            if (mediaFiles.Items.Count == 0)
            {
                invalidReasons.Add($"第 {index} 个选中项没有可聚合播放源。");
                continue;
            }

            foreach (var mediaFile in mediaFiles.Items)
            {
                if (seenMediaFileIds.Add(mediaFile.Id))
                {
                    orderedMediaFiles.Add(mediaFile);
                }
            }
        }

        if (invalidReasons.Count > 0)
        {
            return Invalid(invalidReasons[0]);
        }

        if (orderedMediaFiles.Count == 0)
        {
            return Invalid("没有可聚合的未识别播放源。");
        }

        var sourceRows = new List<ManualUnknownSeasonAggregationSourceItem>();
        for (var rowIndex = 0; rowIndex < orderedMediaFiles.Count; rowIndex++)
        {
            var mediaFile = orderedMediaFiles[rowIndex];
            var parsedEpisodeNumber = TryParseEpisodeNumber(mediaFile.FileName);
            sourceRows.Add(
                new ManualUnknownSeasonAggregationSourceItem
                {
                    MediaFileId = mediaFile.Id,
                    SortIndex = rowIndex + 1,
                    FileName = SafeFileName(mediaFile),
                    SourceSummary = BuildSourceSummary(mediaFile),
                    CurrentBindingText = DescribeCurrentBinding(mediaFile),
                    SuggestedEpisodeNumber = parsedEpisodeNumber ?? rowIndex + 1,
                    EpisodeNumberParsedFromFileName = parsedEpisodeNumber.HasValue
                });
        }

        var title = GuessSeriesTitle(orderedMediaFiles);
        ScanIdentificationDiagnostics.Write(
            $"event=manual-unknown-season-aggregation-prepare sourceCount={sourceRows.Count} suggestedSeries={ScanIdentificationDiagnostics.FormatValue(title, 80)}");
        return new ManualUnknownSeasonAggregationPrepareResult
        {
            IsValid = true,
            Message = $"已展开 {sourceRows.Count} 个可聚合播放源。",
            SuggestedSeriesTitle = title,
            SuggestedSeasonTitle = GuessSeasonTitle(orderedMediaFiles),
            Sources = sourceRows
        };
    }

    public async Task<ManualUnknownSeasonAggregationApplyResult> ApplyAsync(
        ManualUnknownSeasonAggregationApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var seriesTitle = NormalizeRequiredTitle(request.SeriesTitle, "未识别电视剧");
        var seasonTitle = NormalizeRequiredTitle(request.SeasonTitle, "未识别季");
        var seasonNumber = request.SeasonNumber;
        var sourceAssignments = request.Sources
            .Where(x => x.MediaFileId > 0)
            .GroupBy(x => x.MediaFileId)
            .Select(x => x.First())
            .ToArray();
        if (sourceAssignments.Length == 0)
        {
            throw new InvalidOperationException("没有可聚合的播放源。");
        }

        if (sourceAssignments.Any(x => x.EpisodeNumber <= 0))
        {
            throw new InvalidOperationException("集号必须是正整数。");
        }

        if (seasonNumber <= 0)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=manual-season-aggregate-season-number-invalid seasonNumber={seasonNumber} sourceCount={sourceAssignments.Length}");
            throw new InvalidOperationException("季号必须是正整数。");
        }

        var normalizedSeriesTitle = NormalizeSeriesTitleForDuplicate(seriesTitle);
        var normalizedSeriesTitleHash = ShortHash(normalizedSeriesTitle);
        ScanIdentificationDiagnostics.Write(
            $"event=manual-season-aggregate-apply-started sourceCount={sourceAssignments.Length} episodeCount={sourceAssignments.Select(x => x.EpisodeNumber).Distinct().Count()} seasonNumber={seasonNumber} normalizedSeriesTitleHash={ScanIdentificationDiagnostics.FormatValue(normalizedSeriesTitleHash)}");

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var duplicateSeries = await FindDuplicateSeriesAsync(dbContext, normalizedSeriesTitle, cancellationToken);
        if (duplicateSeries is not null)
        {
            var kind = duplicateSeries.IsRecognized ? "recognized" : "unknown";
            ScanIdentificationDiagnostics.Write(
                $"event=manual-season-aggregate-duplicate-series-blocked normalizedSeriesTitleHash={ScanIdentificationDiagnostics.FormatValue(normalizedSeriesTitleHash)} existingSeriesId={duplicateSeries.Id} existingSeriesKind={ScanIdentificationDiagnostics.FormatValue(kind)} seasonNumber={seasonNumber} sourceCount={sourceAssignments.Length}");
            throw new InvalidOperationException("已存在同名剧集。人工聚合用于创建新的未识别季；如果要加入已有剧/季，请使用修正功能。");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        try
        {
            var mediaFileIds = sourceAssignments.Select(x => x.MediaFileId).ToArray();
            var mediaFiles = await dbContext.MediaFiles
                .Include(x => x.SourceConnection)
                .Include(x => x.Movie)
                .Include(x => x.Episode)
                .ThenInclude(x => x!.Season)
                .ThenInclude(x => x!.Series)
                .Where(x => mediaFileIds.Contains(x.Id)
                            && x.MediaType == MediaType.Video
                            && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            if (mediaFiles.Count != mediaFileIds.Length)
            {
                throw new InvalidOperationException("部分播放源不存在或不可用。");
            }

            if (mediaFiles.Any(x => !IsEligibleUnidentifiedSource(x)))
            {
                throw new InvalidOperationException("人工聚合只允许未识别播放源。");
            }

            var mediaFileIndex = mediaFiles.ToDictionary(x => x.Id);
            var tvSeries = new TvSeries
            {
                TmdbSeriesId = null,
                Name = TruncateRequired(seriesTitle, 300),
                Overview = "人工聚合生成的未识别电视剧容器。",
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.TvSeries.Add(tvSeries);

            var tvSeason = new TvSeason
            {
                Series = tvSeries,
                SeasonNumber = seasonNumber,
                TmdbSeasonId = null,
                Name = TruncateRequired(seasonTitle, 300),
                Overview = "人工聚合生成的未识别季；仅包含用户选择的播放源。",
                IdentificationStatus = IdentificationStatus.Failed,
                IdentifiedConfidence = null,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.TvSeasons.Add(tvSeason);

            var episodeByNumber = new Dictionary<int, TvEpisode>();
            var previousMovieIds = new HashSet<int>();
            var previousEpisodeIds = new HashSet<int>();
            var movedMediaFileIds = new HashSet<int>();
            var additionalSourceCount = 0;

            foreach (var assignment in sourceAssignments)
            {
                var mediaFile = mediaFileIndex[assignment.MediaFileId];
                if (mediaFile.MovieId.HasValue)
                {
                    previousMovieIds.Add(mediaFile.MovieId.Value);
                }

                if (mediaFile.EpisodeId.HasValue)
                {
                    previousEpisodeIds.Add(mediaFile.EpisodeId.Value);
                }

                if (!episodeByNumber.TryGetValue(assignment.EpisodeNumber, out var episode))
                {
                    episode = new TvEpisode
                    {
                        Season = tvSeason,
                        EpisodeNumber = assignment.EpisodeNumber,
                        Title = $"E{assignment.EpisodeNumber:00}",
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    dbContext.TvEpisodes.Add(episode);
                    episodeByNumber.Add(assignment.EpisodeNumber, episode);
                }
                else
                {
                    additionalSourceCount++;
                }

                mediaFile.MovieId = null;
                mediaFile.Movie = null;
                mediaFile.Episode = episode;
                mediaFile.EpisodeId = null;
                mediaFile.UpdatedAt = now;
                episode.DefaultMediaFileId ??= mediaFile.Id;
                episode.UpdatedAt = now;
                movedMediaFileIds.Add(mediaFile.Id);
            }

            tvSeason.TmdbEpisodeCount = episodeByNumber.Count;
            await ReconcileMovieDefaultsAfterSourceMoveAsync(dbContext, previousMovieIds, movedMediaFileIds, now, cancellationToken);
            await ReconcileEpisodeDefaultsAfterSourceMoveAsync(dbContext, previousEpisodeIds, movedMediaFileIds, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await CleanupFailedMoviePlaceholdersAsync(dbContext, previousMovieIds, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            ScanIdentificationDiagnostics.Write(
                $"event=manual-season-aggregate-apply-succeeded sourceCount={sourceAssignments.Length} createdEpisodeCount={episodeByNumber.Count} additionalSourceCount={additionalSourceCount} seasonNumber={seasonNumber} normalizedSeriesTitleHash={ScanIdentificationDiagnostics.FormatValue(normalizedSeriesTitleHash)} targetSeriesId={tvSeries.Id} targetSeasonId={tvSeason.Id}");
            return new ManualUnknownSeasonAggregationApplyResult
            {
                SeriesId = tvSeries.Id,
                SeasonId = tvSeason.Id,
                SourceCount = sourceAssignments.Length,
                CreatedEpisodeCount = episodeByNumber.Count,
                AdditionalSourceCount = additionalSourceCount
            };
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=manual-season-aggregate-apply-failed sourceCount={sourceAssignments.Length} seasonNumber={seasonNumber} normalizedSeriesTitleHash={ScanIdentificationDiagnostics.FormatValue(normalizedSeriesTitleHash)} failureReason={ScanIdentificationDiagnostics.FormatValue(DescribeException(exception), 260)}");
            throw;
        }
    }

    private static async Task<(IReadOnlyList<MediaFile> Items, string? InvalidReason)> ExpandSelectionAsync(
        AppDbContext dbContext,
        ManualUnknownSeasonAggregationSelection selection,
        CancellationToken cancellationToken)
    {
        if (selection.OrphanMediaFileId > 0)
        {
            var orphan = await LoadMediaFilesAsync(dbContext, [selection.OrphanMediaFileId], cancellationToken);
            return orphan.Count == 1 && orphan.All(IsEligibleOrphanSource)
                ? (orphan, null)
                : ([], "选中项包含不可聚合的 orphan 播放源。");
        }

        if (selection.GroupedRangeMediaFileIds.Count > 0)
        {
            var ids = selection.GroupedRangeMediaFileIds.Where(x => x > 0).Distinct().ToArray();
            var files = await LoadMediaFilesAsync(dbContext, ids, cancellationToken);
            return files.Count > 0 && files.All(IsEligibleUnidentifiedSource)
                ? (files, null)
                : ([], "选中项包含不可聚合的 grouped 未识别源。");
        }

        if (selection.SeasonId > 0)
        {
            var season = await dbContext.TvSeasons
                .AsNoTracking()
                .Include(x => x.Series)
                .FirstOrDefaultAsync(x => x.Id == selection.SeasonId, cancellationToken);
            if (season is null || !IsUnknownSeason(season))
            {
                return ([], "选中项包含已识别电视剧季。");
            }

            var episodeIds = await dbContext.TvEpisodes
                .AsNoTracking()
                .Where(x => x.TvSeasonId == selection.SeasonId)
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken);
            var files = await dbContext.MediaFiles
                .AsNoTracking()
                .Include(x => x.SourceConnection)
                .Include(x => x.Episode)
                .ThenInclude(x => x!.Season)
                .ThenInclude(x => x!.Series)
                .Where(x => x.EpisodeId.HasValue
                            && episodeIds.Contains(x.EpisodeId.Value)
                            && x.MediaType == MediaType.Video
                            && !x.IsDeleted)
                .OrderBy(x => x.Episode!.EpisodeNumber)
                .ThenBy(x => x.FileName)
                .ToListAsync(cancellationToken);
            return files.Count > 0 && files.All(IsEligibleUnidentifiedSource)
                ? (files, null)
                : ([], "选中的未识别季没有可聚合播放源。");
        }

        if (selection.MovieId > 0)
        {
            var movie = await dbContext.Movies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == selection.MovieId, cancellationToken);
            if (movie is null || !IsFailedMoviePlaceholder(movie))
            {
                return ([], "选中项包含已识别电影。");
            }

            var files = await dbContext.MediaFiles
                .AsNoTracking()
                .Include(x => x.SourceConnection)
                .Include(x => x.Movie)
                .Where(x => x.MovieId == selection.MovieId
                            && x.MediaType == MediaType.Video
                            && !x.IsDeleted)
                .OrderBy(x => x.FileName)
                .ToListAsync(cancellationToken);
            return files.Count > 0 && files.All(IsEligibleUnidentifiedSource)
                ? (files, null)
                : ([], "选中的未识别电影占位没有可聚合播放源。");
        }

        return ([], "选中项不可用于人工聚合为季。");
    }

    private static async Task<List<MediaFile>> LoadMediaFilesAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        if (mediaFileIds.Count == 0)
        {
            return [];
        }

        return await dbContext.MediaFiles
            .AsNoTracking()
            .Include(x => x.SourceConnection)
            .Include(x => x.Movie)
            .Include(x => x.Episode)
            .ThenInclude(x => x!.Season)
            .ThenInclude(x => x!.Series)
            .Where(x => mediaFileIds.Contains(x.Id)
                        && x.MediaType == MediaType.Video
                        && !x.IsDeleted)
            .OrderBy(x => x.FileName)
            .ToListAsync(cancellationToken);
    }

    private static bool IsEligibleUnidentifiedSource(MediaFile mediaFile)
    {
        if (mediaFile.MediaType != MediaType.Video || mediaFile.IsDeleted)
        {
            return false;
        }

        if (mediaFile.Movie is not null)
        {
            return IsFailedMoviePlaceholder(mediaFile.Movie);
        }

        if (mediaFile.Episode is not null)
        {
            return IsUnknownSeason(mediaFile.Episode.Season);
        }

        return mediaFile.MovieId is null && mediaFile.EpisodeId is null;
    }

    private static bool IsEligibleOrphanSource(MediaFile mediaFile)
    {
        return mediaFile.MediaType == MediaType.Video
               && !mediaFile.IsDeleted
               && mediaFile.MovieId is null
               && mediaFile.EpisodeId is null;
    }

    private static bool IsFailedMoviePlaceholder(Movie movie)
    {
        return movie.TmdbId is null && movie.IdentificationStatus == IdentificationStatus.Failed;
    }

    private static bool IsUnknownSeason(TvSeason? season)
    {
        return season is not null
               && season.Series?.TmdbSeriesId is null
               && season?.TmdbSeasonId is null
               && season?.IdentificationStatus == IdentificationStatus.Failed;
    }

    private static int? TryParseEpisodeNumber(string fileName)
    {
        var parsed = TvEpisodeFileNameParser.Parse(fileName);
        return parsed.IsEpisodeLike && !parsed.IsMultiEpisode && parsed.EpisodeNumber > 0
            ? parsed.EpisodeNumber
            : null;
    }

    private static string BuildSourceSummary(MediaFile mediaFile)
    {
        var sourceKind = mediaFile.SourceConnection?.ProtocolType switch
        {
            ProtocolType.Local => "本地",
            ProtocolType.WebDav => "WebDAV",
            _ => "未知来源"
        };
        var context = BuildDirectoryContextHash(mediaFile);
        return string.IsNullOrWhiteSpace(context) ? sourceKind : $"{sourceKind} · {context}";
    }

    private static string BuildDirectoryContextHash(MediaFile mediaFile)
    {
        var path = !string.IsNullOrWhiteSpace(mediaFile.RemoteUri) ? mediaFile.RemoteUri : mediaFile.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var directory = Path.GetDirectoryName(path.Trim()) ?? path.Trim();
            return $"ctx:{ShortHash(directory)}";
        }
        catch
        {
            return $"ctx:{ShortHash(path.Trim())}";
        }
    }

    private static string DescribeCurrentBinding(MediaFile mediaFile)
    {
        if (mediaFile.Movie is not null)
        {
            return $"未识别电影 · {mediaFile.Movie.Title}";
        }

        if (mediaFile.Episode?.Season is not null)
        {
            var seriesTitle = mediaFile.Episode.Season.Series?.Name ?? "未识别电视剧";
            var seasonTitle = string.IsNullOrWhiteSpace(mediaFile.Episode.Season.Name)
                ? $"Season {mediaFile.Episode.Season.SeasonNumber}"
                : mediaFile.Episode.Season.Name;
            return $"{seriesTitle} · {seasonTitle} · E{mediaFile.Episode.EpisodeNumber:00}";
        }

        return "orphan 未识别源";
    }

    private static string GuessSeriesTitle(IReadOnlyList<MediaFile> mediaFiles)
    {
        var boundSeriesTitle = mediaFiles
            .Select(x => x.Episode?.Season?.Series?.Name)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!string.IsNullOrWhiteSpace(boundSeriesTitle))
        {
            return boundSeriesTitle.Trim();
        }

        var boundMovieTitle = mediaFiles
            .Select(x => x.Movie?.Title)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!string.IsNullOrWhiteSpace(boundMovieTitle))
        {
            return boundMovieTitle.Trim();
        }

        var directoryName = mediaFiles
            .Select(GetSafeDirectoryName)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return string.IsNullOrWhiteSpace(directoryName) ? "未识别电视剧" : directoryName;
    }

    private static string GuessSeasonTitle(IReadOnlyList<MediaFile> mediaFiles)
    {
        var seasonTitle = mediaFiles
            .Select(x => x.Episode?.Season?.Name)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return string.IsNullOrWhiteSpace(seasonTitle) ? "未识别季" : seasonTitle.Trim();
    }

    private static string? GetSafeDirectoryName(MediaFile mediaFile)
    {
        var path = !string.IsNullOrWhiteSpace(mediaFile.RemoteUri) ? mediaFile.RemoteUri : mediaFile.FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var directory = Path.GetDirectoryName(path.Trim());
            return string.IsNullOrWhiteSpace(directory) ? null : Path.GetFileName(directory);
        }
        catch
        {
            return null;
        }
    }

    private static string SafeFileName(MediaFile mediaFile)
    {
        return string.IsNullOrWhiteSpace(mediaFile.FileName)
            ? "-"
            : Path.GetFileName(mediaFile.FileName.Trim());
    }

    private static async Task ReconcileMovieDefaultsAfterSourceMoveAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> movieIds,
        IReadOnlyCollection<int> movedMediaFileIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var movieId in movieIds)
        {
            var movie = await dbContext.Movies.FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
            if (movie is null || !movie.DefaultMediaFileId.HasValue || !movedMediaFileIds.Contains(movie.DefaultMediaFileId.Value))
            {
                continue;
            }

            var remainingSources = await dbContext.MediaFiles
                .Include(x => x.SourceConnection)
                .Where(x => x.MovieId == movieId
                            && !movedMediaFileIds.Contains(x.Id)
                            && x.MediaType == MediaType.Video
                            && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            movie.DefaultMediaFileId = SelectPreferredDefaultMediaFileId(remainingSources, movedMediaFileIds);
            movie.UpdatedAt = now;
        }
    }

    private static async Task ReconcileEpisodeDefaultsAfterSourceMoveAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> episodeIds,
        IReadOnlyCollection<int> movedMediaFileIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var episodeId in episodeIds)
        {
            var episode = await dbContext.TvEpisodes.FirstOrDefaultAsync(x => x.Id == episodeId, cancellationToken);
            if (episode is null || !episode.DefaultMediaFileId.HasValue || !movedMediaFileIds.Contains(episode.DefaultMediaFileId.Value))
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
            episode.DefaultMediaFileId = SelectPreferredDefaultMediaFileId(remainingSources, movedMediaFileIds);
            episode.UpdatedAt = now;
        }
    }

    private static int? SelectPreferredDefaultMediaFileId(
        IEnumerable<MediaFile> mediaFiles,
        IReadOnlyCollection<int> excludedMediaFileIds)
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

    private static async Task CleanupFailedMoviePlaceholdersAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> movieIds,
        CancellationToken cancellationToken)
    {
        foreach (var movieId in movieIds)
        {
            var movie = await dbContext.Movies
                .Include(x => x.MediaFiles)
                .Include(x => x.RatingSources)
                .Include(x => x.WatchHistories)
                .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
            if (movie is null || !IsFailedMoviePlaceholder(movie))
            {
                continue;
            }

            var hasActiveSource = await dbContext.MediaFiles
                .AnyAsync(
                    x => x.MovieId == movieId
                         && x.MediaType == MediaType.Video
                         && !x.IsDeleted,
                    cancellationToken);
            if (hasActiveSource || movie.WatchHistories.Count > 0 || movie.IsFavorite || movie.IsWatched)
            {
                continue;
            }

            if (movie.RatingSources.Count > 0)
            {
                dbContext.RatingSources.RemoveRange(movie.RatingSources);
            }

            dbContext.Movies.Remove(movie);
        }
    }

    private static async Task<ExistingSeriesMatch?> FindDuplicateSeriesAsync(
        AppDbContext dbContext,
        string normalizedSeriesTitle,
        CancellationToken cancellationToken)
    {
        var existingSeries = await dbContext.TvSeries
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.TmdbSeriesId
            })
            .ToListAsync(cancellationToken);

        var match = existingSeries.FirstOrDefault(x =>
            string.Equals(
                NormalizeSeriesTitleForDuplicate(x.Name),
                normalizedSeriesTitle,
                StringComparison.Ordinal));
        return match is null
            ? null
            : new ExistingSeriesMatch(match.Id, match.TmdbSeriesId.HasValue);
    }

    private static string NormalizeRequiredTitle(string value, string fallback)
    {
        var title = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return string.IsNullOrWhiteSpace(title) ? fallback : title;
    }

    private static string NormalizeSeriesTitleForDuplicate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = TrimCommonWrappingSymbols(value.Normalize(NormalizationForm.FormKC).Trim());
        var builder = new StringBuilder(normalized.Length);
        var previousWasWhitespace = false;
        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            previousWasWhitespace = false;
            builder.Append(ch);
        }

        return TrimCommonWrappingSymbols(builder.ToString().Trim()).ToLowerInvariant();
    }

    private static string TrimCommonWrappingSymbols(string value)
    {
        var trimmed = value.Trim().Trim('"', '\'', '`', '“', '”', '‘', '’');
        var changed = true;
        while (changed && trimmed.Length >= 2)
        {
            changed = false;
            var first = trimmed[0];
            var last = trimmed[^1];
            if (IsWrappingSymbolPair(first, last))
            {
                trimmed = trimmed[1..^1].Trim().Trim('"', '\'', '`', '“', '”', '‘', '’');
                changed = true;
            }
        }

        return trimmed;
    }

    private static bool IsWrappingSymbolPair(char first, char last)
    {
        return (first, last) switch
        {
            ('(', ')') => true,
            ('[', ']') => true,
            ('{', '}') => true,
            ('<', '>') => true,
            ('《', '》') => true,
            ('【', '】') => true,
            ('「', '」') => true,
            ('『', '』') => true,
            ('“', '”') => true,
            ('‘', '’') => true,
            _ => false
        };
    }

    private static string TruncateRequired(string value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
    }

    private static ManualUnknownSeasonAggregationPrepareResult Invalid(string message)
    {
        return new ManualUnknownSeasonAggregationPrepareResult
        {
            IsValid = false,
            Message = message
        };
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

    private sealed record ExistingSeriesMatch(int Id, bool IsRecognized);
}
