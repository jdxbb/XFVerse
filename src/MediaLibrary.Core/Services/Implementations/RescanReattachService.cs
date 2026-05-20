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

public sealed class RescanReattachService : IRescanReattachService
{
    public async Task<RescanReattachResult> TryReattachAsync(
        IReadOnlyCollection<int> mediaFileIds,
        string sourceKind,
        CancellationToken cancellationToken = default)
    {
        var ids = mediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var result = new RescanReattachResult();
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

        foreach (var mediaFile in mediaFiles)
        {
            if (!IsEligibleUnboundVideo(mediaFile, out var skipReason))
            {
                WriteSkipped(sourceKind, mediaFile, "none", null, null, skipReason, fallbackToPlaceholderGrouping: false);
                continue;
            }

            result.CandidateCount++;
            var parseResult = TvEpisodeFileNameParser.Parse(
                mediaFile.FileName,
                allowSeasonContextOnly: true,
                seasonNumberHint: ResolveDirectorySeasonNumber(mediaFile.FilePath),
                allowStrongContextFallbacks: true);
            var episodeSkipReason = GetEpisodeCandidateSkipReason(mediaFile.FileName, parseResult);
            if (string.IsNullOrWhiteSpace(episodeSkipReason))
            {
                result.EpisodeCandidateCount++;
                WriteCandidate(sourceKind, mediaFile, "episode", parseResult.SeasonNumber, parseResult.EpisodeNumber);
                var attached = await TryAttachEpisodeAsync(dbContext, mediaFile, parseResult, sourceKind, cancellationToken);
                if (attached)
                {
                    result.SucceededCount++;
                    continue;
                }

                result.SkippedCount++;
                result.PlaceholderFallbackCount++;
                continue;
            }

            if (IsMovieReattachCandidate(mediaFile.FileName, parseResult))
            {
                result.MovieCandidateCount++;
                result.SkippedCount++;
                result.PlaceholderFallbackCount++;
                WriteSkipped(sourceKind, mediaFile, "movie", null, null, "movie-reattach-deferred", fallbackToPlaceholderGrouping: true);
                continue;
            }

            result.SkippedCount++;
            result.PlaceholderFallbackCount++;
            WriteSkipped(
                sourceKind,
                mediaFile,
                "episode",
                parseResult.SeasonNumber > 0 ? parseResult.SeasonNumber : null,
                parseResult.EpisodeNumber > 0 ? parseResult.EpisodeNumber : null,
                episodeSkipReason,
                fallbackToPlaceholderGrouping: true);
        }

        WriteSummary(sourceKind, result);
        return result;
    }

    private static bool IsEligibleUnboundVideo(MediaFile mediaFile, out string skipReason)
    {
        if (mediaFile.MediaType != MediaType.Video)
        {
            skipReason = "not-video";
            return false;
        }

        if (mediaFile.IsDeleted)
        {
            skipReason = "deleted-source";
            return false;
        }

        if (mediaFile.EpisodeId.HasValue)
        {
            skipReason = "already-bound-episode";
            return false;
        }

        if (mediaFile.MovieId.HasValue
            && mediaFile.Movie?.IdentificationStatus != IdentificationStatus.Failed)
        {
            skipReason = "already-bound-stable-or-pending-movie";
            return false;
        }

        skipReason = string.Empty;
        return true;
    }

