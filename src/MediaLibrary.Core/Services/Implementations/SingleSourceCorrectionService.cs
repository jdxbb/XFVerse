using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
