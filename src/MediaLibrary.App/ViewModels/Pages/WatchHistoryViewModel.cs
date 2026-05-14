using System.Collections.ObjectModel;
using System.Windows;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class WatchHistoryViewModel : PageViewModelBase
{
    private const int HistoryTake = 100;
    private const string FilterAll = "全部";
    private const string FilterToday = "今天";
    private const string FilterThisWeek = "本周";
    private const string FilterThisMonth = "本月";
    private const string FilterSpecificDate = "指定日期";
    private readonly IWatchHistoryService _watchHistoryService;
    private readonly INavigationStateService _navigationStateService;
    private readonly IDataRefreshService _dataRefreshService;
    private bool _isActive;
    private bool _isLoading;
    private bool _suppressFilterRefresh;
    private string _selectedDateFilter = FilterAll;
    private DateTime? _selectedCustomDate;
    private string _statusMessage = "正在加载观影历史。";

    public WatchHistoryViewModel(
        IWatchHistoryService watchHistoryService,
        INavigationStateService navigationStateService,
        IDataRefreshService dataRefreshService)
        : base("观影历史", "按日期回看你的观看记录。")
    {
        _watchHistoryService = watchHistoryService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;
        _dataRefreshService.DataChanged += OnDataChanged;

        DateFilterOptions = [FilterAll, FilterToday, FilterThisWeek, FilterThisMonth, FilterSpecificDate];
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(), () => !IsLoading);
        OpenMovieCommand = new RelayCommand(OpenMovie);
    }

    public ObservableCollection<WatchHistoryDayGroupViewModel> DayGroups { get; } = [];

    public IReadOnlyList<string> DateFilterOptions { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public RelayCommand OpenMovieCommand { get; }

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
            }
        }
    }

    public string SelectedDateFilter
    {
        get => _selectedDateFilter;
        set
        {
            if (!DateFilterOptions.Contains(value))
            {
                return;
            }

            if (SetProperty(ref _selectedDateFilter, value))
            {
                if (value == FilterSpecificDate && !_selectedCustomDate.HasValue)
                {
                    SetProperty(ref _selectedCustomDate, DateTime.Today, nameof(SelectedCustomDate));
                }

                OnPropertyChanged(nameof(IsCustomDateFilter));
                QueueLoadForFilterChange();
            }
        }
    }

    public DateTime? SelectedCustomDate
    {
        get => _selectedCustomDate;
        set
        {
            var normalized = value?.Date;
            if (SetProperty(ref _selectedCustomDate, normalized))
            {
                if (normalized.HasValue && SelectedDateFilter != FilterSpecificDate)
                {
                    _suppressFilterRefresh = true;
                    SelectedDateFilter = FilterSpecificDate;
                    _suppressFilterRefresh = false;
                }

                QueueLoadForFilterChange();
            }
        }
    }

    public bool IsCustomDateFilter => SelectedDateFilter == FilterSpecificDate;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasDayGroups => DayGroups.Count > 0;

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        _isActive = true;
        var targetDate = _navigationStateService.ConsumeWatchHistoryTargetDate();
        if (targetDate.HasValue)
        {
            ApplyTargetDate(targetDate.Value.Date);
        }

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
        return e.PlaybackChanged
               || e.LibraryChanged
               || e.Reason is AppDataChangeReason.MetadataChanged
                   or AppDataChangeReason.CollectionChanged;
    }

    private void ApplyTargetDate(DateTime date)
    {
        _suppressFilterRefresh = true;
        try
        {
            SelectedDateFilter = FilterSpecificDate;
            SelectedCustomDate = date.Date;
        }
        finally
        {
            _suppressFilterRefresh = false;
        }
    }

    private void QueueLoadForFilterChange()
    {
        if (_suppressFilterRefresh)
        {
            return;
        }

        _ = LoadAsync();
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
            StatusMessage = "正在加载观影历史。";
            var items = await _watchHistoryService.GetHistoryItemsAsync(BuildQuery(), cancellationToken);
            ReplaceGroups(BuildDayGroups(items));
            StatusMessage = BuildStatusMessage(items.Count);
        }
        catch (Exception exception)
        {
            DayGroups.Clear();
            OnPropertyChanged(nameof(HasDayGroups));
            StatusMessage = $"加载观影历史失败：{exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private WatchHistoryQuery BuildQuery()
    {
        var (startLocal, endLocal) = ResolveDateRange();
        return new WatchHistoryQuery
        {
            StartedAtUtc = startLocal.HasValue ? ToUtc(startLocal.Value) : null,
            EndedBeforeUtc = endLocal.HasValue ? ToUtc(endLocal.Value) : null,
            Take = HistoryTake
        };
    }

    private (DateTime? StartLocal, DateTime? EndLocal) ResolveDateRange()
    {
        var today = DateTime.Today;
        return SelectedDateFilter switch
        {
            FilterToday => (today, today.AddDays(1)),
            FilterThisWeek => ResolveThisWeek(today),
            FilterThisMonth => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1)),
            FilterSpecificDate => ResolveSpecificDate(today),
            _ => (null, null)
        };
    }

    private static (DateTime StartLocal, DateTime EndLocal) ResolveThisWeek(DateTime today)
    {
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        var start = today.AddDays(-daysFromMonday);
        return (start, start.AddDays(7));
    }

    private (DateTime StartLocal, DateTime EndLocal) ResolveSpecificDate(DateTime today)
    {
        var date = (SelectedCustomDate ?? today).Date;
        return (date, date.AddDays(1));
    }

    private static DateTime ToUtc(DateTime localDateTime)
    {
        var local = localDateTime.Kind == DateTimeKind.Local
            ? localDateTime
            : DateTime.SpecifyKind(localDateTime, DateTimeKind.Local);
        return local.ToUniversalTime();
    }

    private IReadOnlyList<WatchHistoryDayGroupViewModel> BuildDayGroups(IReadOnlyList<WatchHistoryListItem> items)
    {
        return items
            .GroupBy(item => item.StartedAtLocal.Date)
            .OrderByDescending(group => group.Key)
            .Select(group => new WatchHistoryDayGroupViewModel(
                group.Key,
                BuildDateTitle(group.Key),
                group.OrderByDescending(item => item.StartedAtLocal).Select(item => new WatchHistoryItemViewModel(item)).ToList()))
            .ToList();
    }

    private static string BuildDateTitle(DateTime date)
    {
        var today = DateTime.Today;
        if (date == today)
        {
            return "今天";
        }

        if (date == today.AddDays(-1))
        {
            return "昨天";
        }

        return date.ToString("yyyy年M月d日");
    }

    private void ReplaceGroups(IEnumerable<WatchHistoryDayGroupViewModel> groups)
    {
        DayGroups.Clear();
        foreach (var group in groups)
        {
            DayGroups.Add(group);
        }

        OnPropertyChanged(nameof(HasDayGroups));
    }

    private string BuildStatusMessage(int count)
    {
        if (count == 0)
        {
            return SelectedDateFilter == FilterAll
                ? "还没有观看记录。开始播放影片后，这里会按日期记录你的观影足迹。"
                : "当前日期范围内没有观看记录。";
        }

        var rangeText = SelectedDateFilter == FilterSpecificDate && SelectedCustomDate.HasValue
            ? $"{SelectedCustomDate.Value:yyyy年M月d日}"
            : SelectedDateFilter;
        return $"找到 {count} 条观看记录 · {rangeText}";
    }

    private void OpenMovie(object? parameter)
    {
        if (parameter is not WatchHistoryItemViewModel item)
        {
            return;
        }

        if (item.EpisodeId.HasValue && item.TvSeasonId.HasValue)
        {
            _navigationStateService.RequestTvSeasonDetail(item.TvSeasonId.Value, item.EpisodeId);
            return;
        }

        if (item.MovieId > 0)
        {
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, item.MovieId);
        }
    }

    public sealed class WatchHistoryDayGroupViewModel
    {
        public WatchHistoryDayGroupViewModel(
            DateTime date,
            string title,
            IReadOnlyList<WatchHistoryItemViewModel> items)
        {
            Date = date;
            Title = title;
            Items = items;
            SummaryText = $"{items.Count} 条 · {FormatDurationText(items.Sum(item => item.DurationWatchedSeconds))}";
        }

        public DateTime Date { get; }

        public string Title { get; }

        public string SummaryText { get; }

        public IReadOnlyList<WatchHistoryItemViewModel> Items { get; }
    }

    public sealed class WatchHistoryItemViewModel
    {
        public WatchHistoryItemViewModel(WatchHistoryListItem item)
        {
            HistoryId = item.HistoryId;
            MovieId = item.MovieId;
            EpisodeId = item.EpisodeId;
            TvSeasonId = item.TvSeasonId;
            Title = string.IsNullOrWhiteSpace(item.Title) ? "未知影片" : item.Title;
            ReleaseYearText = item.ReleaseYear.HasValue ? item.ReleaseYear.Value.ToString() : "年份未知";
            PosterRemoteUrl = item.PosterRemoteUrl;
            WatchTimeText = item.StartedAtLocal.ToString("HH:mm");
            WatchDurationText = FormatDurationText(item.DurationWatchedSeconds);
            DurationWatchedSeconds = item.DurationWatchedSeconds;
            ProgressValue = item.ProgressPercent ?? 0d;
            HasProgressPercent = item.ProgressPercent.HasValue;
            ProgressText = BuildProgressText(item);
            MediaFileName = item.MediaFileName;
            SourceStatusText = item.IsMediaFileDeleted ? "播放源已移出" : string.Empty;
            CanOpenDetail = item.MovieId > 0 || (item.EpisodeId.HasValue && item.TvSeasonId.HasValue);
        }

        public int HistoryId { get; }

        public int MovieId { get; }

        public int? EpisodeId { get; }

        public int? TvSeasonId { get; }

        public string Title { get; }

        public string ReleaseYearText { get; }

        public string PosterRemoteUrl { get; }

        public string WatchTimeText { get; }

        public string WatchDurationText { get; }

        public int DurationWatchedSeconds { get; }

        public string ProgressText { get; }

        public double ProgressValue { get; }

        public bool HasProgressPercent { get; }

        public string MediaFileName { get; }

        public string SourceStatusText { get; }

        public bool CanOpenDetail { get; }

        public bool HasSourceStatus => !string.IsNullOrWhiteSpace(SourceStatusText);
    }

    private static string BuildProgressText(WatchHistoryListItem item)
    {
        if (item.IsCompleted)
        {
            return "进度：已看完";
        }

        if (item.ProgressPercent.HasValue && item.LastPlayPositionSeconds > 0)
        {
            return $"进度 {item.ProgressPercent.Value:0}% · 看到 {FormatDurationText(item.LastPlayPositionSeconds)}";
        }

        return item.LastPlayPositionSeconds > 0
            ? $"看到 {FormatDurationText(item.LastPlayPositionSeconds)}"
            : "暂无可靠进度";
    }

    private static string FormatDurationText(int seconds)
    {
        var value = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (value.TotalHours >= 1)
        {
            return value.Minutes > 0
                ? $"{(int)value.TotalHours}小时{value.Minutes}分钟"
                : $"{(int)value.TotalHours}小时";
        }

        if (value.TotalMinutes >= 1)
        {
            return $"{Math.Max(1, (int)Math.Round(value.TotalMinutes))}分钟";
        }

        return $"{value.Seconds}秒";
    }
}
