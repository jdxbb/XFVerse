using System.Collections.ObjectModel;
using MediaLibrary.App.Helpers;
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
    private string _directorText = "-";
    private string _writerText = "-";
    private string _actorsText = "-";
    private string _productionStatusText = "未提供";
    private string _networksText = "未提供";
    private string _productionCompaniesText = "未提供";
    private string _countryText = "-";
    private string _languageText = "-";
    private string _sourceSummary = "暂无播放源";
    private string _seasonCountText = "-";
    private string _episodeCountText = "-";
    private string _statusMessage = "请先选择一部电视剧。";
    private bool _hasSeries;
    private bool _canAddSeriesToLibrary;
    private bool _isDetailLoading;
    private double _seasonListScrollOffset;
    private bool? _restoreSeasonListScrollOffsetOnNextActivation;

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

    public ObservableCollection<MovieRatingItem> Ratings { get; } = [];

    public RelayCommand NavigateToSeasonCommand { get; }

    public RelayCommand NavigateBackCommand { get; }

    public AsyncRelayCommand AddSeriesToLibraryCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public double SeasonListScrollOffset
    {
        get => _seasonListScrollOffset;
        set
        {
            _seasonListScrollOffset = value;
            if (_seriesId.HasValue)
            {
                _navigationStateService.SetSeriesSeasonListScrollOffset(_seriesId.Value, value);
            }
        }
    }

    public string Name { get => _name; private set => SetProperty(ref _name, value); }

    public string OriginalName { get => _originalName; private set => SetProperty(ref _originalName, value); }

    public string Overview { get => _overview; private set => SetProperty(ref _overview, value); }

    public string PosterDisplayUrl { get => _posterDisplayUrl; private set => SetProperty(ref _posterDisplayUrl, value); }

    public string FirstAirDateText { get => _firstAirDateText; private set => SetProperty(ref _firstAirDateText, value); }

    public string GenresText { get => _genresText; private set => SetProperty(ref _genresText, value); }

    public string DirectorText { get => _directorText; private set => SetProperty(ref _directorText, value); }

    public string WriterText { get => _writerText; private set => SetProperty(ref _writerText, value); }

    public string ActorsText { get => _actorsText; private set => SetProperty(ref _actorsText, value); }

    public string ProductionStatusText { get => _productionStatusText; private set => SetProperty(ref _productionStatusText, value); }

    public string NetworksText { get => _networksText; private set => SetProperty(ref _networksText, value); }

    public string ProductionCompaniesText { get => _productionCompaniesText; private set => SetProperty(ref _productionCompaniesText, value); }

    public string CountryText { get => _countryText; private set => SetProperty(ref _countryText, value); }

    public string LanguageText { get => _languageText; private set => SetProperty(ref _languageText, value); }

    public string SourceSummary { get => _sourceSummary; private set => SetProperty(ref _sourceSummary, value); }

    public string SeasonCountText { get => _seasonCountText; private set => SetProperty(ref _seasonCountText, value); }

    public string EpisodeCountText { get => _episodeCountText; private set => SetProperty(ref _episodeCountText, value); }

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
                RefreshSeriesLibraryButtonState();
            }
        }
    }

    public bool HasNoSeries => !HasSeries;

    public bool HasSeasons => HasSeries && Seasons.Count > 0;

    public bool HasNoSeasons => HasSeries && Seasons.Count == 0;

    public bool IsDetailLoading
    {
        get => _isDetailLoading;
        private set => SetProperty(ref _isDetailLoading, value);
    }

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

            if (Seasons.All(x => x.IsVisibleInLibrary))
            {
                return "移出全部季";
            }

            return Seasons.Any(x => x.IsVisibleInLibrary) ? "补充加入全部季" : "加入全部季";
        }
    }

    public string AddSeriesToLibraryButtonIcon => Seasons.Count > 0 && Seasons.All(x => x.IsVisibleInLibrary)
        ? "\uE738"
        : "\uE710";

    public void PrepareForActivation()
    {
        var selectedSeriesId = _navigationStateService.SelectedTvSeriesId;
        if (!selectedSeriesId.HasValue)
        {
            _restoreSeasonListScrollOffsetOnNextActivation = false;
            return;
        }

        var shouldRestoreSeasonListOffset =
            _navigationStateService.ConsumeSeriesSeasonListScrollRestoreRequest(selectedSeriesId.Value);
        _restoreSeasonListScrollOffsetOnNextActivation = shouldRestoreSeasonListOffset;
        if (!shouldRestoreSeasonListOffset)
        {
            ResetSeasonListScrollOffset(selectedSeriesId.Value);
        }

        if (!_seriesId.HasValue || _seriesId.Value != selectedSeriesId.Value)
        {
            BeginDetailLoading("正在加载电视剧详情...");
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
            if (_seriesId.HasValue && _seriesId.Value != selectedSeriesId.Value)
            {
                BeginDetailLoading("正在加载电视剧详情...");
                PosterDisplayUrl = string.Empty;
                await Task.Yield();
            }

            var shouldRestoreSeasonListOffset =
                _restoreSeasonListScrollOffsetOnNextActivation ?? true;
            _restoreSeasonListScrollOffsetOnNextActivation = null;
            if (!shouldRestoreSeasonListOffset)
            {
                ResetSeasonListScrollOffset(selectedSeriesId.Value);
            }
            var model = await _tvDetailQueryService.GetSeriesOverviewAsync(selectedSeriesId.Value, cancellationToken);
            if (model is null)
            {
                Clear("未找到对应电视剧，可能已被移出。");
                return;
            }

            ApplyModel(
                model,
                cancellationToken,
                resetSeasonListScrollOffset: false);
            if (model.TmdbSeriesId.HasValue)
            {
                _ = HydrateAndRefreshAsync(model.SeriesId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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

            ApplyModel(model, cancellationToken, resetSeasonListScrollOffset: false);
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

    private void ApplyModel(
        TvSeriesOverviewModel model,
        CancellationToken cancellationToken = default,
        bool resetSeasonListScrollOffset = false)
    {
        _seriesId = model.SeriesId;
        if (resetSeasonListScrollOffset)
        {
            _navigationStateService.SetSeriesSeasonListScrollOffset(model.SeriesId, 0);
        }

        _seasonListScrollOffset = _navigationStateService.GetSeriesSeasonListScrollOffset(model.SeriesId);
        HasSeries = true;
        Name = model.Name;
        OriginalName = string.IsNullOrWhiteSpace(model.OriginalName) ? "-" : model.OriginalName;
        Overview = string.IsNullOrWhiteSpace(model.Overview) ? "暂无简介" : model.Overview;
        PosterDisplayUrl = model.PosterDisplayUrl;
        FirstAirDateText = model.FirstAirDateText;
        GenresText = string.IsNullOrWhiteSpace(model.GenresText) ? "未提供" : model.GenresText;
        DirectorText = string.IsNullOrWhiteSpace(model.DirectorText) ? "-" : model.DirectorText;
        WriterText = string.IsNullOrWhiteSpace(model.WriterText) ? "-" : model.WriterText;
        ActorsText = string.IsNullOrWhiteSpace(model.ActorsText) ? "-" : model.ActorsText;
        ProductionStatusText = MovieMetadataDisplayText.LocalizeTvProductionStatus(model.ProductionStatus);
        NetworksText = string.IsNullOrWhiteSpace(model.NetworksText) ? "未提供" : model.NetworksText;
        ProductionCompaniesText = string.IsNullOrWhiteSpace(model.ProductionCompaniesText) ? "未提供" : model.ProductionCompaniesText;
        CountryText = MovieMetadataDisplayText.LocalizeCountries(model.Country);
        LanguageText = MovieMetadataDisplayText.LocalizeLanguages(model.Language);
        SourceSummary = model.SourceSummary;
        SeasonCountText = model.SeasonCountText;
        EpisodeCountText = model.EpisodeCountText;
        ApplyRatings([]);
        Seasons.Clear();
        foreach (var season in model.Seasons)
        {
            Seasons.Add(season);
        }

        OnPropertyChanged(nameof(SeasonListScrollOffset));
        CanAddSeriesToLibrary = model.Seasons.Count > 0;
        RefreshSeriesLibraryButtonState();
        OnPropertyChanged(nameof(HasSeasons));
        OnPropertyChanged(nameof(HasNoSeasons));
        StatusMessage = Seasons.Count == 0
            ? "该剧暂无 Season metadata。"
            : $"已加载 {Seasons.Count} 个 Season。";
        IsDetailLoading = false;
        _ = LoadRatingsAsync(model.SeriesId, cancellationToken);
    }

    private void ResetSeasonListScrollOffset(int seriesId)
    {
        _navigationStateService.SetSeriesSeasonListScrollOffset(seriesId, 0);
        if (_seasonListScrollOffset <= 0)
        {
            return;
        }

        _seasonListScrollOffset = 0;
        OnPropertyChanged(nameof(SeasonListScrollOffset));
    }

    private async Task LoadRatingsAsync(int seriesId, CancellationToken cancellationToken)
    {
        try
        {
            var ratings = await _tvDetailQueryService.GetSeriesRatingsAsync(seriesId, cancellationToken);
            if (_seriesId == seriesId)
            {
                ApplyRatings(ratings);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            if (_seriesId == seriesId)
            {
                ApplyRatings([]);
            }
        }
    }

    private void ApplyRatings(IEnumerable<MovieRatingItem> ratings)
    {
        var bySource = ratings
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceName))
            .GroupBy(x => x.SourceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        Ratings.Clear();
        foreach (var source in new[] { "TMDB", "IMDb" })
        {
            Ratings.Add(bySource.GetValueOrDefault(source) ?? new MovieRatingItem { SourceName = source });
        }
    }

    private void RefreshSeriesLibraryButtonState()
    {
        OnPropertyChanged(nameof(ShowSeriesLibraryAction));
        OnPropertyChanged(nameof(AddSeriesToLibraryButtonText));
        OnPropertyChanged(nameof(AddSeriesToLibraryButtonIcon));
        AddSeriesToLibraryCommand.RaiseCanExecuteChanged();
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
            if (Seasons.Count > 0 && Seasons.All(x => x.IsVisibleInLibrary))
            {
                await _tvSeasonCollectionService.RemoveSeriesFromLibraryAsync(_seriesId.Value);
            }
            else if (Seasons.Any(x => x.LibraryVisibilityState == LibraryVisibilityState.Hidden))
            {
                await _tvSeasonCollectionService.RestoreSeriesToLibraryAsync(_seriesId.Value);
            }
            else
            {
                await _tvSeasonCollectionService.AddSeriesToLibraryAsync(_seriesId.Value);
            }
            _dataRefreshService.NotifyLibraryChanged();
            _dataRefreshService.NotifyCollectionChanged();
            var model = await _tvDetailQueryService.GetSeriesOverviewAsync(_seriesId.Value, CancellationToken.None);
            if (model is not null)
            {
                ApplyModel(model, CancellationToken.None, resetSeasonListScrollOffset: false);
            }

            StatusMessage = Seasons.All(x => x.IsVisibleInLibrary) ? "已加入媒体库。" : "已移出媒体库。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"加入媒体库失败：{DescribeException(exception)}";
        }
    }

    private void Clear(string statusMessage)
    {
        IsDetailLoading = false;
        _seriesId = null;
        _seasonListScrollOffset = 0;
        OnPropertyChanged(nameof(SeasonListScrollOffset));
        CanAddSeriesToLibrary = false;
        HasSeries = false;
        Name = "未选择电视剧";
        OriginalName = "-";
        Overview = "请先选择一部电视剧。";
        PosterDisplayUrl = string.Empty;
        FirstAirDateText = "-";
        GenresText = "未提供";
        DirectorText = "-";
        WriterText = "-";
        ActorsText = "-";
        ProductionStatusText = "未提供";
        NetworksText = "未提供";
        ProductionCompaniesText = "未提供";
        CountryText = "-";
        LanguageText = "-";
        SourceSummary = "暂无播放源";
        SeasonCountText = "-";
        EpisodeCountText = "-";
        StatusMessage = statusMessage;
        Seasons.Clear();
        Ratings.Clear();
        RefreshSeriesLibraryButtonState();
        OnPropertyChanged(nameof(HasSeasons));
        OnPropertyChanged(nameof(HasNoSeasons));
    }

    private void BeginDetailLoading(string statusMessage)
    {
        IsDetailLoading = true;
        Clear(statusMessage);
        IsDetailLoading = true;
    }

    private static string DescribeException(Exception exception)
    {
        var baseException = exception.GetBaseException();
        return ReferenceEquals(baseException, exception)
            ? exception.Message
            : $"{exception.Message} Inner: {baseException.Message}";
    }
}
