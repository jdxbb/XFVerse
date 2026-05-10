using MediaLibrary.App.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Services.Implementations;

public sealed class NavigationRequest : EventArgs
{
    public NavigationRequest(
        NavigationPageKey pageKey,
        int? movieId = null,
        AiRecommendationItem? externalRecommendation = null)
    {
        PageKey = pageKey;
        MovieId = movieId;
        ExternalRecommendation = externalRecommendation;
    }

    public NavigationPageKey PageKey { get; }

    public int? MovieId { get; }

    public AiRecommendationItem? ExternalRecommendation { get; }
}
