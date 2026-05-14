using System.Collections.ObjectModel;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class SeriesOverviewViewModel : PageViewModelBase
{
    private readonly INavigationStateService _navigationStateService;
    private readonly ITvDetailQueryService _tvDetailQueryService;
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

    public SeriesOverviewViewModel(
        INavigationStateService navigationStateService,
        ITvDetailQueryService tvDetailQueryService)
        : base("电视剧", "查看剧集包装信息和已入库季。")
    {
        _navigationStateService = navigationStateService;
        _tvDetailQueryService = tvDetailQueryService;
        NavigateToSeasonCommand = new RelayCommand(NavigateToSeason);
        RefreshCommand = new AsyncRelayCommand(() => ActivateAsync());
    }

    public ObservableCollection<TvSeriesSeasonListItem> Seasons { get; } = [];

    public RelayCommand NavigateToSeasonCommand { get; }

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

            OnPropertyChanged(nameof(HasSeasons));
            OnPropertyChanged(nameof(HasNoSeasons));
            StatusMessage = Seasons.Count == 0
                ? "该剧暂无已入库季。"
                : $"已加载 {Seasons.Count} 个已入库季。";
        }
        catch (Exception exception)
        {
            Clear($"加载电视剧详情失败：{DescribeException(exception)}");
        }
    }

    private void NavigateToSeason(object? parameter)
    {
        if (parameter is not TvSeriesSeasonListItem season)
        {
            return;
        }

        _navigationStateService.RequestTvSeasonDetail(season.SeasonId);
    }

    private void Clear(string statusMessage)
    {
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
