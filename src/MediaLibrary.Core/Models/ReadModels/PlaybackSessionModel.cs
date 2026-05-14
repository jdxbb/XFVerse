using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class PlaybackSessionModel
{
    public PlaybackContentType ContentType { get; set; } = PlaybackContentType.Movie;

    public int MovieId { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public int? EpisodeId { get; set; }

    public int? TvSeasonId { get; set; }

    public int? TvSeriesId { get; set; }

    public int SeasonNumber { get; set; }

    public int EpisodeNumber { get; set; }

    public string SeriesTitle { get; set; } = string.Empty;

    public string SeasonTitle { get; set; } = string.Empty;

    public string EpisodeTitle { get; set; } = string.Empty;

    public PlaybackEpisodeNavigationItem? PreviousEpisode { get; set; }

    public PlaybackEpisodeNavigationItem? NextEpisode { get; set; }

    public int? DefaultMediaFileId { get; set; }

    public int SelectedMediaFileId { get; set; }

    public IReadOnlyList<PlaybackSourceItem> Sources { get; set; } = [];

    public string DisplayTitle => ContentType == PlaybackContentType.Episode
        ? BuildEpisodeDisplayTitle()
        : MovieTitle;

    private string BuildEpisodeDisplayTitle()
    {
        var episodeLabel = SeasonNumber > 0 && EpisodeNumber > 0
            ? $"S{SeasonNumber:D2}E{EpisodeNumber:D2}"
            : EpisodeNumber > 0
                ? $"E{EpisodeNumber:D2}"
                : string.Empty;
        var parts = new[] { SeriesTitle, episodeLabel, EpisodeTitle }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" ", parts);
    }
}

public sealed class PlaybackEpisodeNavigationItem
{
    public int EpisodeId { get; set; }

    public int EpisodeNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool HasPlayableSource { get; set; }

    public string DisplayText => string.IsNullOrWhiteSpace(Title)
        ? $"E{EpisodeNumber:D2}"
        : $"E{EpisodeNumber:D2} {Title}";
}
