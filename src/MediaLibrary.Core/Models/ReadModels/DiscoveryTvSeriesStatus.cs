using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class DiscoveryTvSeriesStatus
{
    public int TmdbSeriesId { get; set; }

    public int? TvSeriesId { get; set; }

    public bool IsInLibrary { get; set; }

    public bool IsVisibleInLibrary { get; set; }

    public bool HasHiddenSeason { get; set; }

    public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

    public int InLibrarySeasonCount { get; set; }

    public bool IsWatched { get; set; }

    public bool HasWantToWatchSeason { get; set; }

    public bool HasFavoriteSeason { get; set; }

    public bool HasNotInterestedSeason { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public int? FirstAirYear { get; set; }

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string DirectorText { get; set; } = string.Empty;

    public string ActorsText { get; set; } = string.Empty;

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public double? OmdbScoreValue { get; set; }

    public double? OmdbScoreScale { get; set; }

    public int? OmdbVoteCount { get; set; }

    public string OmdbSourceUrl { get; set; } = string.Empty;

    public DateTime? OmdbLastUpdatedAt { get; set; }
}