    private static string GetEpisodeCandidateSkipReason(string fileName, TvEpisodeFileNameParseResult parseResult)
    {
        if (ContainsSpecialEpisodeToken(fileName))
        {
            return "special-episode-token";
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

    private static async Task<bool> TryAttachEpisodeAsync(
        AppDbContext dbContext,
        MediaFile mediaFile,
        TvEpisodeFileNameParseResult parseResult,
        string sourceKind,
        CancellationToken cancellationToken)
    {
        var seasonNumber = Math.Max(1, parseResult.SeasonNumber);
        var episodeNumber = parseResult.EpisodeNumber;
        var candidateSeasons = await dbContext.TvSeasons
            .Include(x => x.Series)
            .Include(x => x.Episodes)
            .ThenInclude(x => x.MediaFiles)
            .Where(
                x => x.SeasonNumber == seasonNumber
                     && (x.IdentificationStatus == IdentificationStatus.Matched
                         || x.IdentificationStatus == IdentificationStatus.ManualConfirmed)
                     && x.Series != null
                     && x.Series.TmdbSeriesId.HasValue
                     && x.Episodes.Any(episode => episode.EpisodeNumber == episodeNumber))
            .ToListAsync(cancellationToken);

        var safeSeasons = candidateSeasons
            .Where(season => HasSafeEpisodeSourceContext(season, mediaFile, episodeNumber))
            .ToList();
        if (safeSeasons.Count != 1)
        {
            var directorySkipContext = BuildDirectorySkipContext(candidateSeasons, mediaFile, episodeNumber);
            var skippedReason = safeSeasons.Count == 0
                ? directorySkipContext.HasDifferentDirectorySource
                    ? "recognized-reattach-requires-same-directory"
                    : "no-safe-existing-season-context"
                : "multiple-safe-season-contexts";
            WriteSkipped(
                sourceKind,
                mediaFile,
                "episode",
                seasonNumber,
                episodeNumber,
                skippedReason,
                fallbackToPlaceholderGrouping: true,
                existingSeasonCandidates: candidateSeasons.Count,
                existingSeriesCandidates: candidateSeasons.Select(x => x.TvSeriesId).Distinct().Count(),
                candidateDirectoryHash: directorySkipContext.CandidateDirectoryHash,
                existingDirectoryHash: directorySkipContext.ExistingDirectoryHash);
            return false;
        }

        var targetSeason = safeSeasons[0];
        var targetEpisode = targetSeason.Episodes.FirstOrDefault(x => x.EpisodeNumber == episodeNumber);
        if (targetEpisode is null)
        {
            WriteSkipped(
                sourceKind,
                mediaFile,
                "episode",
                seasonNumber,
                episodeNumber,
                "target-episode-missing",
                fallbackToPlaceholderGrouping: true,
                existingSeasonCandidates: 1,
                existingSeriesCandidates: 1);
            return false;
        }

        var now = DateTime.UtcNow;
        var oldMovieId = mediaFile.MovieId;
        var defaultOwners = await dbContext.Movies
            .Where(x => x.DefaultMediaFileId == mediaFile.Id)
            .ToListAsync(cancellationToken);
        foreach (var defaultOwner in defaultOwners)
        {
            defaultOwner.DefaultMediaFileId = null;
            defaultOwner.UpdatedAt = now;
        }

        mediaFile.MovieId = null;
        mediaFile.Movie = null;
        mediaFile.EpisodeId = targetEpisode.Id;
        mediaFile.Episode = targetEpisode;
        mediaFile.UpdatedAt = now;
        targetEpisode.UpdatedAt = now;
        targetSeason.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (oldMovieId.HasValue)
        {
            await CleanupMovieIfOrphanedAsync(dbContext, oldMovieId.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        ScanIdentificationDiagnostics.Write(
            $"event=rescan-reattach-succeeded sourceKind={FormatValue(sourceKind)} targetKind=episode mediaFileId={mediaFile.Id} targetEpisodeId={targetEpisode.Id} parsedSeason={seasonNumber} parsedEpisode={episodeNumber} existingSeasonCandidates=1 existingSeriesCandidates=1 fallbackToPlaceholderGrouping=false");
        return true;
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

    private static bool HasSafeEpisodeSourceContext(TvSeason season, MediaFile mediaFile, int episodeNumber)
    {
        var candidateDirectory = NormalizeDirectoryPath(GetDirectoryPath(mediaFile.FilePath));
        return season.Episodes
            .Where(episode => episode.EpisodeNumber == episodeNumber)
            .SelectMany(x => x.MediaFiles)
            .Any(
                source => source.Id != mediaFile.Id
                          && source.SourceConnectionId == mediaFile.SourceConnectionId
                          && source.MediaType == MediaType.Video
                          && !source.IsDeleted
                          && IsSameDirectory(candidateDirectory, GetDirectoryPath(source.FilePath)));
    }

    private static ReattachDirectorySkipContext BuildDirectorySkipContext(IEnumerable<TvSeason> candidateSeasons, MediaFile mediaFile, int episodeNumber)
    {
        var candidateDirectory = NormalizeDirectoryPath(GetDirectoryPath(mediaFile.FilePath));
        string? existingDirectory = null;
        foreach (var source in candidateSeasons
                     .SelectMany(season => season.Episodes)
                     .Where(episode => episode.EpisodeNumber == episodeNumber)
                     .SelectMany(episode => episode.MediaFiles))
        {
            if (source.Id == mediaFile.Id
                || source.SourceConnectionId != mediaFile.SourceConnectionId
                || source.MediaType != MediaType.Video
                || source.IsDeleted)
            {
                continue;
            }

            existingDirectory = NormalizeDirectoryPath(GetDirectoryPath(source.FilePath));
            if (IsSameDirectory(candidateDirectory, existingDirectory))
            {
                return new ReattachDirectorySkipContext(
                    HashDirectoryPath(candidateDirectory),
                    HashDirectoryPath(existingDirectory),
                    HasDifferentDirectorySource: false);
            }
        }

        return new ReattachDirectorySkipContext(
            HashDirectoryPath(candidateDirectory),
            existingDirectory is null ? null : HashDirectoryPath(existingDirectory),
            HasDifferentDirectorySource: existingDirectory is not null);
    }

    private static bool IsSameDirectory(string leftDirectory, string rightDirectory)
    {
        return string.Equals(
            NormalizeDirectoryPath(leftDirectory),
            NormalizeDirectoryPath(rightDirectory),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int? ResolveDirectorySeasonNumber(string filePath)
    {
        var directory = GetDirectoryPath(filePath);
        var parent = Path.GetFileName(directory.Replace('\\', '/').TrimEnd('/'));
        var seasonNumber = TvEpisodeFileNameParser.TryParseSeasonNumber(parent);
        if (seasonNumber.HasValue)
        {
            return seasonNumber.Value;
        }

        var grandParentPath = GetDirectoryPath(directory);
        var grandParent = Path.GetFileName(grandParentPath.Replace('\\', '/').TrimEnd('/'));
        return TvEpisodeFileNameParser.TryParseSeasonNumber(grandParent);
    }

    private static bool IsMovieReattachCandidate(string fileName, TvEpisodeFileNameParseResult parseResult)
    {
        if (parseResult.IsEpisodeLike || parseResult.IsMultiEpisode || ContainsSpecialEpisodeToken(fileName))
        {
            return false;
        }

        var parsed = MovieFileNameParser.Parse(fileName);
        return !string.IsNullOrWhiteSpace(parsed.CleanTitle)
               && parsed.CleanTitle.Length >= 2;
    }

    private static bool ContainsSpecialEpisodeToken(string fileName)
    {
        var text = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        var tokens = text
            .Replace('.', ' ')
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace('[', ' ')
            .Replace(']', ' ')
            .Replace('(', ' ')
            .Replace(')', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return text.Contains("ova", StringComparison.Ordinal)
               || text.Contains("oad", StringComparison.Ordinal)
               || tokens.Contains("sp", StringComparer.OrdinalIgnoreCase)
               || text.Contains("剧场版", StringComparison.Ordinal)
               || text.Contains("劇場版", StringComparison.Ordinal)
               || text.Contains("特别篇", StringComparison.Ordinal)
               || text.Contains("特別篇", StringComparison.Ordinal)
               || text.Contains("special", StringComparison.Ordinal)
               || text.Contains("番外", StringComparison.Ordinal);
    }

    private static string GetDirectoryPath(string path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/');
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        return lastSeparatorIndex <= 0 ? "/" : normalized[..lastSeparatorIndex];
    }

    private static string NormalizeDirectoryPath(string? path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/').Trim();
        while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        return string.IsNullOrWhiteSpace(normalized) ? "/" : normalized;
    }

    private static string HashDirectoryPath(string? path)
    {
        var normalized = NormalizeDirectoryPath(path).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))
            .ToLowerInvariant();
        return hash[..12];
    }

    private static void WriteCandidate(
        string sourceKind,
        MediaFile mediaFile,
        string targetKind,
        int? parsedSeason,
        int? parsedEpisode)
    {
        var candidateDirectoryHash = HashDirectoryPath(GetDirectoryPath(mediaFile.FilePath));
        ScanIdentificationDiagnostics.Write(
            $"event=rescan-reattach-candidate sourceKind={FormatValue(sourceKind)} targetKind={targetKind} mediaFileId={mediaFile.Id} protocol={FormatValue(mediaFile.SourceConnection?.ProtocolType.ToString())} parsedSeason={FormatNullable(parsedSeason)} parsedEpisode={FormatNullable(parsedEpisode)} candidateDirectoryHash={FormatValue(candidateDirectoryHash)}");
    }

    private static void WriteSkipped(
        string sourceKind,
        MediaFile mediaFile,
        string targetKind,
        int? parsedSeason,
        int? parsedEpisode,
        string skippedReason,
        bool fallbackToPlaceholderGrouping,
        int existingSeasonCandidates = 0,
        int existingSeriesCandidates = 0,
        string? candidateDirectoryHash = null,
        string? existingDirectoryHash = null)
    {
        var effectiveCandidateDirectoryHash = candidateDirectoryHash ?? HashDirectoryPath(GetDirectoryPath(mediaFile.FilePath));
        var existingDirectorySegment = string.IsNullOrWhiteSpace(existingDirectoryHash)
            ? string.Empty
            : $" existingDirectoryHash={FormatValue(existingDirectoryHash)}";
        ScanIdentificationDiagnostics.Write(
            $"event=rescan-reattach-skipped sourceKind={FormatValue(sourceKind)} targetKind={targetKind} mediaFileId={mediaFile.Id} protocol={FormatValue(mediaFile.SourceConnection?.ProtocolType.ToString())} parsedSeason={FormatNullable(parsedSeason)} parsedEpisode={FormatNullable(parsedEpisode)} existingSeriesCandidates={existingSeriesCandidates} existingSeasonCandidates={existingSeasonCandidates} candidateDirectoryHash={FormatValue(effectiveCandidateDirectoryHash)}{existingDirectorySegment} skippedReason={FormatValue(skippedReason)} fallbackToPlaceholderGrouping={fallbackToPlaceholderGrouping.ToString().ToLowerInvariant()}");
    }

    private static void WriteSummary(string sourceKind, RescanReattachResult result)
    {
        ScanIdentificationDiagnostics.Write(
            $"event=rescan-reattach-summary sourceKind={FormatValue(sourceKind)} candidateCount={result.CandidateCount} episodeCandidateCount={result.EpisodeCandidateCount} movieCandidateCount={result.MovieCandidateCount} succeeded={result.SucceededCount} skipped={result.SkippedCount} placeholderFallbackCount={result.PlaceholderFallbackCount}");
    }

    private static string FormatNullable(int? value)
    {
        return value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "(none)";
    }

    private static string FormatValue(string? value)
    {
        return ScanIdentificationDiagnostics.FormatValue(value ?? string.Empty);
    }

    private readonly record struct ReattachDirectorySkipContext(
        string CandidateDirectoryHash,
        string? ExistingDirectoryHash,
        bool HasDifferentDirectorySource);
}
