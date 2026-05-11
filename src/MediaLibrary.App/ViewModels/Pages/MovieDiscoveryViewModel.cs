namespace MediaLibrary.App.ViewModels.Pages;

public sealed class MovieDiscoveryViewModel : PageViewModelBase
{
    private const int SearchTabIndex = 0;
    private const int AiRecommendationTabIndex = 2;
    private int _selectedTabIndex = SearchTabIndex;
    private bool _hasActivatedAiRecommendations;

    public MovieDiscoveryViewModel(RecommendationsViewModel aiRecommendationViewModel)
        : base("影片发现", "搜索、榜单和 AI 推荐集中在这里，当前先承接 AI 推荐。")
    {
        AiRecommendationViewModel = aiRecommendationViewModel;
    }

    public RecommendationsViewModel AiRecommendationViewModel { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value) && value == AiRecommendationTabIndex)
            {
                _ = EnsureAiRecommendationsActivatedAsync();
            }
        }
    }

    public override Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        SelectedTabIndex = SearchTabIndex;
        return Task.CompletedTask;
    }

    private async Task EnsureAiRecommendationsActivatedAsync()
    {
        if (_hasActivatedAiRecommendations)
        {
            return;
        }

        _hasActivatedAiRecommendations = true;
        try
        {
            await AiRecommendationViewModel.ActivateAsync();
        }
        catch
        {
            _hasActivatedAiRecommendations = false;
        }
    }
}
