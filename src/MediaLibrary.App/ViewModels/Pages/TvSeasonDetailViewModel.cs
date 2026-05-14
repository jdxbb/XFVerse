using System.Collections.ObjectModel;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class TvSeasonDetailViewModel : PageViewModelBase
{
    private readonly INavigationStateService _navigationStateService;
    private readonly ITvDetailQueryService _tvDetailQueryService;
    private readonly IPlayerWindowService _playerWindowService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IDataRefreshService _dataRefreshService;
    private int? _seasonId;
    private int? _seriesId;
    private string _seriesName = "-";
    private string _name = "未选择电视剧季";
    private string _overview = "请先选择一个电视剧季。";
    private string _posterDisplayUrl = string.Empty;
    private string _seasonNumberText = "-";
    private string _airDateText = "-";
    private string _genreDisplay = "未提供";
    private string _ratingDisplay = "评分将在后续阶段接入";
    private string _sourceSummary = "暂无播放源";
    private string _progressText = "已看 0 / 0";
    private string _inLibraryText = "已入库 0 集";
    private string _identificationStatusText = "未加载";
    private string _unidentifiedSummary = string.Empty;
    private string _statusMessage = "请先选择一个电视剧季。";
    private bool _hasSeason;
    private bool _isUnidentified;
    private bool _isFavorite;
    private bool _isWantToWatch;
    private bool _isNotInterested;
    private bool _isSeasonWatched;
    private bool _isSeasonUnwatched;

    public TvSeasonDetailViewModel(
        INavigationStateService navigationStateService,
        ITvDetailQueryService tvDetailQueryService,
        IPlayerWindowService playerWindowService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IDataRefreshService dataRefreshService)
        : base("电视剧季", "查看电视剧季详情、聚合进度和集列表。")
    {
        _navigationStateService = navigationStateService;
        _tvDetailQueryService = tvDetailQueryService;
        _playerWindowService = playerWindowService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _dataRefreshService = dataRefreshService;
        NavigateBackToSeriesCommand = new RelayCommand(NavigateBackToSeries, () => _seriesId.HasValue);
        PlayEpisodeCommand = new AsyncRelayCommand(PlayEpisodeAsync);
        ToggleFavoriteCommand = new AsyncRelayCommand(() => ToggleFavoriteAsync(), () => HasSeason && (IsFavorite || IsSeasonWatched));
        ToggleWantToWatchCommand = new AsyncRelayCommand(() => ToggleWantToWatchAsync(), () => HasSeason && (IsWantToWatch || IsSeasonUnwatched));
        ToggleNotInterestedCommand = new AsyncRelayCommand(() => ToggleNotInterestedAsync(), () => HasSeason);
        MarkSeasonWatchedCommand = new AsyncRelayCommand(() => SetSeasonWatchedAsync(true), () => HasSeason);
        MarkSeasonUnwatchedCommand = new AsyncRelayCommand(() => SetSeasonWatchedAsync(false), () => HasSeason);
        MarkEpisodeWatchedCommand = new AsyncRelayCommand(parameter => SetEpisodeWatchedAsync(parameter, true));
        MarkEpisodeUnwatchedCommand = new AsyncRelayCommand(parameter => SetEpisodeWatchedAsync(parameter, false));
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
    }

    public ObservableCollection<TvSeasonEpisodeListItem> Episodes { get; } = [];

    public RelayCommand NavigateBackToSeriesCommand { get; }

    public AsyncRelayCommand PlayEpisodeCommand { get; }

    public AsyncRelayCommand ToggleFavoriteCommand { get; }

    public AsyncRelayCommand ToggleWantToWatchCommand { get; }

    public AsyncRelayCommand ToggleNotInterestedCommand { get; }

    public AsyncRelayCommand MarkSeasonWatchedCommand { get; }

    public AsyncRelayCommand MarkSeasonUnwatchedCommand { get; }

    public AsyncRelayCommand MarkEpisodeWatchedCommand { get; }

    public AsyncRelayCommand MarkEpisodeUnwatchedCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public string SeriesName { get => _seriesName; private set => SetProperty(ref _seriesName, value); }

    public string Name { get => _name; private set => SetProperty(ref _name, value); }

    public string Overview { get => _overview; private set => SetProperty(ref _overview, value); }

    public string PosterDisplayUrl { get => _posterDisplayUrl; private set => SetProperty(ref _posterDisplayUrl, value); }

    public string SeasonNumberText { get => _seasonNumberText; private set => SetProperty(ref _seasonNumberText, value); }

    public string AirDateText { get => _airDateText; private set => SetProperty(ref _airDateText, value); }

    public string GenreDisplay { get => _genreDisplay; private set => SetProperty(ref _genreDisplay, value); }

    public string RatingDisplay { get => _ratingDisplay; private set => SetProperty(ref _ratingDisplay, value); }

    public string SourceSummary { get => _sourceSummary; private set => SetProperty(ref _sourceSummary, value); }

    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }

    public string InLibraryText { get => _inLibraryText; private set => SetProperty(ref _inLibraryText, value); }

    public string IdentificationStatusText { get => _identificationStatusText; private set => SetProperty(ref _identificationStatusText, value); }

    public string UnidentifiedSummary { get => _unidentifiedSummary; private set => SetProperty(ref _unidentifiedSummary, value); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool HasSeason
    {
        get => _hasSeason;
        private set
        {
            if (SetProperty(ref _hasSeason, value))
            {
                OnPropertyChanged(nameof(HasNoSeason));
                OnPropertyChanged(nameof(HasEpisodes));
                OnPropertyChanged(nameof(HasNoEpisodes));
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public bool HasNoSeason => !HasSeason;

    public bool HasEpisodes => HasSeason && Episodes.Count > 0;

    public bool HasNoEpisodes => HasSeason && Episodes.Count == 0;

    public bool IsUnidentified
    {
        get => _isUnidentified;
        private set => SetProperty(ref _isUnidentified, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        private set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteButtonText));
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public bool IsWantToWatch
    {
        get => _isWantToWatch;
        private set
        {
            if (SetProperty(ref _isWantToWatch, value))
            {
                OnPropertyChanged(nameof(WantToWatchButtonText));
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public bool IsNotInterested
    {
        get => _isNotInterested;
        private set
        {
            if (SetProperty(ref _isNotInterested, value))
            {
                OnPropertyChanged(nameof(NotInterestedButtonText));
            }
        }
    }

    public bool IsSeasonWatched
    {
        get => _isSeasonWatched;
        private set
        {
            if (SetProperty(ref _isSeasonWatched, value))
            {
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public bool IsSeasonUnwatched
    {
        get => _isSeasonUnwatched;
        private set
        {
            if (SetProperty(ref _isSeasonUnwatched, value))
            {
                RaiseSeasonStateCommandCanExecuteChanged();
            }
        }
    }

    public string FavoriteButtonText => IsFavorite ? "取消喜爱" : "喜爱";

    public string WantToWatchButtonText => IsWantToWatch ? "取消想看" : "想看";

    public string NotInterestedButtonText => IsNotInterested ? "取消不想看" : "不想看";

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        var selectedSeasonId = _navigationStateService.SelectedTvSeasonId;
        if (!selectedSeasonId.HasValue)
        {
            Clear("请先选择一个电视剧季。");
            return;
        }

        try
        {
            var model = await _tvDetailQueryService.GetSeasonDetailAsync(selectedSeasonId.Value, cancellationToken);
            if (model is null)
            {
                Clear("未找到对应电视剧季，可能已被移出。");
                return;
            }

            _seasonId = model.SeasonId;
            _seriesId = model.SeriesId;
            HasSeason = true;
            SeriesName = model.SeriesName;
            Name = model.IsUnidentified ? "未识别电视剧季" : model.Name;
            Overview = string.IsNullOrWhiteSpace(model.Overview) ? "暂无简介。" : model.Overview;
            PosterDisplayUrl = model.PosterDisplayUrl;
            SeasonNumberText = model.SeasonNumberText;
            AirDateText = model.AirDateText;
            GenreDisplay = string.IsNullOrWhiteSpace(model.GenreDisplay) ? "未提供" : model.GenreDisplay;
            RatingDisplay = model.RatingDisplay;
            SourceSummary = model.SourceSummary;
            ProgressText = model.ProgressText;
            InLibraryText = model.InLibraryText;
            IdentificationStatusText = model.IdentificationStatusText;
            IsUnidentified = model.IsUnidentified;
            UnidentifiedSummary = model.UnidentifiedSummary;
            IsFavorite = model.IsFavorite;
            IsWantToWatch = model.IsWantToWatch;
            IsNotInterested = model.IsNotInterested;
            IsSeasonWatched = model.IsSeasonWatched;
            IsSeasonUnwatched = model.IsSeasonUnwatched;
            Episodes.Clear();
            foreach (var episode in model.Episodes)
            {
                Episodes.Add(episode);
            }

            NavigateBackToSeriesCommand.RaiseCanExecuteChanged();
            RaiseSeasonStateCommandCanExecuteChanged();
            OnPropertyChanged(nameof(HasEpisodes));
            OnPropertyChanged(nameof(HasNoEpisodes));
            var selectedEpisodeId = _navigationStateService.SelectedTvEpisodeId;
            StatusMessage = selectedEpisodeId.HasValue
                ? $"已加载集列表，目标集 ID：{selectedEpisodeId.Value}。"
                : Episodes.Count == 0
                    ? "该季暂无已解析集。"
                    : $"已加载 {Episodes.Count} 集。";
        }
        catch (Exception exception)
        {
            Clear($"加载电视剧季详情失败：{DescribeException(exception)}");
        }
    }

    private void NavigateBackToSeries()
    {
        if (_seriesId.HasValue)
        {
            _navigationStateService.RequestTvSeriesOverview(_seriesId.Value);
        }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetFavoriteAsync(_seasonId.Value, !IsFavorite, changeSource: "Manual");
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新喜爱状态失败：{DescribeException(exception)}";
        }
    }

    private async Task ToggleWantToWatchAsync()
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetWantToWatchAsync(_seasonId.Value, !IsWantToWatch, changeSource: "Manual");
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新想看状态失败：{DescribeException(exception)}";
        }
    }

    private async Task ToggleNotInterestedAsync()
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetNotInterestedAsync(_seasonId.Value, !IsNotInterested, changeSource: "Manual");
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新不想看状态失败：{DescribeException(exception)}";
        }
    }

    private async Task SetSeasonWatchedAsync(bool isWatched)
    {
        if (!_seasonId.HasValue)
        {
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetWatchedAsync(_seasonId.Value, isWatched, changeSource: "Manual");
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
            StatusMessage = isWatched ? "已标记整季为已看。" : "已标记整季为未看。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新整季观看状态失败：{DescribeException(exception)}";
        }
    }

    private async Task SetEpisodeWatchedAsync(object? parameter, bool isWatched)
    {
        if (parameter is not TvSeasonEpisodeListItem episode)
        {
            StatusMessage = "请先选择要标记的集。";
            return;
        }

        try
        {
            await _tvSeasonCollectionService.SetEpisodeWatchedAsync(episode.EpisodeId, isWatched, changeSource: "Manual");
            _dataRefreshService.NotifyPlaybackChanged();
            _dataRefreshService.NotifyCollectionChanged();
            await ActivateAsync();
            StatusMessage = $"{episode.EpisodeNumberText} 已标记为{(isWatched ? "已看" : "未看")}。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"更新集观看状态失败：{DescribeException(exception)}";
        }
    }

    private async Task PlayEpisodeAsync(object? parameter)
    {
        if (parameter is not TvSeasonEpisodeListItem episode)
        {
            StatusMessage = "请先选择要播放的集。";
            return;
        }

        if (!episode.HasPlayableSource)
        {
            StatusMessage = $"{episode.EpisodeNumberText} 暂无可播放源。";
            return;
        }

        try
        {
            StatusMessage = $"{episode.EpisodeNumberText} 正在打开播放器。";
            await _playerWindowService.OpenEpisodeAsync(episode.EpisodeId);
        }
        catch (Exception exception)
        {
            StatusMessage = $"打开剧集播放失败：{DescribeException(exception)}";
        }
    }

    private void Clear(string statusMessage)
    {
        _seasonId = null;
        _seriesId = null;
        HasSeason = false;
        SeriesName = "-";
        Name = "未选择电视剧季";
        Overview = "请先选择一个电视剧季。";
        PosterDisplayUrl = string.Empty;
        SeasonNumberText = "-";
        AirDateText = "-";
        GenreDisplay = "未提供";
        RatingDisplay = "评分将在后续阶段接入";
        SourceSummary = "暂无播放源";
        ProgressText = "已看 0 / 0";
        InLibraryText = "已入库 0 集";
        IdentificationStatusText = "未加载";
        IsUnidentified = false;
        UnidentifiedSummary = string.Empty;
        IsFavorite = false;
        IsWantToWatch = false;
        IsNotInterested = false;
        IsSeasonWatched = false;
        IsSeasonUnwatched = true;
        StatusMessage = statusMessage;
        Episodes.Clear();
        NavigateBackToSeriesCommand.RaiseCanExecuteChanged();
        RaiseSeasonStateCommandCanExecuteChanged();
        OnPropertyChanged(nameof(HasEpisodes));
        OnPropertyChanged(nameof(HasNoEpisodes));
    }

    private void RaiseSeasonStateCommandCanExecuteChanged()
    {
        ToggleFavoriteCommand.RaiseCanExecuteChanged();
        ToggleWantToWatchCommand.RaiseCanExecuteChanged();
        ToggleNotInterestedCommand.RaiseCanExecuteChanged();
        MarkSeasonWatchedCommand.RaiseCanExecuteChanged();
        MarkSeasonUnwatchedCommand.RaiseCanExecuteChanged();
    }

    private static string DescribeException(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
    }
}
