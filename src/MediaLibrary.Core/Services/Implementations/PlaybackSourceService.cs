using MediaLibrary.Core.Data;
using MediaLibrary.Core.Helpers;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class PlaybackSourceService : IPlaybackSourceService
{
    public async Task<PlaybackSessionModel?> GetPlaybackSessionAsync(
        int movieId,
        int? preferredMediaFileId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var movie = await dbContext.Movies
            .AsNoTracking()
            .Where(x => x.Id == movieId)
            .Select(x => new { x.Id, x.Title, x.DefaultMediaFileId })
            .FirstOrDefaultAsync(cancellationToken);

        if (movie is null)
        {
            return null;
        }

        var sourceRows = await dbContext.MediaFiles
            .AsNoTracking()
            .Include(x => x.SourceConnection)
            .Where(x => x.MovieId == movieId && x.MediaType == MediaType.Video && !x.IsDeleted)
            .OrderBy(x => x.Id != movie.DefaultMediaFileId)
            .ThenBy(x => x.FileName)
            .Select(
                x => new
                {
                    x.Id,
                    x.SourceConnectionId,
                    x.FileName,
                    x.FilePath,
                    x.RemoteUri,
                    x.Extension,
                    x.FileSize,
                    x.LastModifiedAt,
                    x.DurationSeconds,
                    x.ResolutionWidth,
                    x.ResolutionHeight,
                    x.VideoCodec,
                    x.AudioCodec,
                    x.AudioChannels,
                    x.AudioSampleRate,
                    x.OverallBitrateKbps,
                    x.VideoBitrateKbps,
                    x.AudioBitrateKbps,
                    x.MediaProbeStatus,
                    x.MediaProbeError,
                    x.MediaProbedAt,
                    x.SourceConnection!.BaseUrl,
                    x.SourceConnection.Username,
                    x.SourceConnection.PasswordEncrypted,
                    x.SourceConnection.ProtocolType
                })
            .ToListAsync(cancellationToken);

        if (sourceRows.Count == 0)
        {
            return new PlaybackSessionModel
            {
                MovieId = movie.Id,
                MovieTitle = movie.Title,
                DefaultMediaFileId = movie.DefaultMediaFileId
            };
        }

        var sourceIds = sourceRows.Select(x => x.Id).ToArray();
        var historyRows = await dbContext.WatchHistories
            .AsNoTracking()
            .Where(x => x.MovieId == movieId && sourceIds.Contains(x.MediaFileId))
            .OrderByDescending(x => x.EndedAt ?? x.StartedAt)
            .Select(
                x => new WatchHistoryProjection
                {
                    MediaFileId = x.MediaFileId,
                    LastPlayPositionSeconds = x.LastPlayPositionSeconds,
                    IsCompleted = x.IsCompleted,
                    StartedAt = x.StartedAt,
                    EndedAt = x.EndedAt
                })
            .ToListAsync(cancellationToken);

        var latestHistory = historyRows
            .GroupBy(x => x.MediaFileId)
            .ToDictionary(x => x.Key, x => x.First());
        var sourceDurations = sourceRows.ToDictionary(x => x.Id, x => x.DurationSeconds);
        var sourceSpecificResumePositions = sourceRows.ToDictionary(
            x => x.Id,
            x => ResolveSourceSpecificResume(
                historyRows.Where(history => history.MediaFileId == x.Id),
                x.DurationSeconds));
        var unifiedResume = ResolveUnifiedResume(movieId, historyRows, sourceDurations);
        var projectedResumePositions = new Dictionary<int, int>();
        var projectedLastPlayedAt = new Dictionary<int, DateTime?>();
        foreach (var sourceRow in sourceRows)
        {
            latestHistory.TryGetValue(sourceRow.Id, out var latest);
            var sourceSpecificResume = sourceSpecificResumePositions.TryGetValue(sourceRow.Id, out var specificResume)
                ? specificResume
                : 0;
            var projectedResume = sourceSpecificResume;
            var projectedLastPlayed = latest?.LastPlayedAt;

            if (unifiedResume.IsCompleted)
            {
                projectedResume = 0;
                projectedLastPlayed = unifiedResume.LastPlayedAt ?? projectedLastPlayed;
                LogWatchHistory(
                    $"watch-history-unified-resume-completed movieId={movieId} fromMediaFileId={unifiedResume.MediaFileId} reason=latest-completed");
            }
            else if (unifiedResume.PositionSeconds > 0)
            {
                var skipReason = ResolveUnifiedResumeSkipReason(unifiedResume, sourceRow.Id, sourceRow.DurationSeconds);
                if (skipReason is null)
                {
                    projectedResume = unifiedResume.PositionSeconds;
                    projectedLastPlayed = unifiedResume.LastPlayedAt ?? projectedLastPlayed;
                    LogWatchHistory(
                        $"watch-history-unified-resume-applied targetMediaFileId={sourceRow.Id} resume={projectedResume}");
                }
                else
                {
                    LogWatchHistory(
                        $"watch-history-unified-resume-skipped movieId={movieId} mediaFileId={sourceRow.Id} reason={skipReason}");
                }
            }
            else if (sourceSpecificResume <= 0)
            {
                LogWatchHistory(
                    $"watch-history-unified-resume-skipped movieId={movieId} mediaFileId={sourceRow.Id} reason=no-valid-history");
            }

            projectedResumePositions[sourceRow.Id] = projectedResume;
            projectedLastPlayedAt[sourceRow.Id] = projectedLastPlayed;
        }

        var subtitleRows = await dbContext.SubtitleBindings
            .AsNoTracking()
            .Include(x => x.SubtitleMediaFile)
            .ThenInclude(x => x!.SourceConnection)
            .Where(x => sourceIds.Contains(x.MediaFileId)
                        && x.SubtitleMediaFile != null
                        && !x.SubtitleMediaFile.IsDeleted)
            .OrderBy(x => x.Priority)
            .Select(
                x => new
                {
                    BindingId = x.Id,
                    x.MediaFileId,
                    x.SubtitleMediaFileId,
                    x.SubtitleMediaFile!.FileName,
                    x.SubtitleMediaFile.FilePath,
                    x.SubtitleMediaFile.RemoteUri,
                    SourceBaseUrl = x.SubtitleMediaFile.SourceConnection!.BaseUrl,
                    x.MatchType,
                    x.IsAutoLoaded,
                    x.Priority
                })
            .ToListAsync(cancellationToken);

        var subtitlesBySource = subtitleRows
            .GroupBy(x => x.MediaFileId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PlaybackSubtitleItem>)group.Select(
                        x => new PlaybackSubtitleItem
                        {
                            DisplayName = $"外挂：{x.FileName}",
                            Type = PlaybackSubtitleType.ExternalFile,
                            BindingId = x.BindingId,
                            MediaFileId = x.SubtitleMediaFileId,
                            SubtitleMediaFileId = x.SubtitleMediaFileId,
                            FileName = x.FileName,
                            FilePath = x.FilePath,
                            PlaybackUrl = WebDavPathHelper.BuildPlaybackUrl(x.SourceBaseUrl, x.FilePath, x.RemoteUri),
                            MatchType = x.MatchType,
                            IsAuto = x.IsAutoLoaded,
                            IsPreferred = x.IsAutoLoaded,
                            IsAutoLoaded = x.IsAutoLoaded,
                            Priority = x.Priority
                        })
                    .ToList());

        var sources = sourceRows
            .Select(
                x =>
                {
                    projectedResumePositions.TryGetValue(x.Id, out var resumePosition);
                    projectedLastPlayedAt.TryGetValue(x.Id, out var lastPlayedAt);
                    subtitlesBySource.TryGetValue(x.Id, out var subtitles);

                    return new PlaybackSourceItem
                    {
                        MediaFileId = x.Id,
                        SourceConnectionId = x.SourceConnectionId,
                        FileName = x.FileName,
                        FilePath = x.FilePath,
                        RemoteUri = x.RemoteUri,
                        PlaybackUrl = WebDavPathHelper.BuildPlaybackUrl(x.BaseUrl, x.FilePath, x.RemoteUri),
                        Extension = x.Extension,
                        FileSize = x.FileSize,
                        LastModifiedAt = x.LastModifiedAt,
                        DurationSeconds = x.DurationSeconds,
                        ResolutionWidth = x.ResolutionWidth,
                        ResolutionHeight = x.ResolutionHeight,
                        VideoCodec = x.VideoCodec,
                        AudioCodec = x.AudioCodec,
                        AudioChannels = x.AudioChannels,
                        AudioSampleRate = x.AudioSampleRate,
                        OverallBitrateKbps = x.OverallBitrateKbps,
                        VideoBitrateKbps = x.VideoBitrateKbps,
                        AudioBitrateKbps = x.AudioBitrateKbps,
                        MediaProbeStatus = x.MediaProbeStatus,
                        MediaProbeError = x.MediaProbeError,
                        MediaProbedAt = x.MediaProbedAt,
                        ProtocolType = x.ProtocolType,
                        Username = x.Username,
                        Password = SecretProtector.Unprotect(x.PasswordEncrypted),
                        IsDefault = x.Id == movie.DefaultMediaFileId,
                        ResumePositionSeconds = resumePosition,
                        LastPlayedAt = lastPlayedAt,
                        LastPlayPositionSeconds = resumePosition,
                        Subtitles = subtitles ?? []
                    };
                })
            .ToList();

        var selectedMediaFileId = preferredMediaFileId.HasValue && sources.Any(x => x.MediaFileId == preferredMediaFileId.Value)
            ? preferredMediaFileId.Value
            : movie.DefaultMediaFileId.HasValue && sources.Any(x => x.MediaFileId == movie.DefaultMediaFileId.Value)
                ? movie.DefaultMediaFileId.Value
                : sources[0].MediaFileId;

        return new PlaybackSessionModel
        {
            MovieId = movie.Id,
            MovieTitle = movie.Title,
            DefaultMediaFileId = movie.DefaultMediaFileId,
            SelectedMediaFileId = selectedMediaFileId,
            Sources = sources
        };
    }

    public async Task SetPreferredSubtitleAsync(
        int mediaFileId,
        int? subtitleMediaFileId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var bindings = await dbContext.SubtitleBindings
            .Where(x => x.MediaFileId == mediaFileId)
            .ToListAsync(cancellationToken);

        foreach (var binding in bindings)
        {
            var isPreferred = subtitleMediaFileId.HasValue && binding.SubtitleMediaFileId == subtitleMediaFileId.Value;
            binding.IsAutoLoaded = isPreferred;
            binding.Priority = isPreferred ? 0 : Math.Max(binding.Priority, 10);
            binding.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static int ResolveSourceSpecificResume(IEnumerable<WatchHistoryProjection> histories, int? targetDurationSeconds)
    {
        var orderedHistories = histories
            .OrderByDescending(x => x.LastPlayedAt)
            .ToList();
        if (orderedHistories.FirstOrDefault()?.IsCompleted == true)
        {
            return 0;
        }

        foreach (var history in orderedHistories)
        {
            if (history.IsCompleted)
            {
                continue;
            }

            var position = history.LastPlayPositionSeconds;
            if (position <= 0)
            {
                continue;
            }

            if (targetDurationSeconds.HasValue && IsNearOrPastEnding(position, targetDurationSeconds.Value))
            {
                continue;
            }

            return position;
        }

        return 0;
    }

    private static UnifiedResumeProjection ResolveUnifiedResume(
        int movieId,
        IReadOnlyCollection<WatchHistoryProjection> histories,
        IReadOnlyDictionary<int, int?> sourceDurations)
    {
        var orderedHistories = histories
            .OrderByDescending(x => x.LastPlayedAt)
            .ToList();
        var latestHistory = orderedHistories.FirstOrDefault();
        if (latestHistory?.IsCompleted == true)
        {
            return UnifiedResumeProjection.Completed(latestHistory.MediaFileId, latestHistory.LastPlayedAt);
        }

        foreach (var history in orderedHistories)
        {
            if (history.IsCompleted || history.LastPlayPositionSeconds <= 0)
            {
                continue;
            }

            if (!sourceDurations.TryGetValue(history.MediaFileId, out var historyDurationSeconds)
                || !historyDurationSeconds.HasValue)
            {
                LogWatchHistory(
                    $"watch-history-unified-resume-skipped movieId={movieId} mediaFileId={history.MediaFileId} reason=missing-duration");
                continue;
            }

            if (IsNearOrPastEnding(history.LastPlayPositionSeconds, historyDurationSeconds.Value))
            {
                LogWatchHistory(
                    $"watch-history-unified-resume-skipped movieId={movieId} mediaFileId={history.MediaFileId} reason=invalid-position");
                continue;
            }

            LogWatchHistory(
                $"watch-history-unified-resume-selected movieId={movieId} resume={history.LastPlayPositionSeconds} fromMediaFileId={history.MediaFileId} reason=latest-compatible");
            return UnifiedResumeProjection.Active(
                history.MediaFileId,
                history.LastPlayPositionSeconds,
                historyDurationSeconds.Value,
                history.LastPlayedAt);
        }

        return UnifiedResumeProjection.None;
    }

    private static string? ResolveUnifiedResumeSkipReason(
        UnifiedResumeProjection unifiedResume,
        int targetMediaFileId,
        int? targetDurationSeconds)
    {
        if (unifiedResume.PositionSeconds <= 0)
        {
            return "no-valid-history";
        }

        if (!targetDurationSeconds.HasValue)
        {
            return "missing-duration";
        }

        if (!AreDurationsCompatible(unifiedResume.DurationSeconds, targetDurationSeconds.Value))
        {
            return "duration-incompatible";
        }

        if (IsNearOrPastEnding(unifiedResume.PositionSeconds, targetDurationSeconds.Value))
        {
            return "invalid-position";
        }

        return null;
    }

    private static bool AreDurationsCompatible(int historyDurationSeconds, int targetDurationSeconds)
    {
        if (historyDurationSeconds <= 0 || targetDurationSeconds <= 0)
        {
            return false;
        }

        var diffSeconds = Math.Abs(historyDurationSeconds - targetDurationSeconds);
        var diffRatio = diffSeconds / (double)Math.Max(historyDurationSeconds, targetDurationSeconds);
        return diffSeconds <= 60 || diffRatio <= 0.02d;
    }

    private static bool IsNearOrPastEnding(int positionSeconds, int durationSeconds)
    {
        return durationSeconds > 0 && positionSeconds >= durationSeconds - 30;
    }

    private static void LogWatchHistory(string message)
    {
        Debug.WriteLine("[WATCH-HISTORY] " + message);
    }

    private sealed class WatchHistoryProjection
    {
        public int MediaFileId { get; init; }

        public int LastPlayPositionSeconds { get; init; }

        public bool IsCompleted { get; init; }

        public DateTime StartedAt { get; init; }

        public DateTime? EndedAt { get; init; }

        public DateTime LastPlayedAt => EndedAt ?? StartedAt;
    }

    private readonly record struct UnifiedResumeProjection(
        int MediaFileId,
        int PositionSeconds,
        int DurationSeconds,
        DateTime? LastPlayedAt,
        bool IsCompleted)
    {
        public static UnifiedResumeProjection None { get; } = new(0, 0, 0, null, false);

        public static UnifiedResumeProjection Active(
            int mediaFileId,
            int positionSeconds,
            int durationSeconds,
            DateTime lastPlayedAt)
        {
            return new UnifiedResumeProjection(mediaFileId, positionSeconds, durationSeconds, lastPlayedAt, false);
        }

        public static UnifiedResumeProjection Completed(int mediaFileId, DateTime lastPlayedAt)
        {
            return new UnifiedResumeProjection(mediaFileId, 0, 0, lastPlayedAt, true);
        }
    }
}
