using System.Collections.ObjectModel;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class FavoritesViewModel : PageViewModelBase
{
    private readonly IUserCollectionService _userCollectionService;
    private readonly INavigationStateService _navigationStateService;
    private readonly List<CollectionMovieItem> _allItems = [];
    private string _selectedCategory = "全部";
    private string _statusMessage = "展示你标记为喜爱或想看的影片。";

    public FavoritesViewModel(
        IUserCollectionService userCollectionService,
        INavigationStateService navigationStateService)
        : base("收藏夹", "集中查看喜爱和想看的影片。")
    {
        _userCollectionService = userCollectionService;
        _navigationStateService = navigationStateService;
        OpenMovieCommand = new RelayCommand(OpenMovie);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
    }

    public ObservableCollection<CollectionMovieItem> Movies { get; } = [];

    public IReadOnlyList<string> CategoryOptions { get; } = ["全部", "喜爱", "想看"];

    public RelayCommand OpenMovieCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilter();
            }
        }
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _allItems.Clear();
            _allItems.AddRange(await _userCollectionService.GetCollectionItemsAsync(cancellationToken));
            ApplyFilter();
        }
        catch (Exception exception)
        {
            Movies.Clear();
            StatusMessage = $"加载收藏夹失败：{exception.Message}";
        }
    }

    private void ApplyFilter()
    {
        var filtered = SelectedCategory switch
        {
            "喜爱" => _allItems.Where(x => x.IsLiked),
            "想看" => _allItems.Where(x => x.IsWantToWatch),
            _ => _allItems
        };

        Movies.Clear();
        foreach (var movie in filtered.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Title))
        {
            Movies.Add(movie);
        }

        StatusMessage = Movies.Count == 0
            ? SelectedCategory switch
            {
                "喜爱" => "当前还没有喜爱影片，可在详情页点击“喜爱”。",
                "想看" => "当前还没有想看影片，可在 AI 推荐页点击“+ 想看”。",
                _ => "当前收藏夹为空，可先标记喜爱或加入想看。"
            }
            : $"当前显示 {Movies.Count} 部{(SelectedCategory == "全部" ? "收藏夹" : SelectedCategory)}影片。";
    }

    private void OpenMovie(object? parameter)
    {
        if (parameter is not CollectionMovieItem movie)
        {
            return;
        }

        if (movie.IsInLibrary && movie.MovieId.HasValue)
        {
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, movie.MovieId.Value);
            return;
        }

        _navigationStateService.RequestExternalMovieDetail(
            new AiRecommendationItem
            {
                MovieId = 0,
                TmdbId = movie.TmdbId,
                Title = movie.Title,
                OriginalTitle = movie.OriginalTitle,
                ReleaseYear = movie.ReleaseYear,
                PosterRemoteUrl = movie.PosterRemoteUrl,
                Overview = movie.Overview,
                Country = movie.Country,
                Language = movie.Language,
                RuntimeMinutes = movie.RuntimeMinutes,
                ImdbId = movie.ImdbId,
                TmdbRating = movie.TmdbRating,
                TmdbVoteCount = movie.TmdbVoteCount,
                OmdbRating = movie.OmdbScoreValue.HasValue
                    ? new MovieRatingItem
                    {
                        SourceName = "OMDb",
                        ScoreValue = movie.OmdbScoreValue.Value,
                        ScoreScale = movie.OmdbScoreScale ?? 10d,
                        VoteCount = movie.OmdbVoteCount,
                        SourceUrl = movie.OmdbSourceUrl,
                        LastUpdatedAt = movie.OmdbLastUpdatedAt ?? DateTime.UtcNow
                    }
                    : null,
                Tags = string.IsNullOrWhiteSpace(movie.AiTagsText) ? movie.GenresText : movie.AiTagsText,
                EmotionTagsText = movie.EmotionTagsText,
                SceneTagsText = movie.SceneTagsText,
                IsInLibrary = false,
                IsWatched = movie.IsWatched,
                IsWantToWatch = movie.IsWantToWatch,
                ScopeText = "想看影片",
                AvailabilityText = "未入库",
                WatchStateText = movie.WatchStateText
            });
    }
}
