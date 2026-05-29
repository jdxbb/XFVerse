using System.Collections.ObjectModel;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class SeriesOverviewViewModel : PageViewModelBase
{
    private readonly INavigationStateService _navigationStateService;
    private readonly ITvDetailQueryService _tvDetailQueryService;
    private readonly ITvMetadataHydrationService _metadataHydrationService;
    private readonly ITvSeasonCollectionService _tvSeasonCollectionService;
    private readonly IDataRefreshService _dataRefreshService;
    private int? _seriesId;
    private string _name = "未选择电视剧";
    private string _originalName = "-";
    private string _overview = "请先选择一部电视剧。";
    private string _posterDisplayUrl = string.Empty;
    private string _firstAirDateText = "-";
    private string _genresText = "未提供";
    private string _sourceSummary = "暂无播放源";
    private string _seasonCountText = "-";
    private string _statusMessage = "请先选择一部电视剧。";
    private bool _hasSeries;
    private bool _canAddSeriesToLibrary;

    public SeriesOverviewViewModel(
        INavigationStateService navigationStateService,
        ITvDetailQueryService tvDetailQueryService,
        ITvMetadataHydrationService metadataHydrationService,
        ITvSeasonCollectionService tvSeasonCollectionService,
        IDataRefreshService dataRefreshService)
        : base("电视剧", "查看剧集信息和 Season。")
    {
        _navigationStateService = navigationStateService;
        _tvDetailQueryService = tvDetailQueryService;
        _metadataHydrationService = metadataHydrationService;
        _tvSeasonCollectionService = tvSeasonCollectionService;
        _dataRefreshService = dataRefreshService;
        NavigateToSeasonCommand = new RelayCommand(NavigateToSeason);
        NavigateBackCommand = new RelayCommand(_navigationStateService.RequestDetailBackToLibrary);
        AddSeriesToLibraryCommand = new AsyncRelayCommand(AddSeriesToLibraryAsync, () => CanAddSeriesToLibrary);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
    }

    public ObservableCollection<TvSeriesSeasonListItem> Seasons { get; } = [];

    public RelayCommand NavigateToSeasonCommand { get; }

    public RelayCommand NavigateBackCommand { get; }

    public AsyncRelayCommand AddSeriesToLibraryCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public string Name { get => _name; private set => SetProperty(ref _name, value); }

    public string OriginalName { get => _originalName; private set => SetProperty(ref _originalName, value); }

    public string Overview { get => _overview; private set => SetProperty(ref _overview, value); }

    public string PosterDisplayUrl { get => _posterDisplayUrl; private set => SetProperty(ref _posterDisplayUrl, value); }

    public string FirstAirDateText { get => _firstAirDateText; private set => SetProperty(ref _firstAirDateText, value); }

    public string GenresText { get => _genresText; private set => SetProperty(ref _genresText, value); }

    public string SourceSummary { get => _sourceSummary; private set => SetProperty(ref _sourceSummary, value); }

    public string SeasonCountText { get => _seasonCountText; private set => SetProperty(ref _seasonCountText, value); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool HasSeries
    {
        get => _hasSeries;
        private set
        {
            if (SetProperty(ref _hasSeries, value))
            {
                OnPropertyChanged(nameof(HasNoSeries));
                OnPropertyChanged(nameof(HasSeasons));
                OnPropertyChanged(nameof(HasNoSeasons));
            }
        }
    }

    public bool HasNoSeries => !HasSeries;

    public bool HasSeasons => HasSeries && Seasons.Count > 0;

    public bool HasNoSeasons => HasSeries && Seasons.Count == 0;

    public bool CanAddSeriesToLibrary
    {
        get => _canAddSeriesToLibrary;
        private set
        {
            if (SetProperty(ref _canAddSeriesToLibrary, value))
            {
                AddSeriesToLibraryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ShowSeriesLibraryAction => HasSeasons;

    public string AddSeriesToLibraryButtonText
    {
        get
        {
            if (Seasons.Count == 0)
            {
                return "暂无季信息";
            }

            var visibleSeasonCount = Seasons.Count(x => x.IsVisibleInLibrary);
            if (Seasons.Any(x => x.LibraryVisibilityState == LibraryVisibilityState.Hidden))
            {
                return "恢复到媒体库";
            }

            if (visibleSeasonCount <= 0)
            {
                return "加入整部剧";
            }

            return visibleSeasonCount < Seasons.Count
                ? "补充加入全部季"
                : "已在媒体库";
        }
    }

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        var selectedSeriesId = _navigationStateService.SelectedTvSeriesId;
        if (!selectedSeriesId.HasValue)
        {
            Clear("请先选择一部电视剧。");
            return;
        }

        try
        {
            var model = await _tvDetailQueryService.GetSeriesOverviewAsync(selectedSeriesId.Value, cancellationToken);
            if (model is null)
            {
                Clear("未找到对应电视剧，可能已被移出。");
                return;
            }

            ApplyModel(model);
            if (model.TmdbSeriesId.HasValue)
            {
                _ = HydrateAndRefreshAsync(model.SeriesId, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            Clear($"加载电视剧详情失败：{DescribeException(exception)}");
        }
    }

    private async Task HydrateAndRefreshAsync(int seriesId, CancellationToken cancellationToken)
    {
        try
        {
            StatusMessage = "正在补齐 TV metadata。";
            var result = await Task.Run(
                () => _metadataHydrationService.EnsureHydratedBySeriesIdAsync(
                    seriesId,
                    cancellationToken: cancellationToken),
                cancellationToken);

            if (_navigationStateService.SelectedTvSeriesId != seriesId)
            {
                return;
            }

            var model = await _tvDetailQueryService.GetSeriesOverviewAsync(seriesId, cancellationToken);
            if (model is null)
            {
                return;
            }

            ApplyModel(model);
            StatusMessage = result.Skipped
                ? $"已加载 {Seasons.Count} 个 Season。"
                : result.BuildStatusMessage();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (_navigationStateService.SelectedTvSeriesId == seriesId)
            {
                StatusMessage = $"TV metadata 补齐失败：{DescribeException(exception)}";
            }
        }
    }

    private void ApplyModel(TvSeriesOverviewModel model)
    {
        _seriesId = model.SeriesId;
        HasSeries = true;
        Name = model.Name;
        OriginalName = string.IsNullOrWhiteSpace(model.OriginalName) ? "-" : model.OriginalName;
        Overview = string.IsNullOrWhiteSpace(model.Overview) ? "暂无简介。" : model.Overview;
        PosterDisplayUrl = model.PosterDisplayUrl;
        FirstAirDateText = model.FirstAirDateText;
        GenresText = string.IsNullOrWhiteSpace(model.GenresText) ? "未提供" : model.GenresText;
        SourceSummary = model.SourceSummary;
        SeasonCountText = model.SeasonCountText;
        Seasons.Clear();
        foreach (var season in model.Seasons)
        {
            Seasons.Add(season);
        }

        CanAddSeriesToLibrary = model.Seasons.Any(season => !season.IsVisibleInLibrary);
        OnPropertyChanged(nameof(AddSeriesToLibraryButtonText));
        OnPropertyChanged(nameof(ShowSeriesLibraryAction));
        OnPropertyChanged(nameof(HasSeasons));
        OnPropertyChanged(nameof(HasNoSeasons));
        StatusMessage = Seasons.Count == 0
            ? "该剧暂无 Season metadata。"
            : $"已加载 {Seasons.Count} 个 Season。";
    }

    private void NavigateToSeason(object? parameter)
    {
        if (parameter is not TvSeriesSeasonListItem season)
        {
            return;
        }

        _navigationStateService.RequestTvSeasonDetail(season.SeasonId);
    }

    private async Task AddSeriesToLibraryAsync()
    {
        if (!_seriesId.HasValue)
        {
            return;
        }

        try
        {
            StatusMessage = "正在加入媒体库。";
            await _metadataHydrationService.EnsureHydratedBySeriesIdAsync(_seriesId.Value);
            if (Seasons.Any(x => x.LibraryVisibilityState == LibraryVisibilityState.Hidden))
            {
                await _tvSeasonCollectionService.RestoreSeriesToLibraryAsync(_seriesId.Value);
            }
            else
            {
                await _tvSeasonCollectionService.AddSeriesToLibraryAsync(_seriesId.Value);
            }
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyCollectionChanged();
            var model = await _tvDetailQueryService.GetSeriesOverviewAsync(_seriesId.Value);
            if (model is not null)
            {
                ApplyModel(model);
            }

            StatusMessage = "已加入媒体库。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"加入媒体库失败：{DescribeException(exception)}";
        }
    }

    private void Clear(string statusMessage)
    {
        _seriesId = null;
        CanAddSeriesToLibrary = false;
        HasSeries = false;
        Name = "未选择电视剧";
        OriginalName = "-";
        Overview = "请先选择一部电视剧。";
        PosterDisplayUrl = string.Empty;
        FirstAirDateText = "-";
        GenresText = "未提供";
        SourceSummary = "暂无播放源";
        SeasonCountText = "-";
        StatusMessage = statusMessage;
        Seasons.Clear();
        OnPropertyChanged(nameof(AddSeriesToLibraryButtonText));
        OnPropertyChanged(nameof(ShowSeriesLibraryAction));
        OnPropertyChanged(nameof(HasSeasons));
        OnPropertyChanged(nameof(HasNoSeasons));
    }

    private static string DescribeException(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
    }
}
