using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class WatchInsightsViewModel : PageViewModelBase
{
    private const string ProfileTabKey = "profile";
    private const string StatisticsTabKey = "statistics";
    private const int ProfileTabIndex = 0;
    private const int StatisticsTabIndex = 1;
    private const int TasteGraphNodeLimit = 4;
    private const int TasteGraphLinkLimit = 14;
    private const double TasteGraphNodeWidth = 108d;
    private const double TasteGraphNodeHeight = 78d;
    private const double TasteGraphTypeX = 46d;
    private const double TasteGraphEmotionX = 276d;
    private const double TasteGraphSceneX = 506d;
    private const double TasteGraphFirstNodeY = 74d;
    private const double TasteGraphNodeSpacingY = 88d;
    private const double PreferenceBubbleCanvasWidth = 760d;
    private const double PreferenceBubbleCanvasHeight = 520d;
    private static readonly (double X, double Y)[] PreferenceBubbleSlots =
    [
        (116d, 100d),
        (300d, 78d),
        (522d, 102d),
        (654d, 176d),
        (178d, 232d),
        (392d, 238d),
        (582d, 306d),
        (108d, 364d),
        (306d, 392d),
        (504d, 426d),
        (656d, 402d),
        (430d, 152d),
        (244d, 318d),
        (620d, 66d)
    ];
    private const string PersonaPosterDefaultGender = "female";
    private const string PersonaPosterFallbackKey = "genre_explorer";
    private const string PersonaFrameDefaultColor = "gold";
    private static readonly string PersonaFrameDefaultUri = BuildPersonaAssetUri("Frames", "persona_card_frame_default.png");
    private static readonly string[] PersonaPosterExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    private static readonly IReadOnlyDictionary<string, string> PersonaTypeKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["情绪沉浸者"] = "emotion_immersive",
            ["悬疑解谜者"] = "mystery_solver",
            ["类型探索家"] = "genre_explorer",
            ["经典收藏家"] = "classic_collector",
            ["治愈陪伴型"] = "healing_companion",
            ["高分严选派"] = "rating_curator",
            ["作者导演迷"] = "auteur_follower",
            ["科幻幻想旅人"] = "sci_fantasy_traveler",
            ["现实观察者"] = "realism_observer",
            ["动作爽片玩家"] = "action_player",
            ["文艺审美家"] = "arthouse_aesthete",
            ["惊悚氛围控"] = "thriller_atmosphere_fan",
            ["黑色幽默爱好者"] = "dark_humorist",
            ["浪漫幻想派"] = "romantic_dreamer",
            ["暗黑猎奇者"] = "dark_curiosity_seeker",
            ["史诗世界观派"] = "epic_worldbuilder",
            ["轻松娱乐派"] = "easy_entertainment_fan",
            ["人性剖析者"] = "human_nature_analyst",
            ["怀旧年代派"] = "nostalgia_time_traveler",
            ["小众寻宝者"] = "niche_treasure_hunter",
            ["爆笑解压派"] = "comedy_relief_fan",
            ["动画叙事派"] = "animation_narrative_fan",
            ["纪录求真者"] = "documentary_truth_seeker",
            ["童心奇想家"] = "animation_narrative_fan"
        };
    private static readonly IReadOnlyDictionary<string, string> PersonaFrameColors =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["emotion_immersive"] = "blue",
            ["mystery_solver"] = "blue",
            ["genre_explorer"] = "blue",
            ["classic_collector"] = "gold",
            ["healing_companion"] = "pink",
            ["rating_curator"] = "gold",
            ["auteur_follower"] = "blue",
            ["sci_fantasy_traveler"] = "blue",
            ["realism_observer"] = "blue",
            ["action_player"] = "green",
            ["arthouse_aesthete"] = "green",
            ["thriller_atmosphere_fan"] = "blue",
            ["dark_humorist"] = "blue",
            ["romantic_dreamer"] = "pink",
            ["dark_curiosity_seeker"] = "blue",
            ["epic_worldbuilder"] = "gold",
            ["easy_entertainment_fan"] = "gold",
            ["human_nature_analyst"] = "pink",
            ["nostalgia_time_traveler"] = "gold",
            ["niche_treasure_hunter"] = "gold",
            ["comedy_relief_fan"] = "pink",
            ["animation_narrative_fan"] = "blue",
            ["documentary_truth_seeker"] = "gold"
        };
    private static readonly TimeSpan DataChangedRefreshDebounce = TimeSpan.FromMilliseconds(600);
    private readonly IWatchStatisticsService _statisticsService;
    private readonly IWatchProfileService _profileService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly INavigationStateService _navigationStateService;
    private CancellationTokenSource? _dataChangedRefreshDebounceCts;
    private string _selectedTab = ProfileTabKey;
    private bool _isLoadingStatistics;
    private bool _isLoadingProfile;
    private bool _isActive;
    private bool _hasLoadedStatistics;
    private bool _statisticsRefreshPendingOnActivate;
    private bool _statisticsRefreshQueuedAfterCurrent;
    private string _statisticsErrorMessage = string.Empty;
    private string _profileErrorMessage = string.Empty;
    private string _profileInsufficientReason = string.Empty;
    private string _profileStatusText = "尚未加载";
    private string _lastProfileRefreshedAtText = "尚未生成";
    private string _calendarNoticeMessage = string.Empty;
    private string _lastStatisticsRefreshedAtText = "尚未加载";
    private WatchStatisticsTimeRange _statisticsTimeRange = WatchStatisticsTimeRange.Month;
    private DateTime _calendarMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1);
    private WatchStatisticsSnapshot _statistics = new();
    private WatchProfileSnapshot? _profile;

    public WatchInsightsViewModel(
        IWatchStatisticsService statisticsService,
        IWatchProfileService profileService,
        IDataRefreshService dataRefreshService,
        INavigationStateService navigationStateService)
        : base("观影洞察", "让你更懂你")
    {
        _statisticsService = statisticsService;
        _profileService = profileService;
        _dataRefreshService = dataRefreshService;
        _navigationStateService = navigationStateService;
        _dataRefreshService.DataChanged += OnDataChanged;

        ProfileEmptyCards =
        [
            new("观影口味总结", "暂无足够数据", "继续观看、标记影片或刷新统计后，这里会生成更完整的观影洞察。"),
            new("观影 DNA", "待生成", "类型、情绪、场景、节奏、叙事和探索六个基因会在画像阶段接入。"),
            new("你的观影人格", "待生成", "观影人格需要 AI 画像生成，本阶段先保留组件结构。"),
            new("口味象限", "待生成", "口味象限会在后续画像阶段根据结构化画像分数展示。")
        ];

        LoadCommand = new AsyncRelayCommand(() => RefreshStatisticsAsync(StatisticsRefreshSource.Activate, forceRefresh: false));
        SelectProfileTabCommand = new RelayCommand(SelectProfileTab);
        SelectStatisticsTabCommand = new RelayCommand(SelectStatisticsTab);
        SelectTabCommand = new RelayCommand(SelectTab);
        SelectMonthRangeCommand = new AsyncRelayCommand(
            () => ChangeStatisticsRangeAsync(WatchStatisticsTimeRange.Month),
            () => !IsLoadingStatistics);
        SelectAllRangeCommand = new AsyncRelayCommand(
            () => ChangeStatisticsRangeAsync(WatchStatisticsTimeRange.All),
            () => !IsLoadingStatistics);
        RefreshStatisticsCommand = new AsyncRelayCommand(
            () => RefreshStatisticsAsync(StatisticsRefreshSource.Manual, forceRefresh: true),
            () => !IsLoadingStatistics);
        RefreshProfileCommand = new AsyncRelayCommand(
            () => LoadProfileAsync(ProfileLoadSource.Manual, forceRefresh: true),
            () => CanRefreshProfile);
        PreviousCalendarMonthCommand = new AsyncRelayCommand(
            () => ChangeCalendarMonthAsync(_calendarMonth.AddMonths(-1)),
            () => !IsLoadingStatistics && CanGoPreviousCalendarMonth);
        NextCalendarMonthCommand = new AsyncRelayCommand(
            () => ChangeCalendarMonthAsync(_calendarMonth.AddMonths(1)),
            () => !IsLoadingStatistics && CanGoNextCalendarMonth);
        ReturnToCurrentCalendarMonthCommand = new AsyncRelayCommand(
            () => ChangeCalendarMonthAsync(GetCurrentMonthStart()),
            () => !IsLoadingStatistics && ShowReturnToCurrentMonth);
        OpenWatchHistoryByDateCommand = new RelayCommand(OpenWatchHistoryByDate);
    }

    public AsyncRelayCommand LoadCommand { get; }

    public RelayCommand SelectProfileTabCommand { get; }

    public RelayCommand SelectStatisticsTabCommand { get; }

    public RelayCommand SelectTabCommand { get; }

    public AsyncRelayCommand SelectMonthRangeCommand { get; }

    public AsyncRelayCommand SelectAllRangeCommand { get; }

    public AsyncRelayCommand RefreshStatisticsCommand { get; }

    public AsyncRelayCommand RefreshProfileCommand { get; }

    public AsyncRelayCommand PreviousCalendarMonthCommand { get; }

    public AsyncRelayCommand NextCalendarMonthCommand { get; }

    public AsyncRelayCommand ReturnToCurrentCalendarMonthCommand { get; }

    public RelayCommand OpenWatchHistoryByDateCommand { get; }

    public IReadOnlyList<ProfilePlaceholderCard> ProfileEmptyCards { get; }

    public ObservableCollection<WarningMessageItem> WarningMessages { get; } = [];

    public ObservableCollection<OverviewMetricCard> OverviewCards { get; } = [];

    public ObservableCollection<TagChipItem> MonthlyFrequentTags { get; } = [];

    public ObservableCollection<CalendarDayCell> CalendarCells { get; } = [];

    public ObservableCollection<BubbleTagItem> PreferenceBubbles { get; } = [];

    public ObservableCollection<RankingGroup> MonthlyRankingGroups { get; } = [];

    public ObservableCollection<TimeBucketItem> ViewingTimeBuckets { get; } = [];

    public ObservableCollection<WeekPartItem> WeekPartItems { get; } = [];

    public ObservableCollection<DurationBucketItem> DurationBuckets { get; } = [];

    public ObservableCollection<TasteGraphNodeItem> TasteGraphNodes { get; } = [];

    public ObservableCollection<TasteGraphLinkItem> TasteGraphLinks { get; } = [];

    public ObservableCollection<TasteCombinationRankItem> TasteCombinationTop10 { get; } = [];

    public ObservableCollection<RankingGroup> WatchLikeGroups { get; } = [];

    public ObservableCollection<TagChipItem> ProfileKeywords { get; } = [];

    public ObservableCollection<ProfileDnaItem> ProfileDnaItems { get; } = [];

    public ObservableCollection<WarningMessageItem> ProfileWarnings { get; } = [];

    public ObservableCollection<WarningMessageItem> ProfileCaveats { get; } = [];

    public ObservableCollection<TagChipItem> PreferredGenres { get; } = [];

    public ObservableCollection<TagChipItem> PreferredEmotions { get; } = [];

    public ObservableCollection<TagChipItem> PreferredScenes { get; } = [];

    public ObservableCollection<TagChipItem> PreferredCountries { get; } = [];

    public ObservableCollection<TagChipItem> PreferredLanguages { get; } = [];

    public ObservableCollection<TagChipItem> AvoidGenres { get; } = [];

    public ObservableCollection<TagChipItem> AvoidEmotions { get; } = [];

    public ObservableCollection<TagChipItem> AvoidScenes { get; } = [];

    public ObservableCollection<TagChipItem> LikelyToEnjoy { get; } = [];

    public ObservableCollection<TagChipItem> LessLikelyToEnjoy { get; } = [];

    public ObservableCollection<ProfileListGroup> ProfileWatchLikeGroups { get; } = [];

    public string SelectedTab
    {
        get => _selectedTab;
        private set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                OnPropertyChanged(nameof(IsProfileTabSelected));
                OnPropertyChanged(nameof(IsStatisticsTabSelected));
                OnPropertyChanged(nameof(SelectedTabIndex));
            }
        }
    }

    public bool IsProfileTabSelected => string.Equals(SelectedTab, ProfileTabKey, StringComparison.Ordinal);

    public bool IsStatisticsTabSelected => string.Equals(SelectedTab, StatisticsTabKey, StringComparison.Ordinal);

    public int SelectedTabIndex
    {
        get => IsStatisticsTabSelected ? StatisticsTabIndex : ProfileTabIndex;
        set
        {
            if (value == StatisticsTabIndex && !IsStatisticsTabSelected)
            {
                SelectStatisticsTab();
            }
            else if (value != StatisticsTabIndex && !IsProfileTabSelected)
            {
                SelectProfileTab();
            }
        }
    }

    public bool IsLoadingStatistics
    {
        get => _isLoadingStatistics;
        private set
        {
            if (SetProperty(ref _isLoadingStatistics, value))
            {
                RefreshStatisticsCommand.RaiseCanExecuteChanged();
                SelectMonthRangeCommand.RaiseCanExecuteChanged();
                SelectAllRangeCommand.RaiseCanExecuteChanged();
                PreviousCalendarMonthCommand.RaiseCanExecuteChanged();
                NextCalendarMonthCommand.RaiseCanExecuteChanged();
                ReturnToCurrentCalendarMonthCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(RefreshButtonText));
                OnPropertyChanged(nameof(IsRefreshing));
                OnPropertyChanged(nameof(IsStatisticsRefreshAnimating));
                OnPropertyChanged(nameof(StatisticsRefreshStatusText));
                OnPropertyChanged(nameof(StatisticsModuleState));
                OnPropertyChanged(nameof(ShowStatisticsModuleState));
            }
        }
    }

    public string RefreshButtonText => IsLoadingStatistics ? "刷新中..." : "刷新统计";

    public bool IsStatisticsRefreshAnimating => IsLoadingStatistics;

    public bool IsLoadingProfile
    {
        get => _isLoadingProfile;
        private set
        {
            if (SetProperty(ref _isLoadingProfile, value))
            {
                RefreshProfileCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(ProfileRefreshButtonText));
                OnPropertyChanged(nameof(CanRefreshProfile));
                OnPropertyChanged(nameof(IsRefreshing));
                OnPropertyChanged(nameof(IsProfileRefreshAnimating));
                OnPropertyChanged(nameof(ProfileRefreshStatusText));
                OnPropertyChanged(nameof(ProfileModuleState));
                OnPropertyChanged(nameof(ShowProfileModuleState));
            }
        }
    }

    public string ProfileRefreshButtonText => IsLoadingProfile ? "生成中..." : "刷新画像";

    public bool IsProfileRefreshAnimating => IsLoadingProfile;

    public bool CanRefreshProfile => !IsLoadingProfile && !IsProfileInsufficient;

    public string StatisticsErrorMessage
    {
        get => _statisticsErrorMessage;
        private set
        {
            if (SetProperty(ref _statisticsErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasStatisticsError));
                OnPropertyChanged(nameof(StatisticsRefreshStatusText));
                OnPropertyChanged(nameof(StatisticsModuleState));
                OnPropertyChanged(nameof(ShowStatisticsModuleState));
            }
        }
    }

    public bool HasStatisticsError => !string.IsNullOrWhiteSpace(StatisticsErrorMessage);

    public string ProfileErrorMessage
    {
        get => _profileErrorMessage;
        private set
        {
            if (SetProperty(ref _profileErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasProfileError));
                OnPropertyChanged(nameof(ShowProfileEmptyState));
                OnPropertyChanged(nameof(ProfileRefreshStatusText));
                OnPropertyChanged(nameof(ProfileModuleState));
                OnPropertyChanged(nameof(ShowProfileModuleState));
            }
        }
    }

    public bool HasProfileError => !string.IsNullOrWhiteSpace(ProfileErrorMessage);

    public string ProfileInsufficientReason
    {
        get => _profileInsufficientReason;
        private set
        {
            if (SetProperty(ref _profileInsufficientReason, value))
            {
                OnPropertyChanged(nameof(IsProfileInsufficient));
                OnPropertyChanged(nameof(ShowProfileEmptyState));
                OnPropertyChanged(nameof(CanRefreshProfile));
                OnPropertyChanged(nameof(ProfileRefreshStatusText));
                OnPropertyChanged(nameof(ProfileModuleState));
                OnPropertyChanged(nameof(ShowProfileModuleState));
                RefreshProfileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsProfileInsufficient => !string.IsNullOrWhiteSpace(ProfileInsufficientReason);

    public string ProfileStatusText
    {
        get => _profileStatusText;
        private set
        {
            if (SetProperty(ref _profileStatusText, value))
            {
                OnPropertyChanged(nameof(ProfileRefreshStatusText));
            }
        }
    }

    public string CalendarNoticeMessage
    {
        get => _calendarNoticeMessage;
        private set
        {
            if (SetProperty(ref _calendarNoticeMessage, value))
            {
                OnPropertyChanged(nameof(HasCalendarNotice));
            }
        }
    }

    public bool HasCalendarNotice => !string.IsNullOrWhiteSpace(CalendarNoticeMessage);

    public WatchStatisticsSnapshot Statistics
    {
        get => _statistics;
        private set
        {
            if (SetProperty(ref _statistics, value))
            {
                OnPropertyChanged(nameof(HasAnyStatisticsData));
                OnPropertyChanged(nameof(StatisticsRefreshStatusText));
            }
        }
    }

    public string LastStatisticsRefreshedAtText
    {
        get => _lastStatisticsRefreshedAtText;
        private set
        {
            if (SetProperty(ref _lastStatisticsRefreshedAtText, value))
            {
                OnPropertyChanged(nameof(StatisticsRefreshStatusText));
            }
        }
    }

    public string LastProfileRefreshedAtText
    {
        get => _lastProfileRefreshedAtText;
        private set
        {
            if (SetProperty(ref _lastProfileRefreshedAtText, value))
            {
                OnPropertyChanged(nameof(ProfileRefreshStatusText));
            }
        }
    }

    public string ProfileRefreshStatusText
    {
        get
        {
            if (IsLoadingProfile)
            {
                return "正在生成新的画像...";
            }

            if (IsProfileInsufficient)
            {
                return string.IsNullOrWhiteSpace(ProfileInsufficientReason)
                    ? "画像数据不足"
                    : "画像数据不足，继续观看后再生成";
            }

            if (HasProfileError && !HasProfile)
            {
                return "画像生成失败，请稍后重试";
            }

            var status = string.IsNullOrWhiteSpace(ProfileStatusText) ? "画像状态" : ProfileStatusText;
            if (string.IsNullOrWhiteSpace(LastProfileRefreshedAtText)
                || string.Equals(LastProfileRefreshedAtText, "尚未生成", StringComparison.Ordinal))
            {
                return status;
            }

            return $"{status} · {LastProfileRefreshedAtText}";
        }
    }

    public string StatisticsRefreshStatusText
    {
        get
        {
            if (IsLoadingStatistics)
            {
                return "正在刷新统计...";
            }

            if (HasStatisticsError)
            {
                return HasAnyStatisticsData
                    ? $"统计刷新失败 · {LastStatisticsRefreshedAtText}"
                    : "统计加载失败，请稍后重试";
            }

            if (!HasAnyStatisticsData)
            {
                return "暂无统计数据";
            }

            if (string.IsNullOrWhiteSpace(LastStatisticsRefreshedAtText)
                || string.Equals(LastStatisticsRefreshedAtText, "尚未加载", StringComparison.Ordinal))
            {
                return "统计已更新";
            }

            return $"统计已更新 · {LastStatisticsRefreshedAtText}";
        }
    }

    public WatchProfileSnapshot? Profile
    {
        get => _profile;
        private set => SetProperty(ref _profile, value);
    }

    public bool HasProfile => Profile?.HasProfile == true;

    public bool ShowProfileEmptyState => !HasProfile && (IsProfileInsufficient || HasProfileError);

    public InsightModuleState ProfileModuleState
    {
        get
        {
            if (IsLoadingProfile)
            {
                return new InsightModuleState("loading", "画像生成中", "正在读取或生成画像，这不会刷新统计或推荐。", true);
            }

            if (HasProfile && HasProfileWarnings)
            {
                return new InsightModuleState("cached-fallback", "使用缓存画像", "当前显示可用缓存；警告信息已在画像概览中列出。", true);
            }

            if (IsProfileInsufficient)
            {
                return new InsightModuleState("data-insufficient", "画像数据不足", BuildSafeStatusMessage(ProfileInsufficientReason, "继续观看或标记影片后再生成画像。"), true);
            }

            if (HasProfileError)
            {
                var safeMessage = BuildSafeStatusMessage(ProfileErrorMessage, "画像生成失败，请稍后重试。");
                return IsConfigMissingMessage(ProfileErrorMessage)
                    ? new InsightModuleState("config-missing", "画像配置缺失", "画像服务配置缺失，请在设置页检查 AI 接口和观影画像模型。", true)
                    : new InsightModuleState("generation-failed", "画像生成失败", safeMessage, true);
            }

            return HasProfile
                ? new InsightModuleState("ready", "画像已生成", "画像模块已有可展示数据。", false)
                : new InsightModuleState("empty", "画像尚未生成", "进入页面后会尝试加载画像；数据不足时会显示明确原因。", true);
        }
    }

    public bool ShowProfileModuleState => ProfileModuleState.IsVisible;

    public string ProfileSummaryText { get; private set; } = string.Empty;

    public string ProfileHeroTitle { get; private set; } = "画像正在学习你的口味";

    public string ProfileHeroSubtitle { get; private set; } = "继续观看、标记喜爱/想看/不想看后，这里会形成更稳定的观影画像。";

    public string ProfilePersonaType { get; private set; } = "--";

    public string ProfilePersonaTitle { get; private set; } = "--";

    public string ProfilePersonaDescription { get; private set; } = string.Empty;

    public string PersonaPosterGender { get; private set; } = PersonaPosterDefaultGender;

    public string PersonaPosterImageUri { get; private set; } = string.Empty;

    public string PersonaPosterFrameUri { get; private set; } = ResolvePersonaFrameUri(PersonaPosterFallbackKey);

    public bool HasPersonaPoster => !string.IsNullOrWhiteSpace(PersonaPosterImageUri);

    public bool HasPersonaPosterFrame => !string.IsNullOrWhiteSpace(PersonaPosterFrameUri);

    public string ProfilePersonaConfidenceText { get; private set; } = "0%";

    public double ProfilePersonaConfidenceValue { get; private set; }

    public string ProfileQuadrantName { get; private set; } = "--";

    public string ProfileQuadrantDescription { get; private set; } = string.Empty;

    public string ProfileXAxisText { get; private set; } = "0";

    public string ProfileYAxisText { get; private set; } = "0";

    public double ProfileQuadrantPointX { get; private set; } = 190d;

    public double ProfileQuadrantPointY { get; private set; } = 110d;

    public string ProfileWatchVsLikeConclusion { get; private set; } = string.Empty;

    public string NegativeSummaryText { get; private set; } = "负反馈样本较少。";

    public string CalendarMonthText { get; private set; } = "本月";

    public string StatisticsRangeText => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "全部" : "本月";

    public bool IsMonthRangeSelected => _statisticsTimeRange == WatchStatisticsTimeRange.Month;

    public bool IsAllRangeSelected => _statisticsTimeRange == WatchStatisticsTimeRange.All;

    public string OverviewTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "当前状态总览" : "本月状态新增";

    public string OverviewSubtitle => _statisticsTimeRange == WatchStatisticsTimeRange.All
        ? "显示当前全部已标记状态，按 TMDB 去重；全部范围不显示月度对比。"
        : "显示本月新增状态；较上月变化来自状态变更历史。";

    public string TotalWatchTimeTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "累计观影时长" : "本月观影时长";

    public string WatchCountTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "累计看过" : "本月看过";

    public string FrequentTagsTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "累计高频标签" : "本月高频标签";

    public string PreferenceGraphTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "累计偏好图谱" : "本月偏好图谱";

    public string TagRankingTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "累计标签排行" : "本月标签排行";

    public string ViewingTimeTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "长期常看时间" : "本月常看时间";

    public string DurationDistributionTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "长期常看片长" : "本月常看片长";

    public string TasteCombinationTitle => _statisticsTimeRange == WatchStatisticsTimeRange.All ? "累计口味组合" : "本月口味组合";

    public string PeakViewingTimeText { get; private set; } = "暂无常看时间段";

    public string DominantDurationText { get; private set; } = "常看片长暂无数据";

    public string PreferenceBubbleLegendText { get; private set; } = "气泡大小代表标签出现次数，颜色区分类型与情绪标签。";

    public string TasteGraphLegendText { get; private set; } = "连线越粗代表组合共现越频繁，节点代表参与组合的标签。";

    public string TotalWatchTimeText { get; private set; } = "0分钟";

    public string MonthlyWatchCountText { get; private set; } = "0部";

    public string MonthlyWatchDaysTitle { get; private set; } = "本月观影天数";

    public string ContinuousWatchDaysTitle { get; private set; } = "本月最长连续";

    public string MostActiveDateTitle { get; private set; } = "本月最活跃日";

    public string MonthlyWatchDaysText { get; private set; } = "0天";

    public string ContinuousWatchDaysText { get; private set; } = "0天";

    public string MostActiveDateText { get; private set; } = "--";

    public string MostActiveDateWatchText { get; private set; } = "0分钟";

    public bool CanGoPreviousCalendarMonth => Statistics.CalendarMonth > Statistics.EarliestCalendarMonth;

    public bool CanGoNextCalendarMonth => Statistics.CalendarMonth < Statistics.LatestCalendarMonth;

    public bool ShowReturnToCurrentMonth => Statistics.CalendarMonth != default
        && Statistics.CalendarMonth != GetCurrentMonthStart();

    public bool HasAnyStatisticsData => Statistics.HasAnyData;

    public InsightModuleState StatisticsModuleState
    {
        get
        {
            if (IsLoadingStatistics)
            {
                return new InsightModuleState("loading", "统计刷新中", "正在刷新本地统计，不会触发画像生成或推荐变化。", true);
            }

            if (HasStatisticsError)
            {
                return new InsightModuleState(
                    "error",
                    "统计加载失败",
                    BuildSafeStatusMessage(StatisticsErrorMessage, "统计加载失败，请稍后重试。"),
                    true);
            }

            if (!HasAnyStatisticsData)
            {
                return new InsightModuleState(
                    "empty",
                    "暂无统计数据",
                    BuildSafeStatusMessage(Statistics.EmptyReason, "识别或标记影片后，这里会显示 Movie-only 统计。"),
                    true);
            }

            if (HasWarningMessages)
            {
                return new InsightModuleState("cached-fallback", "统计已保留", "部分刷新警告已在统计页内列出，当前统计不会触发画像刷新。", true);
            }

            return new InsightModuleState("ready", "统计已就绪", "统计模块已有可展示数据。", false);
        }
    }

    public bool ShowStatisticsModuleState => StatisticsModuleState.IsVisible;

    public bool HasWatchHistoryData => Statistics.HasWatchHistoryData;

    public bool HasMonthlyFrequentTags => MonthlyFrequentTags.Count > 0;

    public bool HasWarningMessages => WarningMessages.Count > 0;

    public bool HasPreferenceBubbles => PreferenceBubbles.Count > 0;

    public bool HasMonthlyRankingData => MonthlyRankingGroups.Any(x => x.Items.Count > 0);

    public bool HasRhythmData => Statistics.HasWatchHistoryData;

    public bool HasTasteCombinationData => TasteCombinationTop10.Count > 0 || TasteGraphNodes.Count > 0;

    public bool HasWatchLikeData => WatchLikeGroups.Any(x => x.Items.Count > 0);

    public bool HasProfileKeywords => ProfileKeywords.Count > 0;

    public bool HasProfileSummary => !string.IsNullOrWhiteSpace(ProfileSummaryText);

    public bool HasProfileDna => ProfileDnaItems.Count > 0;

    public bool HasProfileWarnings => ProfileWarnings.Count > 0;

    public bool HasProfileCaveats => ProfileCaveats.Count > 0;

    public bool HasPreferredGenres => PreferredGenres.Count > 0;

    public bool HasPreferredEmotions => PreferredEmotions.Count > 0;

    public bool HasPreferredScenes => PreferredScenes.Count > 0;

    public bool HasPreferredCountries => PreferredCountries.Count > 0;

    public bool HasPreferredLanguages => PreferredLanguages.Count > 0;

    public bool HasAvoidGenres => AvoidGenres.Count > 0;

    public bool HasAvoidEmotions => AvoidEmotions.Count > 0;

    public bool HasAvoidScenes => AvoidScenes.Count > 0;

    public bool HasAnyAvoidDirection => HasAvoidGenres || HasAvoidEmotions || HasAvoidScenes || !string.IsNullOrWhiteSpace(NegativeSummaryText);

    public bool HasFuturePreference => LikelyToEnjoy.Count > 0 || LessLikelyToEnjoy.Count > 0;

    public bool HasProfileWatchLikeData => ProfileWatchLikeGroups.Any(x => x.Items.Count > 0);

    public override bool IsRefreshing => IsLoadingStatistics || IsLoadingProfile;

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        _isActive = true;
        SelectedTab = ProfileTabKey;
        var shouldForceRefresh = _statisticsRefreshPendingOnActivate;
        _statisticsRefreshPendingOnActivate = false;
        _ = LoadProfileAsync(ProfileLoadSource.Activate, forceRefresh: false, cancellationToken);
        await RefreshStatisticsAsync(
            StatisticsRefreshSource.Activate,
            forceRefresh: shouldForceRefresh,
            cancellationToken);
    }

    public override void Deactivate()
    {
        _isActive = false;
        _dataChangedRefreshDebounceCts?.Cancel();
        _dataChangedRefreshDebounceCts?.Dispose();
        _dataChangedRefreshDebounceCts = null;
    }

    private void SelectProfileTab()
    {
        SelectedTab = ProfileTabKey;
        if (Profile is null && !IsLoadingProfile)
        {
            _ = LoadProfileAsync(ProfileLoadSource.Tab, forceRefresh: false);
        }
    }

    private void SelectStatisticsTab()
    {
        SelectedTab = StatisticsTabKey;
    }

    private async Task LoadProfileAsync(
        ProfileLoadSource source,
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        if (IsLoadingProfile)
        {
            Log($"watch-insights-profile-refresh-skipped reason=already-running source={FormatProfileLoadSource(source)}");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        Log($"watch-insights-profile-load-start source={FormatProfileLoadSource(source)}");
        IsLoadingProfile = true;
        ProfileErrorMessage = string.Empty;

        try
        {
            var profile = await _profileService.GetProfileAsync(forceRefresh, cancellationToken);
            ApplyProfile(profile);
            stopwatch.Stop();
            Log(
                "watch-insights-profile-load-complete "
                + $"source={FormatProfileLoadSource(source)} "
                + $"hasProfile={profile.HasProfile} "
                + $"insufficient={!profile.CanGenerateProfile} "
                + $"warning={profile.WarningMessages.Count > 0} "
                + $"elapsedMs={stopwatch.ElapsedMilliseconds}");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            if (Profile?.HasProfile == true)
            {
                AddProfileWarning("画像刷新失败，当前显示上一次画像缓存。");
            }
            else
            {
                ProfileErrorMessage = $"画像加载失败：{BuildSafeStatusMessage(exception.Message, "未知错误。")}";
                ProfileStatusText = "生成失败";
            }

            Log($"watch-insights-profile-load-failed source={FormatProfileLoadSource(source)} errorType={exception.GetType().Name}");
        }
        finally
        {
            IsLoadingProfile = false;
        }
    }

    private void OnDataChanged(object? sender, AppDataChangedEventArgs e)
    {
        if (!ShouldRefreshStatisticsForDataChange(e))
        {
            return;
        }

        _ = Application.Current.Dispatcher.InvokeAsync(() => ScheduleDataChangedStatisticsRefresh(e.Reason));
    }

    private void ScheduleDataChangedStatisticsRefresh(AppDataChangeReason reason)
    {
        if (!_isActive)
        {
            _statisticsRefreshPendingOnActivate = true;
            Log($"watch-insights-statistics-refresh-skipped reason=inactive source=data-changed change={reason}");
            return;
        }

        if (IsLoadingStatistics)
        {
            _statisticsRefreshQueuedAfterCurrent = true;
            Log($"watch-insights-statistics-refresh-skipped reason=already-running source=data-changed change={reason}");
            return;
        }

        if (_dataChangedRefreshDebounceCts is not null)
        {
            Log($"watch-insights-statistics-refresh-skipped reason=debounced source=data-changed change={reason}");
        }

        _dataChangedRefreshDebounceCts?.Cancel();
        _dataChangedRefreshDebounceCts?.Dispose();
        _dataChangedRefreshDebounceCts = new CancellationTokenSource();
        _ = RunDebouncedDataChangedStatisticsRefreshAsync(_dataChangedRefreshDebounceCts.Token);
    }

    private async Task RunDebouncedDataChangedStatisticsRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DataChangedRefreshDebounce, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(
                () =>
                {
                    _dataChangedRefreshDebounceCts?.Dispose();
                    _dataChangedRefreshDebounceCts = null;
                    _ = RefreshStatisticsAsync(StatisticsRefreshSource.DataChanged, forceRefresh: true);
                });
        }
        catch (TaskCanceledException)
        {
        }
    }

    private static bool ShouldRefreshStatisticsForDataChange(AppDataChangedEventArgs e)
    {
        return e.LibraryChanged
               || e.PlaybackChanged
               || e.Reason is AppDataChangeReason.CollectionChanged
                   or AppDataChangeReason.MetadataChanged
                   or AppDataChangeReason.ScanChanged;
    }

    private async Task ChangeStatisticsRangeAsync(WatchStatisticsTimeRange timeRange)
    {
        if (_statisticsTimeRange == timeRange)
        {
            return;
        }

        _statisticsTimeRange = timeRange;
        Log($"watch-statistics-range-changed range={FormatStatisticsTimeRange(timeRange)}");
        RaiseStatisticsRangeChanged();
        await RefreshStatisticsAsync(StatisticsRefreshSource.Manual, forceRefresh: false);
    }

    private async Task ChangeCalendarMonthAsync(DateTime month)
    {
        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        if (Statistics.EarliestCalendarMonth != default && normalizedMonth < Statistics.EarliestCalendarMonth)
        {
            normalizedMonth = Statistics.EarliestCalendarMonth;
        }

        if (Statistics.LatestCalendarMonth != default && normalizedMonth > Statistics.LatestCalendarMonth)
        {
            normalizedMonth = Statistics.LatestCalendarMonth;
        }

        if (_calendarMonth == normalizedMonth)
        {
            return;
        }

        _calendarMonth = normalizedMonth;
        Log($"watch-statistics-calendar-month-changed month={_calendarMonth:yyyy-MM}");
        await RefreshStatisticsAsync(StatisticsRefreshSource.Manual, forceRefresh: false);
    }

    private async Task RefreshStatisticsAsync(
        StatisticsRefreshSource source,
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        if (IsLoadingStatistics)
        {
            _statisticsRefreshQueuedAfterCurrent = true;
            Log($"watch-insights-statistics-refresh-skipped reason=already-running source={FormatRefreshSource(source)}");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        Log($"watch-insights-statistics-refresh-start source={FormatRefreshSource(source)}");
        IsLoadingStatistics = true;
        StatisticsErrorMessage = string.Empty;

        try
        {
            var snapshot = await _statisticsService.GetStatisticsAsync(
                _statisticsTimeRange,
                _calendarMonth,
                forceRefresh,
                cancellationToken);
            ApplyStatistics(snapshot);
            _hasLoadedStatistics = true;
            stopwatch.Stop();
            Log($"watch-insights-statistics-refresh-complete source={FormatRefreshSource(source)} elapsedMs={stopwatch.ElapsedMilliseconds}");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            StatisticsErrorMessage = $"统计数据加载失败：{BuildSafeStatusMessage(exception.Message, "未知错误。")}";
            if (source == StatisticsRefreshSource.DataChanged && _hasLoadedStatistics)
            {
                StatisticsErrorMessage = string.Empty;
                AddWarning("统计自动刷新失败，已保留上一次统计结果。");
            }

            Log($"watch-insights-statistics-refresh-failed source={FormatRefreshSource(source)} errorType={exception.GetType().Name}");
        }
        finally
        {
            IsLoadingStatistics = false;
            if (_statisticsRefreshQueuedAfterCurrent && _isActive)
            {
                _statisticsRefreshQueuedAfterCurrent = false;
                _ = RefreshStatisticsAsync(StatisticsRefreshSource.DataChanged, forceRefresh: true);
            }
        }
    }

    private void ApplyStatistics(WatchStatisticsSnapshot snapshot)
    {
        Statistics = snapshot;
        _statisticsTimeRange = snapshot.TimeRange;
        if (snapshot.CalendarMonth != default)
        {
            _calendarMonth = snapshot.CalendarMonth;
        }

        CalendarNoticeMessage = string.Empty;
        LastStatisticsRefreshedAtText = snapshot.GeneratedAtUtc == default
            ? "尚未加载"
            : $"上次刷新 {snapshot.GeneratedAtUtc.ToLocalTime():MM-dd HH:mm}";

        BuildWarningMessages(snapshot);
        BuildOverview(snapshot);
        BuildMonthlyTags(snapshot);
        BuildCalendar(snapshot);
        BuildPreferenceBubbles(snapshot);
        BuildMonthlyRankings(snapshot);
        BuildRhythm(snapshot);
        BuildTasteCombinationGraph(snapshot);
        BuildWatchLikeComparison(snapshot);
        if (Profile is not null)
        {
            BuildProfileWatchLikeComparison(Profile);
        }

        RaiseDisplayStateChanged();
        RaiseStatisticsRangeChanged();
        RaiseCalendarNavigationChanged();
    }

    private void ApplyProfile(WatchProfileSnapshot snapshot)
    {
        Profile = snapshot;
        ProfileErrorMessage = BuildSafeStatusMessage(snapshot.ErrorMessage, string.Empty);
        ProfileInsufficientReason = snapshot.CanGenerateProfile ? string.Empty : snapshot.InsufficientReason;
        ProfileStatusText = BuildProfileStatusText(snapshot);
        LastProfileRefreshedAtText = snapshot.Meta.GeneratedAtUtc == default
            ? "尚未生成"
            : $"上次生成 {snapshot.Meta.GeneratedAtUtc.ToLocalTime():MM-dd HH:mm}";

        ProfileSummaryText = FormatProfileSummaryText(snapshot.Summary.Text);
        ProfilePersonaType = string.IsNullOrWhiteSpace(snapshot.Persona.Type) ? "--" : snapshot.Persona.Type;
        ProfilePersonaTitle = string.IsNullOrWhiteSpace(snapshot.Persona.Title) ? ProfilePersonaType : snapshot.Persona.Title;
        ProfilePersonaDescription = snapshot.Persona.Description;
        ApplyPersonaPoster(ProfilePersonaType);
        ProfilePersonaConfidenceValue = Math.Clamp(snapshot.Persona.Confidence, 0, 100);
        ProfilePersonaConfidenceText = $"{ProfilePersonaConfidenceValue:0}%";
        ProfileQuadrantName = string.IsNullOrWhiteSpace(snapshot.Quadrant.QuadrantName) ? "--" : snapshot.Quadrant.QuadrantName;
        ProfileQuadrantDescription = snapshot.Quadrant.Description;
        var xAxis = Math.Clamp(snapshot.Quadrant.XAxisScore, -100, 100);
        var yAxis = Math.Clamp(snapshot.Quadrant.YAxisScore, -100, 100);
        ProfileXAxisText = xAxis.ToString();
        ProfileYAxisText = yAxis.ToString();
        ProfileQuadrantPointX = 24d + (xAxis + 100d) / 200d * 244d;
        ProfileQuadrantPointY = 28d + (100d - yAxis) / 200d * 188d;
        ProfileHeroTitle = snapshot.HasProfile
            ? $"你更像：{ProfilePersonaType}"
            : "画像正在学习你的口味";
        ProfileHeroSubtitle = snapshot.HasProfile
            ? $"当前口味落在「{ProfileQuadrantName}」。下面的总结、DNA 和行为差异会一起解释这份判断。"
            : "继续观看、标记喜爱/想看/不想看后，这里会形成更稳定的观影画像。";
        NegativeSummaryText = string.IsNullOrWhiteSpace(snapshot.Dislikes.NegativeSummary)
            ? "负反馈样本较少。"
            : snapshot.Dislikes.NegativeSummary;

        ReplaceChips(ProfileKeywords, snapshot.Summary.Keywords.Take(6));
        ReplaceDna(snapshot.DNA);
        ReplaceWarnings(snapshot.WarningMessages.Concat(snapshot.Meta.WarningMessages));
        ReplaceMessages(ProfileCaveats, snapshot.Caveats);
        ReplaceChips(PreferredGenres, snapshot.Likes.PreferredGenres);
        ReplaceChips(PreferredEmotions, snapshot.Likes.PreferredEmotions);
        ReplaceChips(PreferredScenes, snapshot.Likes.PreferredScenes);
        ReplaceChips(PreferredCountries, snapshot.Likes.PreferredCountries);
        ReplaceChips(PreferredLanguages, snapshot.Likes.PreferredLanguages);
        ReplaceChips(AvoidGenres, snapshot.Dislikes.AvoidGenres);
        ReplaceChips(AvoidEmotions, snapshot.Dislikes.AvoidEmotions);
        ReplaceChips(AvoidScenes, snapshot.Dislikes.AvoidScenes);
        ReplaceChips(LikelyToEnjoy, snapshot.FuturePreference.LikelyToEnjoy);
        ReplaceChips(LessLikelyToEnjoy, snapshot.FuturePreference.LessLikelyToEnjoy);
        BuildProfileWatchLikeComparison(snapshot);
        RaiseProfileDisplayStateChanged();
    }

    private void ApplyPersonaPoster(string personaType)
    {
        PersonaPosterGender = PersonaPosterDefaultGender;
        var personaKey = ResolvePersonaKey(personaType);
        PersonaPosterImageUri = ResolvePersonaPosterUri(personaKey, PersonaPosterGender);
        PersonaPosterFrameUri = ResolvePersonaFrameUri(personaKey);
    }

    private static string ResolvePersonaKey(string personaType)
    {
        return !string.IsNullOrWhiteSpace(personaType)
            && PersonaTypeKeys.TryGetValue(personaType.Trim(), out var key)
                ? key
                : PersonaPosterFallbackKey;
    }

    private static string ResolvePersonaPosterUri(string personaKey, string gender)
    {
        foreach (var candidate in EnumeratePersonaPosterCandidates(personaKey, gender))
        {
            if (ResourceExists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumeratePersonaPosterCandidates(string personaKey, string gender)
    {
        var normalizedGender = string.Equals(gender, "male", StringComparison.OrdinalIgnoreCase)
            ? "male"
            : "female";
        foreach (var extension in PersonaPosterExtensions)
        {
            yield return BuildPersonaPosterUri(personaKey, normalizedGender, extension);
        }

        foreach (var extension in PersonaPosterExtensions)
        {
            yield return BuildPersonaPosterUri(personaKey, "female", extension);
        }

        foreach (var extension in PersonaPosterExtensions)
        {
            yield return BuildPersonaPosterUri(personaKey, "male", extension);
        }

        foreach (var extension in PersonaPosterExtensions)
        {
            yield return BuildPersonaAssetUri($"default_{normalizedGender}{extension}");
        }

        foreach (var extension in PersonaPosterExtensions)
        {
            yield return BuildPersonaAssetUri($"default{extension}");
        }
    }

    private static string BuildPersonaPosterUri(string personaKey, string gender, string extension)
    {
        return BuildPersonaAssetUri(personaKey, $"{personaKey}_{gender}{extension}");
    }

    private static string ResolvePersonaFrameUri(string personaKey)
    {
        foreach (var candidate in EnumeratePersonaFrameCandidates(personaKey))
        {
            if (ResourceExists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumeratePersonaFrameCandidates(string personaKey)
    {
        if (PersonaFrameColors.TryGetValue(personaKey, out var color))
        {
            yield return BuildPersonaFrameUri(color);

            if (!string.Equals(color, PersonaFrameDefaultColor, StringComparison.OrdinalIgnoreCase))
            {
                yield return BuildPersonaFrameUri(PersonaFrameDefaultColor);
            }
        }
        else
        {
            yield return BuildPersonaFrameUri(PersonaFrameDefaultColor);
        }

        yield return PersonaFrameDefaultUri;
    }

    private static string BuildPersonaFrameUri(string color)
    {
        return BuildPersonaAssetUri("Frames", $"persona_card_frame_{color}.png");
    }

    private static bool ResourceExists(string uri)
    {
        try
        {
            var resourceUri = new Uri(uri, UriKind.Absolute);
            return resourceUri.IsFile && File.Exists(resourceUri.LocalPath);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildPersonaAssetUri(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "WatchPersonas", fileName);
        return new Uri(path, UriKind.Absolute).AbsoluteUri;
    }

    private static string BuildPersonaAssetUri(string folder, string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "WatchPersonas", folder, fileName);
        return new Uri(path, UriKind.Absolute).AbsoluteUri;
    }

    private void BuildWarningMessages(WatchStatisticsSnapshot snapshot)
    {
        WarningMessages.Clear();
        foreach (var warning in snapshot.WarningMessages.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            WarningMessages.Add(new WarningMessageItem(warning));
        }
    }

    private void AddWarning(string message)
    {
        if (WarningMessages.Any(x => string.Equals(x.Text, message, StringComparison.Ordinal)))
        {
            return;
        }

        WarningMessages.Add(new WarningMessageItem(message));
        OnPropertyChanged(nameof(HasWarningMessages));
        OnPropertyChanged(nameof(StatisticsModuleState));
        OnPropertyChanged(nameof(ShowStatisticsModuleState));
    }

    private void AddProfileWarning(string message)
    {
        if (ProfileWarnings.Any(x => string.Equals(x.Text, message, StringComparison.Ordinal)))
        {
            return;
        }

        ProfileWarnings.Add(new WarningMessageItem(message));
        OnPropertyChanged(nameof(HasProfileWarnings));
        OnPropertyChanged(nameof(ProfileModuleState));
        OnPropertyChanged(nameof(ShowProfileModuleState));
    }

    private static string BuildProfileStatusText(WatchProfileSnapshot snapshot)
    {
        if (!snapshot.CanGenerateProfile)
        {
            return "数据不足";
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
        {
            return snapshot.HasProfile ? "使用缓存" : "生成失败";
        }

        if (snapshot.IsUnchanged)
        {
            return "已是最新";
        }

        if (snapshot.LoadedFromCache)
        {
            return snapshot.WarningMessages.Count > 0 ? "使用缓存 / 待自动刷新" : "使用缓存";
        }

        return snapshot.HasProfile ? "已生成" : "尚未生成";
    }

    private void ReplaceChips(ICollection<TagChipItem> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            target.Add(new TagChipItem(item.Trim(), string.Empty));
        }
    }

    private static string FormatProfileSummaryText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var paragraphs = normalized
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(FormatIndentedParagraph)
            .Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static string FormatIndentedParagraph(string paragraph)
    {
        var trimmed = paragraph.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : "　　" + trimmed.TrimStart('　');
    }

    private static void ReplaceMessages(ICollection<WarningMessageItem> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            target.Add(new WarningMessageItem(item.Trim()));
        }
    }

    private void ReplaceWarnings(IEnumerable<string> source)
    {
        ReplaceMessages(ProfileWarnings, source);
        OnPropertyChanged(nameof(ProfileModuleState));
        OnPropertyChanged(nameof(ShowProfileModuleState));
    }

    private void ReplaceDna(IEnumerable<WatchProfileDnaGene> genes)
    {
        ProfileDnaItems.Clear();
        foreach (var gene in genes)
        {
            var isPace = string.Equals(gene.Gene, "节奏基因", StringComparison.Ordinal);
            var isExploration = string.Equals(gene.Gene, "探索基因", StringComparison.Ordinal);
            var isProgressGene = isPace || isExploration;
            IReadOnlyList<TagChipItem> tags = isProgressGene
                ? []
                : BuildDnaTags(gene).Take(3).Select(x => new TagChipItem(x, string.Empty)).ToList();
            ProfileDnaItems.Add(new ProfileDnaItem(
                gene.Gene,
                BuildDnaIconText(gene.Gene),
                BuildDnaSubtitle(gene.Gene),
                tags,
                Math.Clamp(gene.Score, 0, 100),
                gene.Description ?? string.Empty,
                isProgressGene,
                isPace ? "慢热" : isExploration ? "稳定" : string.Empty,
                isPace ? "紧凑" : isExploration ? "新鲜" : string.Empty));
            Log($"watch-profile-ui-dna-tags-projected gene={gene.Gene} tagCount={tags.Count}");
        }

        Log(
            "watch-profile-ui-projection-built "
            + $"hasNarrative={ProfileDnaItems.Any(x => string.Equals(x.Gene, "叙事基因", StringComparison.Ordinal) && x.HasTags)} "
            + $"keywordCount={ProfileKeywords.Count} "
            + $"dnaCount={ProfileDnaItems.Count}");
    }

    private static IEnumerable<string> BuildDnaTags(WatchProfileDnaGene gene)
    {
        if (gene.Tags is { Count: > 0 })
        {
            return gene.Tags;
        }

        if (string.IsNullOrWhiteSpace(gene.Label))
        {
            return [];
        }

        return gene.Label
            .Split(['、', ',', '，', '/', '|', ';', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string BuildDnaIconText(string gene)
    {
        return gene switch
        {
            "类型基因" => "类",
            "情绪基因" => "绪",
            "场景基因" => "景",
            "节奏基因" => "奏",
            "叙事基因" => "叙",
            "探索基因" => "探",
            _ => "因"
        };
    }

    private static string BuildDnaSubtitle(string gene)
    {
        return gene switch
        {
            "类型基因" => "你偏爱的影片类型",
            "情绪基因" => "你喜欢的情绪体验",
            "场景基因" => "你常进入的观影场景",
            "节奏基因" => "你偏好的叙事节奏",
            "叙事基因" => "你在意的叙事元素",
            "探索基因" => "你探索新题材的倾向",
            _ => "画像基因"
        };
    }

    private void BuildProfileWatchLikeComparison(WatchProfileSnapshot profile)
    {
        ProfileWatchLikeGroups.Clear();
        var watched = profile.WatchVsLike.OftenWatchedTypes.Count > 0
            ? profile.WatchVsLike.OftenWatchedTypes
            : Statistics.OftenWatchedTop3.Select(x => x.Label).ToList();
        var liked = profile.WatchVsLike.OftenLikedTypes.Count > 0
            ? profile.WatchVsLike.OftenLikedTypes
            : Statistics.OftenLikedTop3.Select(x => x.Label).ToList();
        var wanted = profile.WatchVsLike.OftenWantedTypes.Count > 0
            ? profile.WatchVsLike.OftenWantedTypes
            : Statistics.OftenWantedTop3.Select(x => x.Label).ToList();

        ProfileWatchLikeGroups.Add(new ProfileListGroup("经常观看", "你最常看的类型", BuildProfileListItems(watched)));
        ProfileWatchLikeGroups.Add(new ProfileListGroup("经常喜爱", "你真正喜欢的类型", BuildProfileListItems(liked)));
        ProfileWatchLikeGroups.Add(new ProfileListGroup("经常想看", "你最想探索的类型", BuildProfileListItems(wanted)));
        ProfileWatchVsLikeConclusion = string.IsNullOrWhiteSpace(profile.WatchVsLike.Conclusion)
            ? "基于本地统计展示。"
            : profile.WatchVsLike.Conclusion;
        OnPropertyChanged(nameof(ProfileWatchVsLikeConclusion));
        OnPropertyChanged(nameof(HasProfileWatchLikeData));
    }

    private static IReadOnlyList<ProfileListItem> BuildProfileListItems(IEnumerable<string> labels)
    {
        return labels
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select((label, index) => new ProfileListItem(index + 1, label.Trim(), Math.Max(44d, 100d - index * 24d)))
            .ToList();
    }

    private void BuildOverview(WatchStatisticsSnapshot snapshot)
    {
        OverviewCards.Clear();
        OverviewCards.Add(new(
            "已看",
            "已完成观看的影片",
            snapshot.WatchedCount.ToString(),
            "部",
            FormatDelta(snapshot.TimeRange, snapshot.WatchedDeltaFromLastWeek),
            "已",
            "watched"));
        OverviewCards.Add(new(
            "喜爱",
            "主动标记喜欢的影片",
            snapshot.FavoriteCount.ToString(),
            "部",
            FormatDelta(snapshot.TimeRange, snapshot.FavoriteDeltaFromLastWeek),
            "喜",
            "favorite"));
        OverviewCards.Add(new(
            "想看",
            "计划稍后观看的影片",
            snapshot.WantToWatchCount.ToString(),
            "部",
            FormatDelta(snapshot.TimeRange, snapshot.WantToWatchDeltaFromLastWeek),
            "想",
            "want"));
        OverviewCards.Add(new(
            "不想看",
            "明确排除的影片",
            snapshot.NotInterestedCount.ToString(),
            "部",
            FormatDelta(snapshot.TimeRange, snapshot.NotInterestedDeltaFromLastWeek),
            "避",
            "negative"));

        TotalWatchTimeText = FormatSeconds(snapshot.TotalWatchSeconds);
        MonthlyWatchCountText = $"{snapshot.MonthlyWatchCount}部";
        OnPropertyChanged(nameof(TotalWatchTimeText));
        OnPropertyChanged(nameof(MonthlyWatchCountText));
    }

    private void BuildMonthlyTags(WatchStatisticsSnapshot snapshot)
    {
        MonthlyFrequentTags.Clear();
        foreach (var tag in snapshot.MonthlyFrequentTags.Take(10))
        {
            MonthlyFrequentTags.Add(new TagChipItem(tag.Label, $"{tag.Count}部"));
        }
    }

    private void BuildCalendar(WatchStatisticsSnapshot snapshot)
    {
        CalendarCells.Clear();
        CalendarMonthText = snapshot.CalendarMonth == default
            ? "本月"
            : snapshot.CalendarMonth.ToString("yyyy年MM月");

        var firstDay = snapshot.CalendarMonth == default
            ? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
            : snapshot.CalendarMonth;
        var leadingPlaceholderCount = ((int)firstDay.DayOfWeek + 6) % 7;
        for (var i = 0; i < leadingPlaceholderCount; i++)
        {
            CalendarCells.Add(CalendarDayCell.Placeholder());
        }

        foreach (var day in snapshot.CalendarDays)
        {
            CalendarCells.Add(new CalendarDayCell(
                day.Date,
                day.Date.Day.ToString(),
                FormatSeconds(day.WatchSeconds),
                day.WatchCount,
                day.HeatLevel,
                day.HasValidWatch,
                IsPlaceholder: false));
        }

        MonthlyWatchDaysText = $"{snapshot.MonthlyWatchDays}天";
        ContinuousWatchDaysText = $"{snapshot.ContinuousWatchDays}天";
        MostActiveDateText = snapshot.MostActiveDate.HasValue
            ? snapshot.MostActiveDate.Value.ToString("MM月dd日")
            : "--";
        MostActiveDateWatchText = FormatSeconds(snapshot.MostActiveDateWatchSeconds);
        var monthLabel = snapshot.CalendarMonth == default ? "本月" : $"{snapshot.CalendarMonth.Month}月";
        MonthlyWatchDaysTitle = $"{monthLabel}观影天数";
        ContinuousWatchDaysTitle = $"{monthLabel}最长连续";
        MostActiveDateTitle = $"{monthLabel}最活跃日";
        OnPropertyChanged(nameof(CalendarMonthText));
        OnPropertyChanged(nameof(MonthlyWatchDaysTitle));
        OnPropertyChanged(nameof(ContinuousWatchDaysTitle));
        OnPropertyChanged(nameof(MostActiveDateTitle));
        OnPropertyChanged(nameof(MonthlyWatchDaysText));
        OnPropertyChanged(nameof(ContinuousWatchDaysText));
        OnPropertyChanged(nameof(MostActiveDateText));
        OnPropertyChanged(nameof(MostActiveDateWatchText));
    }

    private void BuildPreferenceBubbles(WatchStatisticsSnapshot snapshot)
    {
        PreferenceBubbles.Clear();
        var rawItems = snapshot.TypeDistribution
            .Take(7)
            .Select(x => (x.Label, Kind: "类型", x.Count))
            .Concat(snapshot.EmotionDistribution
                .Take(7)
                .Select(x => (x.Label, Kind: "情绪", x.Count)))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Take(PreferenceBubbleSlots.Length)
            .ToList();
        var maxCount = rawItems.Count == 0 ? 0 : rawItems.Max(x => x.Count);

        for (var index = 0; index < rawItems.Count; index++)
        {
            var item = rawItems[index];
            var size = CalculateBubbleSize(item.Count, maxCount);
            var slot = PreferenceBubbleSlots[index];
            PreferenceBubbles.Add(new BubbleTagItem(
                item.Label,
                item.Kind,
                item.Count,
                size,
                CalculateBubbleCoordinate(slot.X, size, PreferenceBubbleCanvasWidth),
                CalculateBubbleCoordinate(slot.Y, size, PreferenceBubbleCanvasHeight)));
        }
    }

    private void BuildMonthlyRankings(WatchStatisticsSnapshot snapshot)
    {
        MonthlyRankingGroups.Clear();
        MonthlyRankingGroups.Add(new RankingGroup("类型 Top3", BuildRankingItems(snapshot.MonthlyTypeTagTop3)));
        MonthlyRankingGroups.Add(new RankingGroup("情绪 Top3", BuildRankingItems(snapshot.MonthlyEmotionTagTop3)));
        MonthlyRankingGroups.Add(new RankingGroup("场景 Top3", BuildRankingItems(snapshot.MonthlySceneTagTop3)));
    }

    private void BuildRhythm(WatchStatisticsSnapshot snapshot)
    {
        ViewingTimeBuckets.Clear();
        var maxWatchSeconds = snapshot.ViewingTimeDistribution.Count == 0
            ? 0
            : snapshot.ViewingTimeDistribution.Max(x => x.WatchSeconds);
        foreach (var bucket in snapshot.ViewingTimeDistribution)
        {
            ViewingTimeBuckets.Add(new TimeBucketItem(
                bucket.Label,
                FormatSeconds(bucket.WatchSeconds),
                bucket.WatchCount,
                CalculateProgress(bucket.WatchSeconds, maxWatchSeconds)));
        }

        var peakBucket = snapshot.ViewingTimeDistribution
            .Where(x => x.WatchSeconds > 0)
            .OrderByDescending(x => x.WatchSeconds)
            .ThenBy(x => x.StartHour)
            .FirstOrDefault();
        PeakViewingTimeText = peakBucket is null
            ? "暂无常看时间段"
            : $"你最常在 {peakBucket.Label} 观影";

        WeekPartItems.Clear();
        WeekPartItems.Add(new(
            "周内",
            FormatSeconds(snapshot.WeekdayWeekendStats.WeekdayWatchSeconds),
            $"日均 {FormatSeconds(snapshot.WeekdayWeekendStats.WeekdayAverageSeconds)}",
            FormatPercent(snapshot.WeekdayWeekendStats.WeekdayRatio),
            snapshot.WeekdayWeekendStats.WeekdayRatio * 100d));
        WeekPartItems.Add(new(
            "周末",
            FormatSeconds(snapshot.WeekdayWeekendStats.WeekendWatchSeconds),
            $"日均 {FormatSeconds(snapshot.WeekdayWeekendStats.WeekendAverageSeconds)}",
            FormatPercent(snapshot.WeekdayWeekendStats.WeekendRatio),
            snapshot.WeekdayWeekendStats.WeekendRatio * 100d));

        DurationBuckets.Clear();
        foreach (var item in snapshot.DurationDistribution)
        {
            DurationBuckets.Add(new DurationBucketItem(
                FormatDurationBucketName(item.Label),
                $"{item.Count}部",
                FormatPercent(item.Percent),
                item.Percent * 100d));
        }

        var dominantDuration = snapshot.DurationDistribution
            .Where(x => x.Count > 0)
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.MinMinutes)
            .FirstOrDefault();
        DominantDurationText = dominantDuration is null
            ? "常看片长暂无数据"
            : $"最多：{FormatDurationBucketName(dominantDuration.Label)}";

        OnPropertyChanged(nameof(PeakViewingTimeText));
        OnPropertyChanged(nameof(DominantDurationText));
    }

    private static List<TasteCombinationNode> SelectTasteNodes(
        IEnumerable<TasteCombinationNode> nodes,
        string kind)
    {
        return nodes
            .Where(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Take(TasteGraphNodeLimit)
            .ToList();
    }

    private static void AddTasteCount(
        IDictionary<string, int> counts,
        string label,
        int count)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var normalized = label.Trim();
        counts[normalized] = counts.TryGetValue(normalized, out var current) ? current + count : count;
    }

    private void BuildTasteCanvasGraph(WatchStatisticsSnapshot snapshot)
    {
        var nodeById = snapshot.TasteCombinationNodes.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var graphNodeById = new Dictionary<string, TasteGraphNodeItem>(StringComparer.OrdinalIgnoreCase);
        var typeNodes = SelectTasteNodes(snapshot.TasteCombinationNodes, "type");
        var emotionNodes = SelectTasteNodes(snapshot.TasteCombinationNodes, "emotion");
        var sceneNodes = SelectTasteNodes(snapshot.TasteCombinationNodes, "scene");

        AddTasteGraphNodes(typeNodes, TasteGraphTypeX, graphNodeById);
        AddTasteGraphNodes(emotionNodes, TasteGraphEmotionX, graphNodeById);
        AddTasteGraphNodes(sceneNodes, TasteGraphSceneX, graphNodeById);

        AddTasteGraphLinks(snapshot.TasteCombinationEdges, nodeById, graphNodeById, "type", "emotion");
        AddTasteGraphLinks(snapshot.TasteCombinationEdges, nodeById, graphNodeById, "emotion", "scene");

        if (snapshot.TasteCombinationTop10.Count > 0 && (TasteGraphNodes.Count == 0 || TasteGraphLinks.Count == 0))
        {
            BuildTasteCanvasGraphFallbackFromCombinations(snapshot);
        }
    }

    private void AddTasteGraphNodes(
        IReadOnlyList<TasteCombinationNode> nodes,
        double x,
        IDictionary<string, TasteGraphNodeItem> graphNodeById)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var graphNode = new TasteGraphNodeItem(
                node.Id,
                node.Label,
                node.Kind,
                x,
                TasteGraphFirstNodeY + index * TasteGraphNodeSpacingY,
                TasteGraphNodeWidth,
                TasteGraphNodeHeight,
                node.Count);
            TasteGraphNodes.Add(graphNode);
            graphNodeById[node.Id] = graphNode;
        }
    }

    private void AddTasteGraphLinks(
        IEnumerable<TasteCombinationEdge> edges,
        IReadOnlyDictionary<string, TasteCombinationNode> nodeById,
        IReadOnlyDictionary<string, TasteGraphNodeItem> graphNodeById,
        string sourceKind,
        string targetKind)
    {
        var links = edges
            .Where(x => graphNodeById.ContainsKey(x.SourceId)
                && graphNodeById.ContainsKey(x.TargetId)
                && nodeById.TryGetValue(x.SourceId, out var source)
                && nodeById.TryGetValue(x.TargetId, out var target)
                && string.Equals(source.Kind, sourceKind, StringComparison.OrdinalIgnoreCase)
                && string.Equals(target.Kind, targetKind, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.TargetId, StringComparer.OrdinalIgnoreCase)
            .Take(TasteGraphLinkLimit)
            .ToList();
        var maxCount = links.Count == 0 ? 0 : links.Max(x => x.Count);

        foreach (var edge in links)
        {
            var source = graphNodeById[edge.SourceId];
            var target = graphNodeById[edge.TargetId];
            TasteGraphLinks.Add(new TasteGraphLinkItem(
                source.RightX,
                source.CenterY,
                target.LeftX,
                target.CenterY,
                CalculateTasteLineThickness(edge.Count, maxCount),
                edge.Count));
        }
    }

    private void BuildTasteCanvasGraphFallbackFromCombinations(WatchStatisticsSnapshot snapshot)
    {
        TasteGraphNodes.Clear();
        TasteGraphLinks.Clear();

        var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var emotionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sceneCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var typeEmotionCounts = new Dictionary<string, (string Source, string Target, int Count)>(StringComparer.OrdinalIgnoreCase);
        var emotionSceneCounts = new Dictionary<string, (string Source, string Target, int Count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in snapshot.TasteCombinationTop10)
        {
            AddTasteCount(typeCounts, item.Type, item.OccurrenceCount);
            AddTasteCount(emotionCounts, item.Emotion, item.OccurrenceCount);
            AddTasteCount(sceneCounts, item.Scene, item.OccurrenceCount);

            var typeEmotionKey = $"{item.Type}|{item.Emotion}";
            typeEmotionCounts[typeEmotionKey] = typeEmotionCounts.TryGetValue(typeEmotionKey, out var typeEmotion)
                ? (typeEmotion.Source, typeEmotion.Target, typeEmotion.Count + item.OccurrenceCount)
                : (item.Type, item.Emotion, item.OccurrenceCount);

            var emotionSceneKey = $"{item.Emotion}|{item.Scene}";
            emotionSceneCounts[emotionSceneKey] = emotionSceneCounts.TryGetValue(emotionSceneKey, out var emotionScene)
                ? (emotionScene.Source, emotionScene.Target, emotionScene.Count + item.OccurrenceCount)
                : (item.Emotion, item.Scene, item.OccurrenceCount);
        }

        var graphNodeById = new Dictionary<string, TasteGraphNodeItem>(StringComparer.OrdinalIgnoreCase);
        AddFallbackGraphNodes(typeCounts, "type", TasteGraphTypeX, graphNodeById);
        AddFallbackGraphNodes(emotionCounts, "emotion", TasteGraphEmotionX, graphNodeById);
        AddFallbackGraphNodes(sceneCounts, "scene", TasteGraphSceneX, graphNodeById);
        AddFallbackGraphLinks(typeEmotionCounts.Values, "type", "emotion", graphNodeById);
        AddFallbackGraphLinks(emotionSceneCounts.Values, "emotion", "scene", graphNodeById);
    }

    private void AddFallbackGraphNodes(
        IReadOnlyDictionary<string, int> counts,
        string kind,
        double x,
        IDictionary<string, TasteGraphNodeItem> graphNodeById)
    {
        var items = counts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(TasteGraphNodeLimit)
            .ToList();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var id = BuildTasteGraphNodeId(kind, item.Key);
            var graphNode = new TasteGraphNodeItem(
                id,
                item.Key,
                kind,
                x,
                TasteGraphFirstNodeY + index * TasteGraphNodeSpacingY,
                TasteGraphNodeWidth,
                TasteGraphNodeHeight,
                item.Value);
            TasteGraphNodes.Add(graphNode);
            graphNodeById[id] = graphNode;
        }
    }

    private void AddFallbackGraphLinks(
        IEnumerable<(string Source, string Target, int Count)> links,
        string sourceKind,
        string targetKind,
        IReadOnlyDictionary<string, TasteGraphNodeItem> graphNodeById)
    {
        var items = links
            .Where(x => graphNodeById.ContainsKey(BuildTasteGraphNodeId(sourceKind, x.Source))
                && graphNodeById.ContainsKey(BuildTasteGraphNodeId(targetKind, x.Target)))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Target, StringComparer.OrdinalIgnoreCase)
            .Take(TasteGraphLinkLimit)
            .ToList();
        var maxCount = items.Count == 0 ? 0 : items.Max(x => x.Count);

        foreach (var item in items)
        {
            var source = graphNodeById[BuildTasteGraphNodeId(sourceKind, item.Source)];
            var target = graphNodeById[BuildTasteGraphNodeId(targetKind, item.Target)];
            TasteGraphLinks.Add(new TasteGraphLinkItem(
                source.RightX,
                source.CenterY,
                target.LeftX,
                target.CenterY,
                CalculateTasteLineThickness(item.Count, maxCount),
                item.Count));
        }
    }

    private static string BuildTasteGraphNodeId(string kind, string label)
    {
        return $"{kind}:{label.Trim().ToLowerInvariant()}";
    }

    private static double CalculateTasteLineThickness(int count, int maxCount)
    {
        if (count <= 0 || maxCount <= 0)
        {
            return 1.5d;
        }

        return Math.Clamp(1.5d + count / (double)maxCount * 5d, 1.5d, 6.5d);
    }

    private void BuildTasteCombinationGraph(WatchStatisticsSnapshot snapshot)
    {
        TasteGraphNodes.Clear();
        TasteGraphLinks.Clear();
        TasteCombinationTop10.Clear();

        BuildTasteCanvasGraph(snapshot);

        var maxScore = snapshot.TasteCombinationTop10.Count == 0
            ? 0
            : snapshot.TasteCombinationTop10.Max(x => x.Score);
        var rank = 1;
        foreach (var item in snapshot.TasteCombinationTop10)
        {
            TasteCombinationTop10.Add(new TasteCombinationRankItem(
                rank++,
                $"{item.Type} x {item.Emotion} x {item.Scene}",
                $"{item.OccurrenceCount}次",
                CalculateProgress(item.Score, maxScore)));
        }
    }

    private void BuildWatchLikeComparison(WatchStatisticsSnapshot snapshot)
    {
        WatchLikeGroups.Clear();
        WatchLikeGroups.Add(new RankingGroup("经常观看", BuildRankingItems(snapshot.OftenWatchedTop3)));
        WatchLikeGroups.Add(new RankingGroup("经常喜爱", BuildRankingItems(snapshot.OftenLikedTop3)));
        WatchLikeGroups.Add(new RankingGroup("经常想看", BuildRankingItems(snapshot.OftenWantedTop3)));
    }

    private void OpenWatchHistoryByDate(object? parameter)
    {
        if (parameter is not CalendarDayCell day || day.IsPlaceholder)
        {
            return;
        }

        CalendarNoticeMessage = string.Empty;
        _navigationStateService.RequestNavigation(NavigationPageKey.WatchHistory, targetDate: day.Date);
    }

    private static List<RankedTagItem> BuildRankingItems(IReadOnlyList<WatchStatisticsTagItem> source)
    {
        var maxScore = source.Count == 0 ? 0 : source.Max(x => x.Score);
        return source
            .Select((item, index) => new RankedTagItem(
                index + 1,
                item.Label,
                item.Count > 0 ? $"{item.Count}次" : FormatSeconds(item.WatchSeconds),
                CalculateProgress(item.Score, maxScore)))
            .ToList();
    }

    private static string FormatDelta(WatchStatisticsTimeRange timeRange, int? delta)
    {
        if (timeRange == WatchStatisticsTimeRange.All)
        {
            return string.Empty;
        }

        if (!delta.HasValue)
        {
            return "暂无上月记录";
        }

        return delta.Value switch
        {
            > 0 => $"较上月 +{delta.Value}",
            < 0 => $"较上月 {delta.Value}",
            _ => "与上月持平"
        };
    }

    private static string FormatSeconds(double seconds)
    {
        if (seconds <= 0)
        {
            return "0分钟";
        }

        var totalMinutes = (int)Math.Round(seconds / 60d);
        if (totalMinutes < 60)
        {
            return $"{Math.Max(1, totalMinutes)}分钟";
        }

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return minutes == 0 ? $"{hours}小时" : $"{hours}小时{minutes}分钟";
    }

    private static string FormatPercent(double ratio)
    {
        return $"{Math.Round(ratio * 100d)}%";
    }

    private static double CalculateProgress(double value, double max)
    {
        if (value <= 0 || max <= 0)
        {
            return 0d;
        }

        return Math.Clamp(value / max * 100d, 3d, 100d);
    }

    private static double CalculateBubbleSize(int count, int maxCount)
    {
        if (count <= 0 || maxCount <= 0)
        {
            return 72d;
        }

        return Math.Clamp(76d + count / (double)maxCount * 72d, 82d, 148d);
    }

    private static double CalculateBubbleCoordinate(double centerCoordinate, double size, double canvasLength)
    {
        return Math.Clamp(centerCoordinate - size / 2d, 16d, Math.Max(16d, canvasLength - size - 16d));
    }

    private static string FormatDurationBucketName(string label)
    {
        return label switch
        {
            "Short" => "短片 <=60min",
            "Medium" => "中等 60-120min",
            "Long" => "长片 120-180min",
            "ExtraLong" => "超长 >180min",
            _ => label
        };
    }

    private void RaiseDisplayStateChanged()
    {
        OnPropertyChanged(nameof(HasAnyStatisticsData));
        OnPropertyChanged(nameof(HasWatchHistoryData));
        OnPropertyChanged(nameof(HasMonthlyFrequentTags));
        OnPropertyChanged(nameof(HasWarningMessages));
        OnPropertyChanged(nameof(HasPreferenceBubbles));
        OnPropertyChanged(nameof(HasMonthlyRankingData));
        OnPropertyChanged(nameof(HasRhythmData));
        OnPropertyChanged(nameof(HasTasteCombinationData));
        OnPropertyChanged(nameof(HasWatchLikeData));
        OnPropertyChanged(nameof(StatisticsRefreshStatusText));
        OnPropertyChanged(nameof(StatisticsModuleState));
        OnPropertyChanged(nameof(ShowStatisticsModuleState));
    }

    private void RaiseStatisticsRangeChanged()
    {
        OnPropertyChanged(nameof(StatisticsRangeText));
        OnPropertyChanged(nameof(IsMonthRangeSelected));
        OnPropertyChanged(nameof(IsAllRangeSelected));
        OnPropertyChanged(nameof(OverviewTitle));
        OnPropertyChanged(nameof(OverviewSubtitle));
        OnPropertyChanged(nameof(TotalWatchTimeTitle));
        OnPropertyChanged(nameof(WatchCountTitle));
        OnPropertyChanged(nameof(FrequentTagsTitle));
        OnPropertyChanged(nameof(PreferenceGraphTitle));
        OnPropertyChanged(nameof(TagRankingTitle));
        OnPropertyChanged(nameof(ViewingTimeTitle));
        OnPropertyChanged(nameof(DurationDistributionTitle));
        OnPropertyChanged(nameof(TasteCombinationTitle));
    }

    private void RaiseCalendarNavigationChanged()
    {
        OnPropertyChanged(nameof(CanGoPreviousCalendarMonth));
        OnPropertyChanged(nameof(CanGoNextCalendarMonth));
        OnPropertyChanged(nameof(ShowReturnToCurrentMonth));
        PreviousCalendarMonthCommand.RaiseCanExecuteChanged();
        NextCalendarMonthCommand.RaiseCanExecuteChanged();
        ReturnToCurrentCalendarMonthCommand.RaiseCanExecuteChanged();
    }

    private void RaiseProfileDisplayStateChanged()
    {
        OnPropertyChanged(nameof(Profile));
        OnPropertyChanged(nameof(HasProfile));
        OnPropertyChanged(nameof(ShowProfileEmptyState));
        OnPropertyChanged(nameof(IsProfileInsufficient));
        OnPropertyChanged(nameof(HasProfileError));
        OnPropertyChanged(nameof(ProfileStatusText));
        OnPropertyChanged(nameof(LastProfileRefreshedAtText));
        OnPropertyChanged(nameof(ProfileRefreshStatusText));
        OnPropertyChanged(nameof(ProfileSummaryText));
        OnPropertyChanged(nameof(ProfileHeroTitle));
        OnPropertyChanged(nameof(ProfileHeroSubtitle));
        OnPropertyChanged(nameof(ProfilePersonaType));
        OnPropertyChanged(nameof(ProfilePersonaTitle));
        OnPropertyChanged(nameof(ProfilePersonaDescription));
        OnPropertyChanged(nameof(PersonaPosterGender));
        OnPropertyChanged(nameof(PersonaPosterImageUri));
        OnPropertyChanged(nameof(PersonaPosterFrameUri));
        OnPropertyChanged(nameof(HasPersonaPoster));
        OnPropertyChanged(nameof(HasPersonaPosterFrame));
        OnPropertyChanged(nameof(ProfilePersonaConfidenceText));
        OnPropertyChanged(nameof(ProfilePersonaConfidenceValue));
        OnPropertyChanged(nameof(ProfileQuadrantName));
        OnPropertyChanged(nameof(ProfileQuadrantDescription));
        OnPropertyChanged(nameof(ProfileXAxisText));
        OnPropertyChanged(nameof(ProfileYAxisText));
        OnPropertyChanged(nameof(ProfileQuadrantPointX));
        OnPropertyChanged(nameof(ProfileQuadrantPointY));
        OnPropertyChanged(nameof(ProfileWatchVsLikeConclusion));
        OnPropertyChanged(nameof(NegativeSummaryText));
        OnPropertyChanged(nameof(HasProfileKeywords));
        OnPropertyChanged(nameof(HasProfileSummary));
        OnPropertyChanged(nameof(HasProfileDna));
        OnPropertyChanged(nameof(HasProfileWarnings));
        OnPropertyChanged(nameof(HasProfileCaveats));
        OnPropertyChanged(nameof(HasPreferredGenres));
        OnPropertyChanged(nameof(HasPreferredEmotions));
        OnPropertyChanged(nameof(HasPreferredScenes));
        OnPropertyChanged(nameof(HasPreferredCountries));
        OnPropertyChanged(nameof(HasPreferredLanguages));
        OnPropertyChanged(nameof(HasAvoidGenres));
        OnPropertyChanged(nameof(HasAvoidEmotions));
        OnPropertyChanged(nameof(HasAvoidScenes));
        OnPropertyChanged(nameof(HasAnyAvoidDirection));
        OnPropertyChanged(nameof(HasFuturePreference));
        OnPropertyChanged(nameof(HasProfileWatchLikeData));
        OnPropertyChanged(nameof(ProfileModuleState));
        OnPropertyChanged(nameof(ShowProfileModuleState));
    }

    private void SelectTab(object? parameter)
    {
        var nextIndex = parameter switch
        {
            int index => index,
            string value when int.TryParse(value, out var index) => index,
            _ => SelectedTabIndex
        };

        SelectedTabIndex = nextIndex;
    }

    private static string BuildSafeStatusMessage(string? message, string fallback)
    {
        var value = (message ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (ContainsSensitiveUiFragment(value))
        {
            return "错误详情包含受保护的路径、地址或密钥信息，已隐藏。";
        }

        return value.Length <= 140 ? value : $"{value[..140]}...";
    }

    private static bool ContainsSensitiveUiFragment(string value)
    {
        return value.Contains("://", StringComparison.Ordinal)
               || value.Contains(@":\", StringComparison.Ordinal)
               || value.Contains("api_key=", StringComparison.OrdinalIgnoreCase)
               || value.Contains("apikey=", StringComparison.OrdinalIgnoreCase)
               || value.Contains("token=", StringComparison.OrdinalIgnoreCase)
               || value.Contains("password=", StringComparison.OrdinalIgnoreCase)
               || value.Contains("secret=", StringComparison.OrdinalIgnoreCase)
               || value.Contains("bearer ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfigMissingMessage(string value)
    {
        return value.Contains("配置", StringComparison.OrdinalIgnoreCase)
               || value.Contains("未填写", StringComparison.OrdinalIgnoreCase)
               || value.Contains("未设置", StringComparison.OrdinalIgnoreCase)
               || value.Contains("not configured", StringComparison.OrdinalIgnoreCase)
               || value.Contains("api key", StringComparison.OrdinalIgnoreCase)
               || value.Contains("model", StringComparison.OrdinalIgnoreCase)
               || value.Contains("endpoint", StringComparison.OrdinalIgnoreCase);
    }

    private static void Log(string message)
    {
        Debug.WriteLine("[WATCH-INSIGHTS] " + message);
    }

    private static string FormatRefreshSource(StatisticsRefreshSource source)
    {
        return source switch
        {
            StatisticsRefreshSource.Manual => "manual",
            StatisticsRefreshSource.DataChanged => "data-changed",
            _ => "activate"
        };
    }

    private static string FormatStatisticsTimeRange(WatchStatisticsTimeRange timeRange)
    {
        return timeRange == WatchStatisticsTimeRange.All ? "all" : "month";
    }

    private static DateTime GetCurrentMonthStart()
    {
        var now = DateTime.Now;
        return new DateTime(now.Year, now.Month, 1);
    }

    private static string FormatProfileLoadSource(ProfileLoadSource source)
    {
        return source switch
        {
            ProfileLoadSource.Manual => "manual",
            ProfileLoadSource.Tab => "tab",
            _ => "activate"
        };
    }

    private enum StatisticsRefreshSource
    {
        Activate,
        Manual,
        DataChanged
    }

    private enum ProfileLoadSource
    {
        Activate,
        Tab,
        Manual
    }
}

public sealed record ProfilePlaceholderCard(string Title, string State, string Description);

public sealed record InsightModuleState(string Kind, string Title, string Message, bool IsVisible);

public sealed record WarningMessageItem(string Text);

public sealed record OverviewMetricCard(
    string Title,
    string Subtitle,
    string ValueText,
    string UnitText,
    string DeltaText,
    string IconText,
    string Kind)
{
    public bool HasDelta => !string.IsNullOrWhiteSpace(DeltaText);
}

public sealed record TagChipItem(string Label, string DetailText);

public sealed record CalendarDayCell(
    DateTime Date,
    string DayText,
    string WatchText,
    int WatchCount,
    int HeatLevel,
    bool HasValidWatch,
    bool IsPlaceholder)
{
    public bool IsClickable => !IsPlaceholder;

    public string WatchCountText => WatchCount > 0 ? $"{WatchCount}部" : string.Empty;

    public static CalendarDayCell Placeholder()
    {
        return new CalendarDayCell(default, string.Empty, string.Empty, 0, 0, false, true);
    }
}

public sealed record BubbleTagItem(string Label, string Kind, int Count, double Size, double X, double Y)
{
    public string CountText => $"{Count}次";

    public string KindIcon => Kind switch
    {
        "类型" => "类",
        "情绪" => "情",
        _ => "签"
    };

    public double LabelMaxWidth => Math.Max(44d, Size - 18d);
}

public sealed record RankingGroup(string Title, IReadOnlyList<RankedTagItem> Items)
{
    public bool HasItems => Items.Count > 0;
}

public sealed record RankedTagItem(int Rank, string Label, string DetailText, double ProgressValue);

public sealed record ProfileDnaItem(
    string Gene,
    string IconText,
    string Subtitle,
    IReadOnlyList<TagChipItem> Tags,
    int Score,
    string Description,
    bool IsProgressGene,
    string LeftLabel,
    string RightLabel)
{
    public bool HasTags => Tags.Count > 0;
}

public sealed record ProfileListGroup(string Title, string Subtitle, IReadOnlyList<ProfileListItem> Items)
{
    public bool HasItems => Items.Count > 0;
}

public sealed record ProfileListItem(int Rank, string Label, double ProgressValue);

public sealed record TimeBucketItem(string Label, string WatchText, int WatchCount, double ProgressValue)
{
    public string CountText => $"{WatchCount}次";

    public double BarHeight => Math.Clamp(ProgressValue / 100d * 164d, ProgressValue > 0 ? 12d : 4d, 164d);
}

public sealed record WeekPartItem(
    string Title,
    string TotalText,
    string AverageText,
    string RatioText,
    double ProgressValue);

public sealed record DurationBucketItem(
    string Label,
    string CountText,
    string PercentText,
    double ProgressValue);

public sealed record TasteGraphNodeItem(
    string Id,
    string Label,
    string Kind,
    double X,
    double Y,
    double Width,
    double Height,
    int Count)
{
    public double LeftX => X;

    public double RightX => X + Width;

    public double CenterY => Y + Height / 2d;

    public string CountText => $"{Count}次";

    public string KindText => Kind switch
    {
        "type" => "类型",
        "emotion" => "情绪",
        "scene" => "场景",
        _ => "标签"
    };

    public string KindIcon => Kind switch
    {
        "type" => "类",
        "emotion" => "情",
        "scene" => "景",
        _ => "签"
    };
}

public sealed record TasteGraphLinkItem(
    double X1,
    double Y1,
    double X2,
    double Y2,
    double StrokeThickness,
    int Count);

public sealed record TasteCombinationRankItem(
    int Rank,
    string Label,
    string CountText,
    double ProgressValue)
{
    public string TypeLabel => SplitCombinationLabel(0);

    public string EmotionLabel => SplitCombinationLabel(1);

    public string SceneLabel => SplitCombinationLabel(2);

    public string TagToolTipText => BuildGroupedTagToolTipText();

    public string OccurrenceText => $"出现 {ExtractLeadingNumber(CountText)} 次";

    private string BuildGroupedTagToolTipText()
    {
        var lines = new[]
        {
            FormatToolTipLine("类型", TypeLabel),
            FormatToolTipLine("情绪", EmotionLabel),
            FormatToolTipLine("场景", SceneLabel)
        };

        var tooltip = string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        return string.IsNullOrWhiteSpace(tooltip) ? Label : tooltip;
    }

    private static string FormatToolTipLine(string label, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label}: {value.Trim()}";
    }

    private string SplitCombinationLabel(int index)
    {
        var parts = Label.Split(" x ", StringSplitOptions.TrimEntries);
        return index < parts.Length ? parts[index] : string.Empty;
    }

    private static int ExtractLeadingNumber(string text)
    {
        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }
}
