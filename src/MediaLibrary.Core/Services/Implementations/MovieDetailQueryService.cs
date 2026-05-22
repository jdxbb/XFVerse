using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class MovieDetailQueryService : IMovieDetailQueryService
{
    public async Task<MovieDetailModel?> GetMovieDetailAsync(
        int movieId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movie = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.Id == movieId)
            .Select(
                x => new
                {
                    x.Id,
                    x.Title,
                    x.OriginalTitle,
                    x.ReleaseYear,
                    x.Overview,
                    x.PosterRemoteUrl,
                    x.PosterLocalPath,
                    x.Country,
                    x.Language,
                    x.RuntimeMinutes,
                    x.GenresText,
                    x.AiTagsText,
                    x.EmotionTagsText,
                    x.SceneTagsText,
                    x.TmdbId,
                    x.ImdbId,
                    x.IdentificationStatus,
                    x.IdentifiedConfidence,
                    x.DefaultMediaFileId,
                    x.IsFavorite,
                    x.IsWatched,
                    x.UserRating
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (movie is null)
        {
            return null;
        }

        var isNotInterested = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(x => x.IsNotInterested)
            .AnyAsync(
                x => x.MovieId == movie.Id
                     || (movie.TmdbId.HasValue && x.TmdbId == movie.TmdbId.Value)
                     || (!string.IsNullOrWhiteSpace(movie.ImdbId) && x.ImdbId == movie.ImdbId)
                     || (x.Title == movie.Title && x.ReleaseYear == movie.ReleaseYear),
                cancellationToken);

        var collectionState = await dbContext.UserMovieCollectionItems
            .AsNoTracking()
            .Where(
                x => x.MovieId == movie.Id
                     || (movie.TmdbId.HasValue && x.TmdbId == movie.TmdbId.Value)
                     || (!string.IsNullOrWhiteSpace(movie.ImdbId) && x.ImdbId == movie.ImdbId)
                     || (x.Title == movie.Title && x.ReleaseYear == movie.ReleaseYear))
            .OrderByDescending(x => x.LibraryVisibilityState != LibraryVisibilityState.Auto)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(
                x => new
                {
                    x.LibraryVisibilityState,
                    HasUserState = x.IsWatched || x.IsWantToWatch || x.IsNotInterested
                })
            .FirstOrDefaultAsync(cancellationToken);

        var ratings = await dbContext.RatingSources
            .AsNoTracking()
            .Where(rating => rating.MovieId == movieId)
            .OrderByDescending(rating => rating.LastUpdatedAt ?? rating.CreatedAt)
            .Select(
                rating => new MovieRatingItem
                {
                    SourceName = rating.SourceName,
                    ScoreValue = rating.ScoreValue,
                    ScoreScale = rating.ScoreScale,
                    VoteCount = rating.VoteCount,
                    SourceUrl = rating.SourceUrl ?? string.Empty,
                    LastUpdatedAt = rating.LastUpdatedAt ?? rating.CreatedAt
                })
            .ToListAsync(cancellationToken);

        var sources = await dbContext.MediaFiles
            .AsNoTracking()
            .Where(mediaFile => mediaFile.MovieId == movieId
                                && !mediaFile.IsDeleted
                                && mediaFile.MediaType == MediaType.Video)
            .Select(
                mediaFile => new MovieSourceItem
                {
                    MediaFileId = mediaFile.Id,
                    FileName = mediaFile.FileName,
                    FilePath = mediaFile.FilePath,
                    Extension = mediaFile.Extension,
                    FileSize = mediaFile.FileSize,
                    LastModifiedAt = mediaFile.LastModifiedAt,
                    DurationSeconds = mediaFile.DurationSeconds,
                    ResolutionWidth = mediaFile.ResolutionWidth,
                    ResolutionHeight = mediaFile.ResolutionHeight,
                    VideoCodec = mediaFile.VideoCodec,
                    AudioCodec = mediaFile.AudioCodec,
                    AudioChannels = mediaFile.AudioChannels,
                    AudioSampleRate = mediaFile.AudioSampleRate,
                    OverallBitrateKbps = mediaFile.OverallBitrateKbps,
                    VideoBitrateKbps = mediaFile.VideoBitrateKbps,
                    AudioBitrateKbps = mediaFile.AudioBitrateKbps,
                    MediaProbeStatus = mediaFile.MediaProbeStatus,
                    MediaProbeError = mediaFile.MediaProbeError,
                    MediaProbedAt = mediaFile.MediaProbedAt,
                    ProtocolType = mediaFile.SourceConnection!.ProtocolType
                })
            .ToListAsync(cancellationToken);

        var effectiveDefaultMediaFileId = ResolveEffectiveDefaultMediaFileId(sources, movie.DefaultMediaFileId);
        foreach (var source in sources)
        {
            source.IsDefault = source.MediaFileId == effectiveDefaultMediaFileId;
        }
        var hasActiveSource = sources.Count > 0;
        var hasLocalRecognizedMovie = movie.TmdbId.HasValue
                                      && movie.IdentificationStatus != IdentificationStatus.Failed;
        var libraryVisibilityState = collectionState?.LibraryVisibilityState ?? LibraryVisibilityState.Auto;
        var isVisibleInLibrary = ResolveIsVisibleInLibrary(
            hasActiveSource,
            libraryVisibilityState,
            hasLocalRecognizedMovie
            || movie.IsFavorite
            || movie.IsWatched
            || movie.UserRating.HasValue
            || isNotInterested
            || collectionState?.HasUserState == true);

        sources = sources
            .OrderBy(source => source.MediaFileId == effectiveDefaultMediaFileId ? 0 : 1)
            .ThenBy(source => source.ProtocolType == ProtocolType.Local ? 0 : 1)
            .ThenBy(source => source.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sourceIds = sources.Select(source => source.MediaFileId).ToArray();
        var latestHistoryRows = sourceIds.Length == 0
            ? []
            : await dbContext.WatchHistories
                .AsNoTracking()
                .Where(history => history.MovieId == movieId && sourceIds.Contains(history.MediaFileId))
                .OrderByDescending(history => history.EndedAt ?? history.StartedAt)
                .Select(
                    history => new
                    {
                        history.MediaFileId,
                        LastPlayedAt = history.EndedAt ?? history.StartedAt,
                        history.LastPlayPositionSeconds
                    })
                .ToListAsync(cancellationToken);

        var latestHistoryBySource = latestHistoryRows
            .GroupBy(history => history.MediaFileId)
            .ToDictionary(group => group.Key, group => group.First());

        var subtitleBindings = sourceIds.Length == 0
            ? []
            : await dbContext.SubtitleBindings
                .AsNoTracking()
                .Where(binding => sourceIds.Contains(binding.MediaFileId)
                                  && binding.SubtitleMediaFile != null
                                  && !binding.SubtitleMediaFile.IsDeleted)
                .OrderBy(binding => binding.Priority)
                .Select(
                    binding => new
                    {
                        binding.MediaFileId,
                        Item = new SubtitleBindingItem
                        {
                            SubtitleMediaFileId = binding.SubtitleMediaFileId,
                            FileName = binding.SubtitleMediaFile!.FileName,
                            FilePath = binding.SubtitleMediaFile.FilePath,
                            MatchType = binding.MatchType,
                            Language = binding.Language ?? string.Empty,
                            IsAutoLoaded = binding.IsAutoLoaded,
                            Priority = binding.Priority
                        }
                    })
                .ToListAsync(cancellationToken);

        var subtitlesBySource = subtitleBindings
            .GroupBy(binding => binding.MediaFileId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SubtitleBindingItem>)group.Select(binding => binding.Item).ToList());

        foreach (var source in sources)
        {
            source.SubtitleBindings = subtitlesBySource.TryGetValue(source.MediaFileId, out var subtitles)
                ? subtitles
                : [];

            if (latestHistoryBySource.TryGetValue(source.MediaFileId, out var history))
            {
                source.LastPlayedAt = history.LastPlayedAt;
                source.LastPlayPositionSeconds = history.LastPlayPositionSeconds;
            }
        }

        return new MovieDetailModel
        {
            MovieId = movie.Id,
            Title = movie.Title,
            OriginalTitle = movie.OriginalTitle ?? string.Empty,
            ReleaseYear = movie.ReleaseYear,
            Overview = movie.Overview ?? string.Empty,
            PosterRemoteUrl = movie.PosterRemoteUrl ?? string.Empty,
            PosterLocalPath = movie.PosterLocalPath ?? string.Empty,
            Country = movie.Country ?? string.Empty,
            Language = movie.Language ?? string.Empty,
            RuntimeMinutes = movie.RuntimeMinutes,
            GenresText = movie.GenresText ?? string.Empty,
            AiTagsText = AiTagVocabulary.NormalizeText(movie.AiTagsText, AiTagVocabulary.TypeTags),
            EmotionTagsText = AiTagVocabulary.NormalizeText(movie.EmotionTagsText, AiTagVocabulary.EmotionTags),
            SceneTagsText = AiTagVocabulary.NormalizeText(movie.SceneTagsText, AiTagVocabulary.SceneTags),
            TmdbId = movie.TmdbId,
            ImdbId = movie.ImdbId ?? string.Empty,
            IdentificationStatus = movie.IdentificationStatus,
            IdentifiedConfidence = movie.IdentifiedConfidence,
            DefaultMediaFileId = effectiveDefaultMediaFileId,
            IsFavorite = movie.IsFavorite,
            IsWatched = movie.IsWatched,
            IsNotInterested = isNotInterested,
            IsVisibleInLibrary = isVisibleInLibrary,
            LibraryVisibilityState = libraryVisibilityState,
            Ratings = ratings,
            Sources = sources
        };
    }

    private static bool ResolveIsVisibleInLibrary(
        bool hasActiveSource,
        LibraryVisibilityState visibilityState,
        bool hasCurrentState)
    {
        return visibilityState switch
        {
            LibraryVisibilityState.Hidden => false,
            LibraryVisibilityState.Visible => true,
            _ => hasActiveSource || hasCurrentState
        };
    }

    private static int? ResolveEffectiveDefaultMediaFileId(
        IReadOnlyList<MovieSourceItem> sources,
        int? storedDefaultMediaFileId)
    {
        if (sources.Count == 0)
        {
            return null;
        }

        var storedDefault = storedDefaultMediaFileId.HasValue
            ? sources.FirstOrDefault(x => x.MediaFileId == storedDefaultMediaFileId.Value)
            : null;
        if (storedDefault is not null && IsAvailableForAutomaticSelection(storedDefault))
        {
            return storedDefault.MediaFileId;
        }

        var localSource = sources.FirstOrDefault(x => x.ProtocolType == ProtocolType.Local && IsExistingLocalFile(x.FilePath));
        if (localSource is not null)
        {
            return localSource.MediaFileId;
        }

        if (storedDefault is not null && storedDefault.ProtocolType != ProtocolType.Local)
        {
            return storedDefault.MediaFileId;
        }

        return sources
            .Where(x => x.ProtocolType != ProtocolType.Local)
            .OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(x => (int?)x.MediaFileId)
            .FirstOrDefault()
            ?? storedDefault?.MediaFileId
            ?? sources.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase).First().MediaFileId;
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

    private static bool IsAvailableForAutomaticSelection(MovieSourceItem source)
    {
        return source.ProtocolType != ProtocolType.Local || IsExistingLocalFile(source.FilePath);
    }
}
