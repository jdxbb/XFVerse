using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.Services.Implementations;

public sealed class NavigationStateService : INavigationStateService
{
    public int? SelectedMovieId { get; private set; }

    public AiRecommendationItem? SelectedExternalRecommendation { get; private set; }

    public event EventHandler<NavigationRequest>? NavigationRequested;

    public void RequestNavigation(NavigationPageKey pageKey, int? movieId = null)
    {
        SelectedMovieId = movieId;
        SelectedExternalRecommendation = null;
        NavigationRequested?.Invoke(this, new NavigationRequest(pageKey, movieId));
    }

    public void RequestExternalMovieDetail(AiRecommendationItem recommendation)
    {
        SelectedMovieId = null;
        SelectedExternalRecommendation = recommendation;
        NavigationRequested?.Invoke(this, new NavigationRequest(NavigationPageKey.MovieDetail, externalRecommendation: recommendation));
    }
}
