using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class SingleSourceCorrectionService : ISingleSourceCorrectionService
{
    private readonly ITmdbService _tmdbService;
    private readonly IMovieIdentificationService _movieIdentificationService;
    private readonly ITvSeasonIdentificationService _tvSeasonIdentificationService;

    public SingleSourceCorrectionService(
        ITmdbService tmdbService,
        IMovieIdentificationService movieIdentificationService,
        ITvSeasonIdentificationService tvSeasonIdentificationService)
    {
        _tmdbService = tmdbService;
        _movieIdentificationService = movieIdentificationService;
        _tvSeasonIdentificationService = tvSeasonIdentificationService;
    }

    public async Task<SingleSourceCorrectionPreview> PreviewMovieCorrectionAsync(
        int mediaFileId,
        int tmdbMovieId,
        CancellationToken cancellationToken = default)
    {
        if (mediaFileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaFileId));
        }

        if (tmdbMovieId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tmdbMovieId));
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFile = await LoadCorrectionSourceAsync(dbContext, mediaFileId, cancellationToken)
            ?? throw new InvalidOperationException("待修正的播放源不存在。");
        var targetMovie = await _tmdbService.GetMovieDetailsAsync(tmdbMovieId, cancellationToken)
            ?? throw new InvalidOperationException("无法读取 TMDB 影片详情。");

        var existingMovie = await dbContext.Movies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TmdbId == targetMovie.TmdbId, cancellationToken);
        var targetActiveSourceCount = existingMovie is null
            ? 0
            : await dbContext.MediaFiles
                .AsNoTracking()
                .CountAsync(
                    x => x.MovieId == existingMovie.Id
                         && x.Id != mediaFile.Id
                         && x.MediaType == MediaType.Video
                         && !x.IsDeleted,
                    cancellationToken);

        var preview = BuildMoviePreview(mediaFile, targetMovie, existingMovie is null, targetActiveSourceCount > 0);
        LogPreview(preview, mediaFile.SourceConnection?.ProtocolType.ToString() ?? "unknown", targetMovie.TmdbId, null);
        return preview;
    }

    public async Task<SingleSourceCorrectionApplyResult> ApplyMovieCorrectionAsync(
        int mediaFileId,
        int tmdbMovieId,
        CancellationToken cancellationToken = default)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=correction-apply-started mediaFileId={mediaFileId} targetKind=movie tmdbId={tmdbMovieId}");

        try
        {
            var targetMovieId = await _movieIdentificationService.ApplyManualMediaFileMatchAsync(
                mediaFileId,
                tmdbMovieId,
                cancellationToken);

            ScanIdentificationDiagnostics.Write(
                $"event=correction-apply-succeeded mediaFileId={mediaFileId} targetKind=movie targetMovieId={targetMovieId}");
            return new SingleSourceCorrectionApplyResult
            {
                MediaFileId = mediaFileId,
                TargetKind = SingleSourceCorrectionTargetKind.Movie,
                TargetMovieId = targetMovieId,
                Message = "播放源已修正为电影。"
            };
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=correction-apply-failed mediaFileId={mediaFileId} targetKind=movie error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
            throw;
        }
    }

    public async Task<SingleSourceCorrectionPreview> PreviewTvEpisodeCorrectionAsync(
        int mediaFileId,
        int seriesTmdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        if (mediaFileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaFileId));
        }

        if (seriesTmdbId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seriesTmdbId));
        }

        if (seasonNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonNumber));
        }

        if (episodeNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(episodeNumber));
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var mediaFile = await LoadCorrectionSourceAsync(dbContext, mediaFileId, cancellationToken)
            ?? throw new InvalidOperationException("待修正的播放源不存在。");
        var seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(seriesTmdbId, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("无法读取 TMDB 电视剧详情。");
        var seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(seriesTmdbId, seasonNumber, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("无法读取 TMDB 电视剧季详情。");
        var episodeMetadata = seasonDetails.Episodes.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);

        var existingEpisode = await dbContext.TvEpisodes
            .AsNoTracking()
            .Where(x => x.EpisodeNumber == episodeNumber
                        && x.Season != null
                        && x.Season.SeasonNumber == seasonNumber
                        && x.Season.Series != null
                        && x.Season.Series.TmdbSeriesId == seriesTmdbId)
            .Select(x => new
            {
                x.Id,
                ActiveSourceCount = x.MediaFiles.Count(
                    mediaFileItem => mediaFileItem.Id != mediaFile.Id
                                     && mediaFileItem.MediaType == MediaType.Video
                                     && !mediaFileItem.IsDeleted)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var preview = BuildTvEpisodePreview(
            mediaFile,
            seriesDetails,
            seasonDetails,
            episodeMetadata,
            seasonNumber,
            episodeNumber,
            existingEpisode is null,
            existingEpisode?.ActiveSourceCount > 0);
        LogPreview(preview, mediaFile.SourceConnection?.ProtocolType.ToString() ?? "unknown", null, seriesTmdbId);
        return preview;
    }

    public async Task<SingleSourceCorrectionApplyResult> ApplyTvEpisodeCorrectionAsync(
        int mediaFileId,
        int seriesTmdbId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=correction-apply-started mediaFileId={mediaFileId} targetKind=tv-episode seriesTmdbId={seriesTmdbId} season={seasonNumber} episode={episodeNumber}");

        try
        {
            var targetEpisodeId = await _tvSeasonIdentificationService.ApplyManualMediaFileMatchAsync(
                mediaFileId,
                seriesTmdbId,
                seasonNumber,
                episodeNumber,
                cancellationToken);

            ScanIdentificationDiagnostics.Write(
                $"event=correction-apply-succeeded mediaFileId={mediaFileId} targetKind=tv-episode targetEpisodeId={targetEpisodeId} seriesTmdbId={seriesTmdbId} season={seasonNumber} episode={episodeNumber}");
            return new SingleSourceCorrectionApplyResult
            {
                MediaFileId = mediaFileId,
                TargetKind = SingleSourceCorrectionTargetKind.TvEpisode,
                TargetEpisodeId = targetEpisodeId,
                Message = "播放源已修正为电视剧集。"
            };
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=correction-apply-failed mediaFileId={mediaFileId} targetKind=tv-episode seriesTmdbId={seriesTmdbId} season={seasonNumber} episode={episodeNumber} error={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
            throw;
        }
    }

    public async Task<IReadOnlyList<UnknownTvSeasonCorrectionTargetItem>> SearchUnknownSeasonTargetsAsync(
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var queryable = dbContext.TvSeasons
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Series)
            .Include(x => x.Episodes)
            .ThenInclude(x => x.MediaFiles)
            .ThenInclude(x => x.SourceConnection)
            .Where(x => x.Series != null
                        && x.Series.TmdbSeriesId == null
                        && x.TmdbSeasonId == null
                        && x.Episodes.Any(episode => episode.MediaFiles.Any(mediaFile =>
                            mediaFile.MediaType == MediaType.Video
                            && !mediaFile.IsDeleted)));

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            queryable = queryable.Where(x =>
                x.Name.Contains(normalizedQuery)
                || (x.Series != null && x.Series.Name.Contains(normalizedQuery)));
        }

        var seasons = await queryable
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Take(150)
            .ToListAsync(cancellationToken);

        return seasons
            .Select(BuildUnknownSeasonTargetItem)
            .Where(x => x.SourceCount > 0)
            .ToList();
    }

    public async Task<SingleSourceCorrectionApplyResult> ApplyUnknownSeasonEpisodeCorrectionAsync(
        int mediaFileId,
        int targetSeasonId,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        if (mediaFileId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaFileId));
        }

        if (targetSeasonId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetSeasonId));
        }

        if (episodeNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(episodeNumber));
        }

        ScanIdentificationDiagnostics.Write(
            $"event=correction-target-unknown-season-started mediaFileId={mediaFileId} targetSeasonId={targetSeasonId} inputEpisodeNumber={episodeNumber}");

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var mediaFile = await dbContext.MediaFiles
                .Include(x => x.SourceConnection)
                .Include(x => x.Movie)
                .Include(x => x.Episode)
                .FirstOrDefaultAsync(
                    x => x.Id == mediaFileId
                         && x.MediaType == MediaType.Video
                         && !x.IsDeleted,
                    cancellationToken)
                ?? throw new InvalidOperationException("待修正的播放源不存在。");

            var targetSeason = await dbContext.TvSeasons
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                .ThenInclude(x => x.MediaFiles)
                .ThenInclude(x => x.SourceConnection)
                .FirstOrDefaultAsync(x => x.Id == targetSeasonId, cancellationToken)
                ?? throw new InvalidOperationException("目标未识别季不存在。");

            if (!IsUnknownSeason(targetSeason))
            {
                throw new InvalidOperationException("只能加入 no-TMDB / 未识别电视剧季。");
            }

            var previousMovieId = mediaFile.MovieId;
            var previousEpisodeId = mediaFile.EpisodeId;
            var targetEpisode = targetSeason.Episodes.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);
            var createdEpisode = targetEpisode is null;
            if (targetEpisode is null)
            {
                targetEpisode = new TvEpisode
                {
                    TvSeasonId = targetSeason.Id,
                    Season = targetSeason,
                    EpisodeNumber = episodeNumber,
                    Title = $"E{episodeNumber:00}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.TvEpisodes.Add(targetEpisode);
            }

            var existingSourceCount = targetEpisode.MediaFiles.Count(x =>
                x.Id != mediaFile.Id
                && x.MediaType == MediaType.Video
                && !x.IsDeleted);
            var overwrittenTargetDefaultSource = targetEpisode.DefaultMediaFileId.HasValue
                                                 && targetEpisode.DefaultMediaFileId.Value != mediaFile.Id;

            mediaFile.MovieId = null;
            mediaFile.Movie = null;
            mediaFile.EpisodeId = targetEpisode.Id == 0 ? null : targetEpisode.Id;
            mediaFile.Episode = targetEpisode;
            mediaFile.UpdatedAt = DateTime.UtcNow;
            targetEpisode.DefaultMediaFileId = mediaFile.Id;
            targetEpisode.UpdatedAt = DateTime.UtcNow;
            targetSeason.UpdatedAt = DateTime.UtcNow;
            if (targetSeason.Series is not null)
            {
                targetSeason.Series.UpdatedAt = DateTime.UtcNow;
            }

            var oldDefaultFallback = false;
            if (previousMovieId.HasValue)
            {
                oldDefaultFallback |= await ReconcileMovieAfterSourceMoveAsync(
                    dbContext,
                    previousMovieId.Value,
                    mediaFile.Id,
                    cancellationToken);
                await CleanupMovieIfOrphanedAsync(dbContext, previousMovieId.Value, cancellationToken);
            }

            if (previousEpisodeId.HasValue && (!targetEpisode.Id.Equals(previousEpisodeId.Value)))
            {
                oldDefaultFallback |= await ReconcileEpisodeAfterSourceMoveAsync(
                    dbContext,
                    previousEpisodeId.Value,
                    mediaFile.Id,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            ScanIdentificationDiagnostics.Write(
                $"event=correction-target-unknown-season-succeeded mediaFileId={mediaFileId} oldMovieId={ScanIdentificationDiagnostics.FormatNullable(previousMovieId)} oldEpisodeId={ScanIdentificationDiagnostics.FormatNullable(previousEpisodeId)} targetSeasonId={targetSeason.Id} targetEpisodeId={targetEpisode.Id} inputEpisodeNumber={episodeNumber} createdEpisode={createdEpisode.ToString().ToLowerInvariant()} appendedAsAdditionalSource={(existingSourceCount > 0).ToString().ToLowerInvariant()} overwrittenTargetDefaultSource={overwrittenTargetDefaultSource.ToString().ToLowerInvariant()} oldDefaultFallback={oldDefaultFallback.ToString().ToLowerInvariant()}");

            return new SingleSourceCorrectionApplyResult
            {
                MediaFileId = mediaFileId,
                TargetKind = SingleSourceCorrectionTargetKind.UnknownSeasonEpisode,
                TargetSeasonId = targetSeason.Id,
                TargetEpisodeId = targetEpisode.Id,
                CreatedEpisode = createdEpisode,
                AppendedAsAdditionalSource = existingSourceCount > 0,
                OverwrittenTargetDefaultSource = overwrittenTargetDefaultSource,
                OldDefaultFallback = oldDefaultFallback,
                Message = "播放源已加入已有未识别季。"
            };
        }
        catch (Exception exception)
        {
            ScanIdentificationDiagnostics.Write(
                $"event=correction-target-unknown-season-failed mediaFileId={mediaFileId} targetSeasonId={targetSeasonId} inputEpisodeNumber={episodeNumber} reason={ScanIdentificationDiagnostics.FormatValue(TrimMessage(exception.Message), 220)}");
            throw;
        }
    }

    private static async Task<MediaFile?> LoadCorrectionSourceAsync(
        AppDbContext dbContext,
        int mediaFileId,
        CancellationToken cancellationToken)
    {
        return await dbContext.MediaFiles
            .AsNoTracking()
            .Include(x => x.SourceConnection)
            .Include(x => x.Movie)
            .Include(x => x.Episode)
            .ThenInclude(x => x!.Season)
            .ThenInclude(x => x!.Series)
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken);
    }

    private static UnknownTvSeasonCorrectionTargetItem BuildUnknownSeasonTargetItem(TvSeason season)
    {
        var activeSources = season.Episodes
            .SelectMany(x => x.MediaFiles)
            .Where(IsActiveVideo)
            .ToList();
        var episodeNumbers = season.Episodes
            .Where(episode => episode.MediaFiles.Any(IsActiveVideo))
            .Select(x => x.EpisodeNumber)
            .Distinct()
            .Order()
            .ToArray();

        return new UnknownTvSeasonCorrectionTargetItem
        {
            SeasonId = season.Id,
            SeriesTitle = string.IsNullOrWhiteSpace(season.Series?.Name) ? "-" : season.Series.Name.Trim(),
            SeasonTitle = string.IsNullOrWhiteSpace(season.Name) ? $"Season {season.SeasonNumber}" : season.Name.Trim(),
            SeasonNumber = season.SeasonNumber,
            EpisodeRangeText = FormatEpisodeRange(episodeNumbers),
            SourceCount = activeSources.Count,
            SourceKindSummary = FormatSourceKindSummary(activeSources),
            ContextHint = BuildContextHint(activeSources)
        };
    }

    private static bool IsUnknownSeason(TvSeason season)
    {
        return season.Series?.TmdbSeriesId is null
               && season.TmdbSeasonId is null;
    }

    private static bool IsActiveVideo(MediaFile mediaFile)
    {
        return mediaFile.MediaType == MediaType.Video && !mediaFile.IsDeleted;
    }

    private static string FormatEpisodeRange(IReadOnlyList<int> episodeNumbers)
    {
        if (episodeNumbers.Count == 0)
        {
            return "episodes:-";
        }

        if (episodeNumbers.Count == 1)
        {
            return $"E{episodeNumbers[0]:00}";
        }

        return $"E{episodeNumbers[0]:00}-E{episodeNumbers[^1]:00}";
    }

    private static string FormatSourceKindSummary(IReadOnlyCollection<MediaFile> mediaFiles)
    {
        var protocolGroups = mediaFiles
            .Select(x => x.SourceConnection?.ProtocolType.ToString().ToLowerInvariant() ?? "unknown")
            .GroupBy(x => x)
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key}:{x.Count()}")
            .ToArray();
        return protocolGroups.Length == 0 ? "source:unknown" : string.Join(" ", protocolGroups);
    }

    private static string BuildContextHint(IReadOnlyCollection<MediaFile> mediaFiles)
    {
        var directoryKeys = mediaFiles
            .Select(BuildDirectoryKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (directoryKeys.Length == 0)
        {
            return "ctx:-";
        }

        return directoryKeys.Length == 1
            ? $"ctx:{ShortHash(directoryKeys[0])}"
            : $"ctx:{ShortHash(string.Join("|", directoryKeys))}/multi:{directoryKeys.Length}";
    }

    private static string BuildDirectoryKey(MediaFile mediaFile)
    {
        var source = !string.IsNullOrWhiteSpace(mediaFile.RemoteUri)
            ? mediaFile.RemoteUri
            : mediaFile.FilePath;
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetDirectoryName(source.Trim()) ?? source.Trim();
        }
        catch
        {
            return source.Trim();
        }
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
    }

    private static async Task<bool> ReconcileMovieAfterSourceMoveAsync(
        AppDbContext dbContext,
        int movieId,
        int movedMediaFileId,
        CancellationToken cancellationToken)
    {
        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .ThenInclude(x => x.SourceConnection)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
        if (movie is null || movie.DefaultMediaFileId != movedMediaFileId)
        {
            return false;
        }

        movie.DefaultMediaFileId = SelectPreferredDefaultMediaFileId(movie.MediaFiles, movedMediaFileId);
        movie.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    private static async Task<bool> ReconcileEpisodeAfterSourceMoveAsync(
        AppDbContext dbContext,
        int episodeId,
        int movedMediaFileId,
        CancellationToken cancellationToken)
    {
        var episode = await dbContext.TvEpisodes
            .FirstOrDefaultAsync(x => x.Id == episodeId, cancellationToken);
        if (episode is null || episode.DefaultMediaFileId != movedMediaFileId)
        {
            return false;
        }

        var remainingSources = await dbContext.MediaFiles
            .Include(x => x.SourceConnection)
            .Where(x => x.EpisodeId == episodeId
                        && x.Id != movedMediaFileId
                        && x.MediaType == MediaType.Video
                        && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        episode.DefaultMediaFileId = SelectPreferredDefaultMediaFileId(remainingSources, movedMediaFileId);
        episode.UpdatedAt = DateTime.UtcNow;
        return true;
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
        if (movie is null)
        {
            return;
        }

        if (movie.MediaFiles.Count == 0
            && movie.WatchHistories.Count == 0
            && !movie.IsFavorite
            && !movie.IsWatched)
        {
            if (movie.RatingSources.Count > 0)
            {
                dbContext.RatingSources.RemoveRange(movie.RatingSources);
            }

            dbContext.Movies.Remove(movie);
        }
    }

    private static SingleSourceCorrectionPreview BuildMoviePreview(
        MediaFile mediaFile,
        MetadataSearchCandidate targetMovie,
        bool willCreateTargetContainer,
        bool willAppendAsAdditionalSource)
    {
        var current = DescribeCurrentBinding(mediaFile);
        var targetTitle = FormatMovieTargetTitle(targetMovie);
        return new SingleSourceCorrectionPreview
        {
            MediaFileId = mediaFile.Id,
            TargetKind = SingleSourceCorrectionTargetKind.Movie,
            IsValid = true,
            SourceFileName = SafeFileName(mediaFile),
            CurrentBindingKind = current.Kind,
            CurrentBindingTitle = current.Title,
            TargetTypeText = "电影",
            TargetTitle = targetTitle,
            WillClearMovieId = false,
            WillClearEpisodeId = mediaFile.EpisodeId.HasValue,
            WillAppendAsAdditionalSource = willAppendAsAdditionalSource,
            WillCreateTargetContainer = willCreateTargetContainer,
            Lines =
            [
                $"播放源：{SafeFileName(mediaFile)}",
                $"当前归属：{current.Kind} · {current.Title}",
                $"目标：电影 · {targetTitle}",
                mediaFile.EpisodeId.HasValue ? "将清除当前 EpisodeId，并绑定到目标 MovieId。" : "将绑定到目标 MovieId。",
                willAppendAsAdditionalSource ? "目标电影已有播放源，本次将作为多播放源追加。" : "目标电影暂无其它播放源，本次将作为播放源绑定。",
                "保留：媒体探测信息、字幕绑定、源记录。",
                "不会删除真实本地文件或 WebDAV 文件。",
                "跨类型用户状态不会迁移。"
            ]
        };
    }

    private static SingleSourceCorrectionPreview BuildTvEpisodePreview(
        MediaFile mediaFile,
        TmdbTvSeriesDetailResult seriesDetails,
        TmdbTvSeasonDetailResult seasonDetails,
        TmdbTvEpisodeMetadataItem? episodeMetadata,
        int seasonNumber,
        int episodeNumber,
        bool willCreateTargetContainer,
        bool willAppendAsAdditionalSource)
    {
        var current = DescribeCurrentBinding(mediaFile);
        var episodeTitle = string.IsNullOrWhiteSpace(episodeMetadata?.Name)
            ? $"第 {episodeNumber} 集"
            : episodeMetadata.Name.Trim();
        var seasonTitle = string.IsNullOrWhiteSpace(seasonDetails.Name)
            ? $"第 {seasonNumber} 季"
            : seasonDetails.Name.Trim();
        var targetTitle = $"{seriesDetails.Name} / {seasonTitle} / E{episodeNumber:00} {episodeTitle}";
        return new SingleSourceCorrectionPreview
        {
            MediaFileId = mediaFile.Id,
            TargetKind = SingleSourceCorrectionTargetKind.TvEpisode,
            IsValid = true,
            SourceFileName = SafeFileName(mediaFile),
            CurrentBindingKind = current.Kind,
            CurrentBindingTitle = current.Title,
            TargetTypeText = "电视剧集",
            TargetTitle = targetTitle,
            WillClearMovieId = mediaFile.MovieId.HasValue,
            WillClearEpisodeId = mediaFile.EpisodeId.HasValue,
            WillAppendAsAdditionalSource = willAppendAsAdditionalSource,
            WillCreateTargetContainer = willCreateTargetContainer,
            Lines =
            [
                $"播放源：{SafeFileName(mediaFile)}",
                $"当前归属：{current.Kind} · {current.Title}",
                $"目标：电视剧集 · {targetTitle}",
                mediaFile.MovieId.HasValue ? "将清除当前 MovieId，并绑定到目标 EpisodeId。" : "将绑定到目标 EpisodeId。",
                willAppendAsAdditionalSource ? "目标剧集已有播放源，本次将作为多播放源追加。" : "目标剧集暂无其它播放源，本次将作为播放源绑定。",
                "保留：媒体探测信息、字幕绑定、源记录。",
                "不会删除真实本地文件或 WebDAV 文件。",
                "跨类型用户状态不会迁移；TV 不进入 Watch Insights / AI 推荐。"
            ]
        };
    }

    private static (string Kind, string Title) DescribeCurrentBinding(MediaFile mediaFile)
    {
        if (mediaFile.Movie is not null)
        {
            return ("电影", string.IsNullOrWhiteSpace(mediaFile.Movie.Title) ? "-" : mediaFile.Movie.Title);
        }

        if (mediaFile.Episode is not null)
        {
            var seriesName = mediaFile.Episode.Season?.Series?.Name;
            var seasonName = mediaFile.Episode.Season?.Name;
            var episodeTitle = string.IsNullOrWhiteSpace(mediaFile.Episode.Title)
                ? $"第 {mediaFile.Episode.EpisodeNumber} 集"
                : mediaFile.Episode.Title.Trim();
            return ("电视剧集", string.Join(" / ", new[] { seriesName, seasonName, episodeTitle }
                .Where(x => !string.IsNullOrWhiteSpace(x))));
        }

        return ("未识别 / orphan", "-");
    }

    private static string FormatMovieTargetTitle(MetadataSearchCandidate movie)
    {
        var year = movie.ReleaseYear.HasValue ? $" ({movie.ReleaseYear.Value})" : string.Empty;
        return $"{movie.Title}{year}";
    }

    private static string SafeFileName(MediaFile mediaFile)
    {
        if (!string.IsNullOrWhiteSpace(mediaFile.FileName))
        {
            return Path.GetFileName(mediaFile.FileName.Trim());
        }

        return "-";
    }

    private static void LogPreview(
        SingleSourceCorrectionPreview preview,
        string sourceKind,
        int? tmdbMovieId,
        int? seriesTmdbId)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=correction-preview-created mediaFileId={preview.MediaFileId} sourceKind={ScanIdentificationDiagnostics.FormatValue(sourceKind)} targetKind={ScanIdentificationDiagnostics.FormatValue(preview.TargetKind.ToString())} tmdbMovieId={ScanIdentificationDiagnostics.FormatNullable(tmdbMovieId)} seriesTmdbId={ScanIdentificationDiagnostics.FormatNullable(seriesTmdbId)} currentBinding={ScanIdentificationDiagnostics.FormatValue(preview.CurrentBindingKind)} willClearMovieId={preview.WillClearMovieId.ToString().ToLowerInvariant()} willClearEpisodeId={preview.WillClearEpisodeId.ToString().ToLowerInvariant()} append={preview.WillAppendAsAdditionalSource.ToString().ToLowerInvariant()} createTarget={preview.WillCreateTargetContainer.ToString().ToLowerInvariant()} sourceFile={ScanIdentificationDiagnostics.FormatFileName(preview.SourceFileName)}");
    }

    private static string TrimMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Trim();
    }
}
