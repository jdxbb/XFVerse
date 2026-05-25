using MediaLibrary.Core.Data;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class SubtitleBindingService : ISubtitleBindingService
{
    public async Task RebuildBindingsAsync(
        int sourceConnectionId,
        IReadOnlyCollection<int> videoMediaFileIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = videoMediaFileIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (distinctIds.Length == 0)
        {
            return;
        }

        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create());

        var videos = await dbContext.MediaFiles
            .Where(
                x => distinctIds.Contains(x.Id)
                     && x.SourceConnectionId == sourceConnectionId
                     && !x.IsDeleted
                     && x.MediaType == MediaType.Video)
            .ToListAsync(cancellationToken);

        if (videos.Count == 0)
        {
            return;
        }

        var directoryPaths = videos
            .Select(video => GetDirectoryPath(video.FilePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var subtitles = await dbContext.MediaFiles
            .Where(
                x => x.SourceConnectionId == sourceConnectionId
                     && !x.IsDeleted
                     && x.MediaType == MediaType.Subtitle)
            .ToListAsync(cancellationToken);

        subtitles = subtitles
            .Where(subtitle => directoryPaths.Contains(GetDirectoryPath(subtitle.FilePath), StringComparer.OrdinalIgnoreCase))
            .ToList();

        var existingBindings = await dbContext.SubtitleBindings
            .Where(binding => distinctIds.Contains(binding.MediaFileId))
            .ToListAsync(cancellationToken);
        var preferredSubtitleByVideo = existingBindings
            .Where(binding => binding.Priority == 0)
            .GroupBy(binding => binding.MediaFileId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(binding => binding.UpdatedAt)
                    .Select(binding => binding.SubtitleMediaFileId)
                    .First());

        if (existingBindings.Count > 0)
        {
            dbContext.SubtitleBindings.RemoveRange(existingBindings);
        }

        var preferredPreservedCount = 0;
        foreach (var video in videos)
        {
            var videoDirectory = GetDirectoryPath(video.FilePath);
            var videoBaseName = Path.GetFileNameWithoutExtension(video.FileName);
            var normalizedVideoBaseName = NormalizeMediaBaseName(video.FileName);
            var sameDirectorySubtitles = subtitles
                .Where(subtitle => string.Equals(
                    GetDirectoryPath(subtitle.FilePath),
                    videoDirectory,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(subtitle => subtitle.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var priority = 1;
            var preferredSubtitleMediaFileId = preferredSubtitleByVideo.GetValueOrDefault(video.Id);
            var preferredPreserved = false;
            foreach (var subtitle in sameDirectorySubtitles
                         .Where(subtitle => IsSameNameSubtitle(videoBaseName, normalizedVideoBaseName, subtitle.FileName)))
            {
                var isPreferred = preferredSubtitleMediaFileId == subtitle.Id;
                preferredPreserved |= isPreferred;
                dbContext.SubtitleBindings.Add(
                    CreateBinding(
                        video.Id,
                        subtitle,
                        SubtitleMatchType.SameName,
                        true,
                        isPreferred ? 0 : priority++));
            }

            foreach (var subtitle in sameDirectorySubtitles
                         .Where(subtitle => !IsSameNameSubtitle(videoBaseName, normalizedVideoBaseName, subtitle.FileName)))
            {
                var isPreferred = preferredSubtitleMediaFileId == subtitle.Id;
                preferredPreserved |= isPreferred;
                dbContext.SubtitleBindings.Add(
                    CreateBinding(
                        video.Id,
                        subtitle,
                        SubtitleMatchType.SameFolder,
                        isPreferred,
                        isPreferred ? 0 : priority++));
            }

            if (preferredPreserved)
            {
                preferredPreservedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        ScanIdentificationDiagnostics.Write(
            $"event=subtitle-binding-rebuild-completed sourceConnectionId={sourceConnectionId} videoCount={videos.Count} removedBindingCount={existingBindings.Count} preferredPreservedCount={preferredPreservedCount}");
    }

    private static bool IsSameNameSubtitle(
        string videoBaseName,
        string normalizedVideoBaseName,
        string subtitleFileName)
    {
        var subtitleBaseName = Path.GetFileNameWithoutExtension(subtitleFileName);
        return string.Equals(subtitleBaseName, videoBaseName, StringComparison.OrdinalIgnoreCase)
               || string.Equals(NormalizeMediaBaseName(subtitleFileName), normalizedVideoBaseName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMediaBaseName(string fileName)
    {
        var parsed = Helpers.MovieFileNameParser.Parse(fileName);
        return string.IsNullOrWhiteSpace(parsed.CleanTitle)
            ? Path.GetFileNameWithoutExtension(fileName).Trim()
            : parsed.ReleaseYear.HasValue
                ? $"{parsed.CleanTitle} {parsed.ReleaseYear.Value}"
                : parsed.CleanTitle;
    }

    private static SubtitleBinding CreateBinding(
        int mediaFileId,
        MediaFile subtitle,
        SubtitleMatchType matchType,
        bool isAutoLoaded,
        int priority)
    {
        return new SubtitleBinding
        {
            MediaFileId = mediaFileId,
            SubtitleMediaFileId = subtitle.Id,
            MatchType = matchType,
            IsAutoLoaded = isAutoLoaded,
            Priority = priority,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static string GetDirectoryPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var lastSeparatorIndex = normalized.LastIndexOf('/');
        if (lastSeparatorIndex <= 0)
        {
            return "/";
        }

        return normalized[..lastSeparatorIndex];
    }
}
