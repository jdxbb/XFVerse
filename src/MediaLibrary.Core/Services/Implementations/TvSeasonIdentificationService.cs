using System.Globalization;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class TvSeasonIdentificationService : ITvSeasonIdentificationService
{
    private const double MinimumAutoMatchConfidence = 0.55d;
    private const double MatchedConfidence = 0.80d;
    private const string UnidentifiedSeasonTitle = "未识别电视剧季";

    private readonly ISettingsService _settingsService;
    private readonly ITmdbService _tmdbService;

    public TvSeasonIdentificationService(
        ISettingsService settingsService,
        ITmdbService tmdbService)
    {
        _settingsService = settingsService;
        _tmdbService = tmdbService;
    }

    public async Task<TvSeasonIdentificationRunResult> IdentifyMediaFilesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken = default)
    {
        var result = new TvSeasonIdentificationRunResult();
        var distinctIds = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (distinctIds.Length == 0)
        {
            return result;
        }

        var candidates = await BuildCandidatesAsync(distinctIds, cancellationToken);
        if (candidates.Count == 0)
        {
            return result;
        }

        var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
        var hasTmdbCredential = !string.IsNullOrWhiteSpace(settings.TmdbReadAccessToken)
                                || !string.IsNullOrWhiteSpace(settings.TmdbApiKey);

        foreach (var candidate in candidates)
        {
            result.Summary.AttemptedCount++;
            result.AddHandledMediaFiles(candidate.Files.Select(x => x.MediaFileId));
            result.AddHandledMediaFiles(candidate.UnsupportedFiles.Select(x => x.MediaFileId));

            if (candidate.UnsupportedFiles.Count > 0)
            {
                result.Summary.AddWarning("TV.Parse", "发现多集文件，本阶段不支持多集拆分，已跳过对应播放源。");
            }

            if (candidate.Files.Count == 0)
            {
                continue;
            }

            if (!hasTmdbCredential || string.IsNullOrWhiteSpace(candidate.CandidateName))
            {
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                if (!hasTmdbCredential)
                {
                    result.Summary.AddWarning("TMDB.Auth", "TMDB 认证未配置，电视剧季已保留为未识别。");
                }

                continue;
            }

            TmdbTvSeriesSearchPage searchPage;
            try
            {
                searchPage = await _tmdbService.SearchTvSeriesAsync(candidate.CandidateName, 1, cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                result.Summary.AddError("TV.Search", TrimMessage(exception.Message));
                continue;
            }

            var bestCandidate = searchPage.Results
                .Select(item => new TvSearchCandidate(item, CalculateSeriesConfidence(candidate.CandidateName, item)))
                .OrderByDescending(x => x.Confidence)
                .FirstOrDefault();

            if (bestCandidate is null || bestCandidate.Confidence < MinimumAutoMatchConfidence)
            {
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                continue;
            }

            TmdbTvSeriesDetailResult? seriesDetails = null;
            TmdbTvSeasonDetailResult? seasonDetails = null;
            try
            {
                seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(
                    bestCandidate.Item.TmdbId,
                    cancellationToken: cancellationToken);
                seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(
                    bestCandidate.Item.TmdbId,
                    candidate.SeasonNumber,
                    cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                result.Summary.AddWarning("TV.Detail", TrimMessage(exception.Message));
            }

            try
            {
                await UpsertMatchedSeasonAsync(
                    candidate,
                    bestCandidate.Item,
                    bestCandidate.Confidence,
                    seriesDetails,
                    seasonDetails,
                    IdentificationStatusFromConfidence(bestCandidate.Confidence),
                    cancellationToken);
                result.Summary.BoundCount++;
            }
            catch (Exception exception)
            {
                await UpsertUnidentifiedSeasonAsync(candidate, cancellationToken);
                result.Summary.PlaceholderCount++;
                result.Summary.AddError("TV.Apply", TrimMessage(exception.Message));
            }
        }

        return result;
    }

    public async Task<int> ApplyManualMediaFileMatchAsync(
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

        if (seasonNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonNumber));
        }

        if (episodeNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(episodeNumber));
        }

        var seriesDetails = await _tmdbService.GetTvSeriesDetailsAsync(
                                seriesTmdbId,
                                cancellationToken: cancellationToken)
                            ?? throw new InvalidOperationException("无法读取 TMDB 电视剧详情。");
        var seasonDetails = await _tmdbService.GetTvSeasonDetailsAsync(
                                seriesTmdbId,
                                seasonNumber,
                                cancellationToken: cancellationToken)
                            ?? throw new InvalidOperationException("无法读取 TMDB 电视剧季详情。");
        var episodeMetadata = seasonDetails.Episodes.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var mediaFile = await dbContext.MediaFiles
            .Include(x => x.Movie)
            .Include(x => x.Episode)
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken)
            ?? throw new InvalidOperationException("待修正的播放源不存在。");

        var previousMovieId = mediaFile.MovieId;
        var tvSeries = await UpsertSeriesAsync(dbContext, seriesDetails, null, cancellationToken);
        var tvSeason = await UpsertSeasonAsync(
            dbContext,
            tvSeries,
            seasonNumber,
            1d,
            IdentificationStatus.ManualConfirmed,
            seriesDetails,
            seasonDetails,
            cancellationToken);
        await UpsertSeasonMetadataEpisodesAsync(dbContext, tvSeason, seasonDetails, cancellationToken);
        var tvEpisode = await UpsertEpisodeAsync(
            dbContext,
            tvSeason,
            episodeNumber,
            episodeMetadata,
            null,
            cancellationToken);

        mediaFile.MovieId = null;
        mediaFile.Movie = null;
        mediaFile.EpisodeId = tvEpisode.Id;
        mediaFile.Episode = tvEpisode;
        mediaFile.UpdatedAt = DateTime.UtcNow;

        if (previousMovieId.HasValue)
        {
            await ReconcileMovieAfterSourceMoveAsync(dbContext, previousMovieId.Value, mediaFile.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tvEpisode.Id;
    }

    private async Task<IReadOnlyList<TvSeasonCandidate>> BuildCandidatesAsync(
        IReadOnlyCollection<int> mediaFileIds,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        var touchedFiles = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => mediaFileIds.Contains(x.Id)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.MovieId.HasValue)
            .Select(
                x => new CandidateMediaFile
                {
                    Id = x.Id,
                    SourceConnectionId = x.SourceConnectionId,
                    ScanPathId = x.ScanPathId,
                    EpisodeId = x.EpisodeId,
                    FileName = x.FileName,
                    FilePath = x.FilePath
                })
            .ToListAsync(cancellationToken);

        if (touchedFiles.Count == 0)
        {
            return [];
        }

        var touchedDirectoryKeys = touchedFiles
            .Select(x => BuildDirectoryKey(x.SourceConnectionId, GetDirectoryPath(x.FilePath)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceConnectionIds = touchedFiles
            .Select(x => x.SourceConnectionId)
            .Distinct()
            .ToArray();

        var sourceFiles = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(
                x => sourceConnectionIds.Contains(x.SourceConnectionId)
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted
                     && !x.MovieId.HasValue)
            .Select(
                x => new CandidateMediaFile
                {
                    Id = x.Id,
                    SourceConnectionId = x.SourceConnectionId,
                    ScanPathId = x.ScanPathId,
                    EpisodeId = x.EpisodeId,
                    FileName = x.FileName,
                    FilePath = x.FilePath
                })
            .ToListAsync(cancellationToken);

        var candidates = new List<TvSeasonCandidate>();
        foreach (var directoryGroup in sourceFiles
                     .GroupBy(x => new
                     {
                         x.SourceConnectionId,
                         DirectoryPath = GetDirectoryPath(x.FilePath)
                     })
                     .Where(x => touchedDirectoryKeys.Contains(BuildDirectoryKey(x.Key.SourceConnectionId, x.Key.DirectoryPath))))
        {
            candidates.AddRange(BuildCandidatesForDirectory(directoryGroup.Key.DirectoryPath, directoryGroup.ToList()));
        }

        return candidates;
    }

    private static IReadOnlyList<TvSeasonCandidate> BuildCandidatesForDirectory(
        string directoryPath,
        IReadOnlyList<CandidateMediaFile> files)
    {
        var folderName = GetFolderName(directoryPath);
        var folderSeasonNumber = TvEpisodeFileNameParser.TryParseSeasonNumber(folderName);
        var parsedFiles = files
            .Select(
                file =>
                {
                    var parseResult = TvEpisodeFileNameParser.Parse(file.FileName, allowSeasonContextOnly: true);
                    var effectiveSeasonNumber = parseResult.IsSeasonContextOnly && folderSeasonNumber.HasValue
                        ? folderSeasonNumber.Value
                        : parseResult.SeasonNumber;
                    return new TvSeasonCandidateFile
                    {
                        MediaFileId = file.Id,
                        FileName = file.FileName,
                        FilePath = file.FilePath,
                        EpisodeId = file.EpisodeId,
                        SeasonNumber = effectiveSeasonNumber,
                        ParseResult = parseResult
                    };
                })
            .ToList();

        var unsupportedFiles = parsedFiles
            .Where(x => x.ParseResult.IsMultiEpisode)
            .ToList();
        var validEpisodeFiles = parsedFiles
            .Where(
                x => x.ParseResult.IsEpisodeLike
                     && !x.ParseResult.IsMultiEpisode
                     && x.ParseResult.EpisodeNumber > 0)
            .ToList();

        if (validEpisodeFiles.Count < 2 && !parsedFiles.Any(x => x.EpisodeId.HasValue))
        {
            return unsupportedFiles.Count == 0
                ? []
                : [
                    new TvSeasonCandidate
                    {
                        SourceConnectionId = files[0].SourceConnectionId,
                        DirectoryPath = directoryPath,
                        FolderName = folderName,
                        CandidateName = BuildCandidateName(folderName, validEpisodeFiles),
                        CommonPrefix = BuildCommonPrefix(validEpisodeFiles),
                        SeasonNumber = folderSeasonNumber ?? 1,
                        UnsupportedFiles = unsupportedFiles
                    }
                ];
        }

        var candidates = new List<TvSeasonCandidate>();
        foreach (var seasonGroup in validEpisodeFiles.GroupBy(x => Math.Max(1, x.SeasonNumber)))
        {
            var seasonFiles = seasonGroup
                .GroupBy(x => x.ParseResult.EpisodeNumber)
                .SelectMany(x => x)
                .ToList();
            var seasonNumber = seasonGroup.Key;
            candidates.Add(
                new TvSeasonCandidate
                {
                    SourceConnectionId = files[0].SourceConnectionId,
                    DirectoryPath = directoryPath,
                    FolderName = folderName,
                    CandidateName = BuildCandidateName(folderName, seasonFiles),
                    CommonPrefix = BuildCommonPrefix(seasonFiles),
                    SeasonNumber = seasonNumber,
                    Files = seasonFiles,
                    UnsupportedFiles = unsupportedFiles
                        .Where(x => Math.Max(1, x.SeasonNumber) == seasonNumber || !x.ParseResult.IsEpisodeLike)
                        .ToList()
                });
        }

        return candidates;
    }

    private async Task UpsertMatchedSeasonAsync(
        TvSeasonCandidate candidate,
        TmdbTvSeriesSearchItem searchItem,
        double confidence,
        TmdbTvSeriesDetailResult? seriesDetails,
        TmdbTvSeasonDetailResult? seasonDetails,
        IdentificationStatus status,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var tvSeries = await UpsertSeriesAsync(dbContext, seriesDetails, searchItem, cancellationToken);
        var tvSeason = await UpsertSeasonAsync(
            dbContext,
            tvSeries,
            candidate.SeasonNumber,
            confidence,
            status,
            seriesDetails,
            seasonDetails,
            cancellationToken);
        await UpsertSeasonMetadataEpisodesAsync(dbContext, tvSeason, seasonDetails, cancellationToken);

        foreach (var candidateFile in candidate.Files)
        {
            var episodeMetadata = seasonDetails?.Episodes
                .FirstOrDefault(x => x.EpisodeNumber == candidateFile.ParseResult.EpisodeNumber);
            var tvEpisode = await UpsertEpisodeAsync(
                dbContext,
                tvSeason,
                candidateFile.ParseResult.EpisodeNumber,
                episodeMetadata,
                candidateFile,
                cancellationToken);

            await AttachMediaFileToEpisodeAsync(dbContext, candidateFile.MediaFileId, tvEpisode.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task UpsertUnidentifiedSeasonAsync(
        TvSeasonCandidate candidate,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var seriesName = FirstNonEmpty(candidate.CandidateName, candidate.CommonPrefix, candidate.FolderName, UnidentifiedSeasonTitle);
        var tvSeries = await dbContext.TvSeries
            .FirstOrDefaultAsync(
                x => !x.TmdbSeriesId.HasValue
                     && x.Name == seriesName,
                cancellationToken);
        if (tvSeries is null)
        {
            tvSeries = new TvSeries
            {
                Name = TruncateRequired(seriesName, 300),
                CreatedAt = now
            };
            dbContext.TvSeries.Add(tvSeries);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        tvSeries.OriginalName = null;
        tvSeries.Overview = null;
        tvSeries.PosterRemoteUrl = null;
        tvSeries.Country = null;
        tvSeries.Language = null;
        tvSeries.FirstAirDate = null;
        tvSeries.FirstAirYear = null;
        tvSeries.GenresText = null;
        tvSeries.UpdatedAt = now;

        var tvSeason = await dbContext.TvSeasons
            .FirstOrDefaultAsync(
                x => x.TvSeriesId == tvSeries.Id
                     && x.SeasonNumber == candidate.SeasonNumber,
                cancellationToken);
        if (tvSeason is null)
        {
            tvSeason = new TvSeason
            {
                TvSeriesId = tvSeries.Id,
                SeasonNumber = candidate.SeasonNumber,
                CreatedAt = now
            };
            dbContext.TvSeasons.Add(tvSeason);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        tvSeason.TmdbSeasonId = null;
        tvSeason.Name = TruncateRequired($"{UnidentifiedSeasonTitle} S{candidate.SeasonNumber:D2}", 300);
        tvSeason.Overview = null;
        tvSeason.PosterRemoteUrl = null;
        tvSeason.AirDate = null;
        tvSeason.TmdbEpisodeCount = candidate.Files.Count;
        tvSeason.IdentifiedConfidence = null;
        tvSeason.IdentificationStatus = IdentificationStatus.Failed;
        tvSeason.UpdatedAt = now;

        foreach (var candidateFile in candidate.Files)
        {
            var tvEpisode = await UpsertEpisodeAsync(
                dbContext,
                tvSeason,
                candidateFile.ParseResult.EpisodeNumber,
                null,
                candidateFile,
                cancellationToken);
            await AttachMediaFileToEpisodeAsync(dbContext, candidateFile.MediaFileId, tvEpisode.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<TvSeries> UpsertSeriesAsync(
        AppDbContext dbContext,
        TmdbTvSeriesDetailResult? seriesDetails,
        TmdbTvSeriesSearchItem? searchItem,
        CancellationToken cancellationToken)
    {
        var tmdbSeriesId = seriesDetails?.TmdbId ?? searchItem?.TmdbId;
        if (tmdbSeriesId is not > 0)
        {
            throw new InvalidOperationException("TV Series TMDB id 无效。");
        }

        var tvSeries = await dbContext.TvSeries
            .FirstOrDefaultAsync(x => x.TmdbSeriesId == tmdbSeriesId.Value, cancellationToken);
        if (tvSeries is null)
        {
            tvSeries = new TvSeries
            {
                TmdbSeriesId = tmdbSeriesId.Value,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvSeries.Add(tvSeries);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        tvSeries.Name = TruncateRequired(FirstNonEmpty(seriesDetails?.Name, searchItem?.Name, $"TV {tmdbSeriesId.Value}"), 300);
        tvSeries.OriginalName = Truncate(FirstNonEmpty(seriesDetails?.OriginalName, searchItem?.OriginalName), 300);
        tvSeries.Overview = Truncate(FirstNonEmpty(seriesDetails?.Overview, searchItem?.Overview), 5000);
        tvSeries.PosterRemoteUrl = EmptyToNull(FirstNonEmpty(seriesDetails?.PosterRemoteUrl, searchItem?.PosterRemoteUrl));
        tvSeries.Country = Truncate(string.Join(", ", seriesDetails?.OriginCountries ?? searchItem?.OriginCountries ?? []), 120);
        tvSeries.Language = Truncate(FirstNonEmpty(seriesDetails?.OriginalLanguage, searchItem?.OriginalLanguage), 120);
        tvSeries.FirstAirDate = ParseDate(FirstNonEmpty(seriesDetails?.FirstAirDate, searchItem?.FirstAirDate));
        tvSeries.FirstAirYear = seriesDetails?.FirstAirYear ?? searchItem?.FirstAirYear;
        tvSeries.GenresText = Truncate(seriesDetails?.GenresText ?? string.Empty, 1000);
        tvSeries.UpdatedAt = DateTime.UtcNow;
        return tvSeries;
    }

    private static async Task<TvSeason> UpsertSeasonAsync(
        AppDbContext dbContext,
        TvSeries tvSeries,
        int seasonNumber,
        double confidence,
        IdentificationStatus status,
        TmdbTvSeriesDetailResult? seriesDetails,
        TmdbTvSeasonDetailResult? seasonDetails,
        CancellationToken cancellationToken)
    {
        var tvSeason = await dbContext.TvSeasons
            .FirstOrDefaultAsync(
                x => x.TvSeriesId == tvSeries.Id
                     && x.SeasonNumber == seasonNumber,
                cancellationToken);
        if (tvSeason is null)
        {
            tvSeason = new TvSeason
            {
                TvSeriesId = tvSeries.Id,
                SeasonNumber = seasonNumber,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvSeasons.Add(tvSeason);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var summary = seriesDetails?.Seasons.FirstOrDefault(x => x.SeasonNumber == seasonNumber);
        tvSeason.TmdbSeasonId = PositiveOrNull(seasonDetails?.TmdbId) ?? summary?.TmdbId;
        tvSeason.Name = TruncateRequired(FirstNonEmpty(seasonDetails?.Name, summary?.Name, $"Season {seasonNumber}"), 300);
        tvSeason.Overview = Truncate(FirstNonEmpty(seasonDetails?.Overview, summary?.Overview), 5000);
        tvSeason.PosterRemoteUrl = EmptyToNull(FirstNonEmpty(seasonDetails?.PosterRemoteUrl, summary?.PosterRemoteUrl));
        tvSeason.AirDate = ParseDate(FirstNonEmpty(seasonDetails?.AirDate, summary?.AirDate));
        tvSeason.TmdbEpisodeCount = seasonDetails?.EpisodeCount > 0
            ? seasonDetails.EpisodeCount
            : summary?.EpisodeCount;
        tvSeason.IdentifiedConfidence = confidence;
        tvSeason.IdentificationStatus = status;
        tvSeason.UpdatedAt = DateTime.UtcNow;
        return tvSeason;
    }

    private static async Task UpsertSeasonMetadataEpisodesAsync(
        AppDbContext dbContext,
        TvSeason tvSeason,
        TmdbTvSeasonDetailResult? seasonDetails,
        CancellationToken cancellationToken)
    {
        if (seasonDetails is null || seasonDetails.Episodes.Count == 0)
        {
            return;
        }

        foreach (var metadata in seasonDetails.Episodes
                     .Where(x => x.EpisodeNumber > 0)
                     .GroupBy(x => x.EpisodeNumber)
                     .Select(x => x.OrderByDescending(y => y.TmdbId).First())
                     .OrderBy(x => x.EpisodeNumber))
        {
            await UpsertEpisodeAsync(
                dbContext,
                tvSeason,
                metadata.EpisodeNumber,
                metadata,
                candidateFile: null,
                cancellationToken);
        }
    }

    private static async Task<TvEpisode> UpsertEpisodeAsync(
        AppDbContext dbContext,
        TvSeason tvSeason,
        int episodeNumber,
        TmdbTvEpisodeMetadataItem? metadata,
        TvSeasonCandidateFile? candidateFile,
        CancellationToken cancellationToken)
    {
        var tvEpisode = await dbContext.TvEpisodes
            .FirstOrDefaultAsync(
                x => x.TvSeasonId == tvSeason.Id
                     && x.EpisodeNumber == episodeNumber,
                cancellationToken);
        if (tvEpisode is null)
        {
            tvEpisode = new TvEpisode
            {
                TvSeasonId = tvSeason.Id,
                EpisodeNumber = episodeNumber,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.TvEpisodes.Add(tvEpisode);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        tvEpisode.TmdbEpisodeId = PositiveOrNull(metadata?.TmdbId);
        tvEpisode.Title = TruncateRequired(
            FirstNonEmpty(
                metadata?.Name,
                candidateFile?.ParseResult.EpisodeTitleCandidate,
                $"第 {episodeNumber} 集"),
            300);
        tvEpisode.Overview = Truncate(metadata?.Overview ?? string.Empty, 5000);
        tvEpisode.StillRemoteUrl = EmptyToNull(metadata?.StillRemoteUrl);
        tvEpisode.AirDate = ParseDate(metadata?.AirDate ?? string.Empty);
        tvEpisode.RuntimeMinutes = metadata?.RuntimeMinutes;
        tvEpisode.UpdatedAt = DateTime.UtcNow;
        return tvEpisode;
    }

    private static async Task AttachMediaFileToEpisodeAsync(
        AppDbContext dbContext,
        int mediaFileId,
        int episodeId,
        CancellationToken cancellationToken)
    {
        var mediaFile = await dbContext.MediaFiles
            .FirstOrDefaultAsync(
                x => x.Id == mediaFileId
                     && x.MediaType == MediaType.Video
                     && !x.IsDeleted,
                cancellationToken);
        if (mediaFile is null || mediaFile.MovieId.HasValue)
        {
            return;
        }

        mediaFile.MovieId = null;
        mediaFile.EpisodeId = episodeId;
        mediaFile.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task ReconcileMovieAfterSourceMoveAsync(
        AppDbContext dbContext,
        int movieId,
        int movedMediaFileId,
        CancellationToken cancellationToken)
    {
        var movie = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .FirstOrDefaultAsync(x => x.Id == movieId, cancellationToken);
        if (movie is null || movie.DefaultMediaFileId != movedMediaFileId)
        {
            return;
        }

        movie.DefaultMediaFileId = movie.MediaFiles
            .Where(x => x.Id != movedMediaFileId && x.MediaType == MediaType.Video && !x.IsDeleted)
            .OrderByDescending(x => x.LastSeenAt ?? x.UpdatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        movie.UpdatedAt = DateTime.UtcNow;
    }

    private static string BuildCandidateName(
        string folderName,
        IReadOnlyList<TvSeasonCandidateFile> files)
    {
        var cleanedFolder = TvEpisodeFileNameParser.CleanSeriesNameCandidate(folderName);
        if (!string.IsNullOrWhiteSpace(cleanedFolder))
        {
            return cleanedFolder;
        }

        return FirstNonEmpty(
            BuildCommonPrefix(files),
            files.Select(x => x.ParseResult.SeriesNameCandidate).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
            folderName);
    }

    private static string BuildCommonPrefix(IReadOnlyList<TvSeasonCandidateFile> files)
    {
        var candidates = files
            .Select(x => FirstNonEmpty(x.ParseResult.SeriesNameCandidate, TvEpisodeFileNameParser.CleanSeriesNameCandidate(x.FileName)))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => NormalizeTitle(x), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenByDescending(x => x.First().Length)
            .Select(x => x.First())
            .FirstOrDefault();

        return candidates ?? string.Empty;
    }

    private static double CalculateSeriesConfidence(string expectedTitle, TmdbTvSeriesSearchItem candidate)
    {
        var titleSimilarity = MovieFileNameParser.CalculateTitleSimilarity(expectedTitle, candidate.Name);
        var originalTitleSimilarity = MovieFileNameParser.CalculateTitleSimilarity(expectedTitle, candidate.OriginalName);
        return Math.Clamp(Math.Max(titleSimilarity, originalTitleSimilarity), 0d, 1d);
    }

    private static IdentificationStatus IdentificationStatusFromConfidence(double confidence)
    {
        return confidence >= MatchedConfidence
            ? IdentificationStatus.Matched
            : IdentificationStatus.NeedsReview;
    }

    private static string GetDirectoryPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex <= 0
            ? "/"
            : normalized[..lastSeparatorIndex];
    }

    private static string GetFolderName(string directoryPath)
    {
        var normalized = directoryPath.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex < 0
            ? normalized
            : normalized[(lastSeparatorIndex + 1)..];
    }

    private static string BuildDirectoryKey(int sourceConnectionId, string directoryPath)
    {
        return $"{sourceConnectionId}:{directoryPath.Replace('\\', '/').TrimEnd('/').ToUpperInvariant()}";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string NormalizeTitle(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date.Date
            : null;
    }

    private static int? PositiveOrNull(int? value)
    {
        return value is > 0 ? value.Value : null;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TruncateRequired(string value, int maxLength)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? UnidentifiedSeasonTitle : value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TrimMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "未知错误" : message.Trim();
    }

    private sealed class CandidateMediaFile
    {
        public int Id { get; set; }

        public int SourceConnectionId { get; set; }

        public int? ScanPathId { get; set; }

        public int? EpisodeId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;
    }

    private sealed class TvSeasonCandidate
    {
        public int SourceConnectionId { get; set; }

        public string DirectoryPath { get; set; } = string.Empty;

        public string FolderName { get; set; } = string.Empty;

        public string CandidateName { get; set; } = string.Empty;

        public string CommonPrefix { get; set; } = string.Empty;

        public int SeasonNumber { get; set; } = 1;

        public List<TvSeasonCandidateFile> Files { get; set; } = [];

        public List<TvSeasonCandidateFile> UnsupportedFiles { get; set; } = [];
    }

    private sealed class TvSeasonCandidateFile
    {
        public int MediaFileId { get; set; }

        public int? EpisodeId { get; set; }

        public int SeasonNumber { get; set; } = 1;

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public TvEpisodeFileNameParseResult ParseResult { get; set; } = new();
    }

    private sealed record TvSearchCandidate(TmdbTvSeriesSearchItem Item, double Confidence);
}
