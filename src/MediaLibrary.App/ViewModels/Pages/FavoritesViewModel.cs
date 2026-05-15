using System.Collections.ObjectModel;
using System.Windows;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class FavoritesViewModel : PageViewModelBase
{
    private const string CollectionChangeSource = "Collection";
    private readonly IUserCollectionService _userCollectionService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IMovieManagementService _movieManagementService;
    private readonly INavigationStateService _navigationStateService;
    private readonly IDataRefreshService _dataRefreshService;
    private bool _isActive;
    private bool _isLoading;
    private string _statusMessage = "管理你喜爱和想看的影片。";

    public FavoritesViewModel(
        IUserCollectionService userCollectionService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IMovieManagementService movieManagementService,
        INavigationStateService navigationStateService,
        IDataRefreshService dataRefreshService)
        : base("收藏夹", "集中查看喜爱和想看的影片。")
    {
        _userCollectionService = userCollectionService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _movieManagementService = movieManagementService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;
        _dataRefreshService.DataChanged += OnDataChanged;

        OpenMovieCommand = new RelayCommand(OpenMovie);
        RemoveItemCommand = new AsyncRelayCommand(RemoveItemAsync, _ => !IsLoading);
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(), () => !IsLoading);
    }

    public ObservableCollection<FavoriteCollectionItemViewModel> FavoriteMovies { get; } = [];

    public ObservableCollection<FavoriteCollectionItemViewModel> WantToWatchMovies { get; } = [];

    public RelayCommand OpenMovieCommand { get; }

    public AsyncRelayCommand RemoveItemCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public override bool IsRefreshing => IsLoading;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsRefreshing));
                RefreshCommand.RaiseCanExecuteChanged();
                RemoveItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasFavoriteMovies => FavoriteMovies.Count > 0;

    public bool HasWantToWatchMovies => WantToWatchMovies.Count > 0;

    public string FavoriteCountText => $"{FavoriteMovies.Count} 部";

    public string WantToWatchCountText => $"{WantToWatchMovies.Count} 部";

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        _isActive = true;
        await LoadAsync(cancellationToken);
    }

    public override void Deactivate()
    {
        _isActive = false;
    }

    private void OnDataChanged(object? sender, AppDataChangedEventArgs e)
    {
        if (!_isActive || !ShouldRefreshForDataChange(e))
        {
            return;
        }

        _ = Application.Current.Dispatcher.InvokeAsync(() => _ = LoadAsync());
    }

    private static bool ShouldRefreshForDataChange(AppDataChangedEventArgs e)
    {
        return e.LibraryChanged
               || e.Reason is AppDataChangeReason.CollectionChanged
                   or AppDataChangeReason.MetadataChanged;
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            StatusMessage = "正在加载收藏夹。";
            var movieItems = await _userCollectionService.GetCollectionItemsAsync(cancellationToken);
            var tvItems = await _tvSeasonCollectionService.GetCollectionItemsAsync(cancellationToken);
            var items = movieItems.Concat(tvItems).ToList();
            ReplaceItems(FavoriteMovies, BuildTabItems(items.Where(x => x.IsLiked), FavoriteTabKind.Favorite));
            ReplaceItems(WantToWatchMovies, BuildTabItems(items.Where(x => x.IsWantToWatch), FavoriteTabKind.WantToWatch));
            RaiseCountsChanged();
            StatusMessage = BuildStatusMessage();
        }
        catch (Exception exception)
        {
            FavoriteMovies.Clear();
            WantToWatchMovies.Clear();
            RaiseCountsChanged();
            StatusMessage = $"加载收藏夹失败：{exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static IReadOnlyList<FavoriteCollectionItemViewModel> BuildTabItems(
        IEnumerable<CollectionMovieItem> items,
        FavoriteTabKind tabKind)
    {
        return items
            .GroupBy(BuildDeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.IsInLibrary).ThenByDescending(item => item.UpdatedAt).First())
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Title)
            .Select(item => new FavoriteCollectionItemViewModel(item, tabKind))
            .ToList();
    }

    private static string BuildDeduplicationKey(CollectionMovieItem item)
    {
        if (item.TvSeasonId.HasValue)
        {
            return $"season:{item.TvSeasonId.Value}";
        }

        if (item.TmdbId.HasValue)
        {
            return item.IsTvSeason
                ? $"tv-tmdb:{item.TmdbId.Value}:s{item.SeasonNumber}"
                : $"tmdb:{item.TmdbId.Value}";
        }

        if (item.MovieId.HasValue)
        {
            return $"movie:{item.MovieId.Value}";
        }

        return $"title:{item.Title.Trim().ToLowerInvariant()}:{item.ReleaseYear?.ToString() ?? string.Empty}";
    }

    private static void ReplaceItems(
        ObservableCollection<FavoriteCollectionItemViewModel> target,
        IEnumerable<FavoriteCollectionItemViewModel> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void RaiseCountsChanged()
    {
        OnPropertyChanged(nameof(HasFavoriteMovies));
        OnPropertyChanged(nameof(HasWantToWatchMovies));
        OnPropertyChanged(nameof(FavoriteCountText));
        OnPropertyChanged(nameof(WantToWatchCountText));
    }

    private string BuildStatusMessage()
    {
        var total = FavoriteMovies.Count + WantToWatchMovies.Count;
        return total == 0
            ? "当前收藏夹为空，可先在详情页标记喜爱或在推荐中加入想看。"
            : $"喜爱 {FavoriteMovies.Count} 部 · 想看 {WantToWatchMovies.Count} 部";
    }

    private async Task RemoveItemAsync(object? parameter)
    {
        if (parameter is not FavoriteCollectionItemViewModel item)
        {
            return;
        }

        try
        {
            if (item.TabKind == FavoriteTabKind.Favorite)
            {
                await RemoveFavoriteAsync(item);
            }
            else
            {
                await RemoveWantToWatchAsync(item);
            }

            _dataRefreshService.NotifyCollectionChanged();
            await LoadAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"{item.ActionText}失败：{exception.Message}";
        }
    }

    private async Task RemoveFavoriteAsync(FavoriteCollectionItemViewModel item)
    {
        if (item.IsTvSeason && item.TvSeasonId.HasValue)
        {
            await _tvSeasonCollectionService.SetFavoriteAsync(
                item.TvSeasonId.Value,
                isFavorite: false,
                changeSource: CollectionChangeSource);
            StatusMessage = $"已取消喜爱：{item.Title}";
            return;
        }

        if (!item.MovieId.HasValue)
        {
            StatusMessage = "库外喜爱暂未接入，无法取消喜爱。";
            return;
        }

        await _movieManagementService.SetFavoriteAsync(
            item.MovieId.Value,
            isFavorite: false,
            changeSource: CollectionChangeSource);
        StatusMessage = $"已取消喜爱：{item.Title}";
    }

    private async Task RemoveWantToWatchAsync(FavoriteCollectionItemViewModel item)
    {
        if (item.IsTvSeason && item.TvSeasonId.HasValue)
        {
            await _tvSeasonCollectionService.SetWantToWatchAsync(
                item.TvSeasonId.Value,
                isWantToWatch: false,
                changeSource: CollectionChangeSource);
            StatusMessage = $"已取消想看：{item.Title}";
            return;
        }

        await _userCollectionService.RemoveWantToWatchAsync(
            ToRecommendation(item, keepMovieId: true),
            changeSource: CollectionChangeSource);
        StatusMessage = $"已取消想看：{item.Title}";
    }

    private void OpenMovie(object? parameter)
    {
        if (parameter is not FavoriteCollectionItemViewModel movie)
        {
            return;
        }

        if (!movie.IsTvSeason && movie.MovieId is > 0)
        {
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, movie.MovieId.Value);
            return;
        }

        if (movie.IsTvSeason && movie.TvSeasonId.HasValue)
        {
            _navigationStateService.RequestTvSeasonDetail(movie.TvSeasonId.Value);
            return;
        }

        _navigationStateService.RequestExternalMovieDetail(ToRecommendation(movie, keepMovieId: false));
    }

    private static AiRecommendationItem ToRecommendation(
        FavoriteCollectionItemViewModel movie,
        bool keepMovieId)
    {
        return new AiRecommendationItem
        {
            MovieId = keepMovieId && movie.MovieId.HasValue ? movie.MovieId.Value : 0,
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
            IsInLibrary = movie.IsInLibrary,
            IsWatched = movie.IsWatched,
            IsWantToWatch = movie.IsWantToWatch,
            IsNotInterested = movie.IsNotInterested,
            ScopeText = movie.TabKind == FavoriteTabKind.WantToWatch ? "想看影片" : "喜爱影片",
            AvailabilityText = movie.AvailabilityText,
            WatchStateText = movie.WatchStateText
        };
    }

    public enum FavoriteTabKind
    {
        Favorite,
        WantToWatch
    }

    public sealed class FavoriteCollectionItemViewModel
    {
        private readonly CollectionMovieItem _item;

        public FavoriteCollectionItemViewModel(CollectionMovieItem item, FavoriteTabKind tabKind)
        {
            _item = item;
            TabKind = tabKind;
        }

        public FavoriteTabKind TabKind { get; }

        public int? MovieId => _item.MovieId;

        public bool IsTvSeason => _item.IsTvSeason;

        public int? TvSeasonId => _item.TvSeasonId;

        public int? TmdbId => _item.TmdbId;

        public string Title => _item.Title;

        public string OriginalTitle => _item.OriginalTitle;

        public int? ReleaseYear => _item.ReleaseYear;

        public string PosterRemoteUrl => _item.PosterRemoteUrl;

        public string Overview => _item.Overview;

        public string GenresText => _item.GenresText;

        public string AiTagsText => _item.AiTagsText;

        public string EmotionTagsText => _item.EmotionTagsText;

        public string SceneTagsText => _item.SceneTagsText;

        public string Country => _item.Country;

        public string Language => _item.Language;

        public int? RuntimeMinutes => _item.RuntimeMinutes;

        public string ImdbId => _item.ImdbId;

        public double? TmdbRating => _item.TmdbRating;

        public int? TmdbVoteCount => _item.TmdbVoteCount;

        public double? OmdbScoreValue => _item.OmdbScoreValue;

        public double? OmdbScoreScale => _item.OmdbScoreScale;

        public int? OmdbVoteCount => _item.OmdbVoteCount;

        public string OmdbSourceUrl => _item.OmdbSourceUrl;

        public DateTime? OmdbLastUpdatedAt => _item.OmdbLastUpdatedAt;

        public bool IsLiked => _item.IsLiked;

        public bool IsWantToWatch => _item.IsWantToWatch;

        public bool IsWatched => _item.IsWatched;

        public bool IsNotInterested => _item.IsNotInterested;

        public bool IsInLibrary => _item.IsInLibrary;

        public string CollectionTypeText => TabKind == FavoriteTabKind.Favorite ? "喜爱" : "想看";

        public string AvailabilityText => _item.AvailabilityText;

        public string WatchStateText => _item.WatchStateText;

        public string DetailButtonText => _item.DetailButtonText;

        public string ActionText => TabKind == FavoriteTabKind.Favorite ? "取消喜爱" : "取消想看";

        public bool CanRemove => IsTvSeason || TabKind != FavoriteTabKind.Favorite || MovieId.HasValue;
    }
}
