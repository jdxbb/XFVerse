using MediaLibrary.App.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Services.Implementations;

public sealed class NavigationRequest : EventArgs
{
    public NavigationRequest(
        NavigationPageKey pageKey,
        int? movieId = null,
        AiRecommendationItem? externalRecommendation = null,
        DateTime? targetDate = null,
        int? tvSeriesId = null,
        int? tvSeasonId = null,
        int? tvEpisodeId = null)
    {
        PageKey = pageKey;
        MovieId = movieId;
        ExternalRecommendation = externalRecommendation;
        TargetDate = targetDate;
        TvSeriesId = tvSeriesId;
        TvSeasonId = tvSeasonId;
        TvEpisodeId = tvEpisodeId;
    }

    public NavigationPageKey PageKey { get; }

    public int? MovieId { get; }

    public AiRecommendationItem? ExternalRecommendation { get; }

    public DateTime? TargetDate { get; }

    public int? TvSeriesId { get; }

    public int? TvSeasonId { get; }

    public int? TvEpisodeId { get; }
}
