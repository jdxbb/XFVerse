using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.Models.Caches;
using MediaLibrary.App.Models.Settings;
using MediaLibrary.App.Services;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class SettingsViewModel : PageViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IWebDavService _webDavService;
    private readonly IThemeService _themeService;
    private readonly IAppBehaviorPreferencesService _appBehaviorPreferencesService;
    private readonly ISoftwareCacheManagementService _softwareCacheManagementService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly IOpenSubtitlesClientService _openSubtitlesClientService;
    private readonly HttpClient _metadataApiHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };
    private readonly HttpClient _aiApiHttpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
    private const long BytesPerMb = 1024L * 1024L;
    private const int AiProbeTimeoutSeconds = 20;
    private const int GeneralSettingsTabIndex = 0;
    private const int ApiSettingsTabIndex = 1;
    private const string ThemeSystem = "System";
    private const string ThemeLight = "Light";
    private const string ThemeDark = "Dark";
    private const string CloseWindowBehaviorExit = "exit";
    private const string CloseWindowBehaviorTray = "tray";
    private const string ApiConfigStatusSuccess = "success";
    private const string ApiConfigStatusUntested = "untested";
    private const string ApiConfigStatusFailure = "failure";
    private const string OfficialWebsiteUrl = "https://xfverse.fun";
    private int? _connectionId;
    private int? _applicationSettingId;
    private string _connectionName = string.Empty;
    private string _baseUrl = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isConnectionEnabled = true;
    private string _tmdbReadAccessToken = string.Empty;
    private string _tmdbApiKey = string.Empty;
    private string _omdbApiKey = string.Empty;
    private string _loadedTmdbReadAccessToken = string.Empty;
    private string _loadedTmdbApiKey = string.Empty;
    private string _loadedOmdbApiKey = string.Empty;
    private string _openSubtitlesEndpoint = "https://api.opensubtitles.com/api/v1";
    private string _openSubtitlesApiKey = string.Empty;
    private string _openSubtitlesUsername = string.Empty;
    private string _openSubtitlesPassword = string.Empty;
    private string _openSubtitlesToken = string.Empty;
    private string _loadedOpenSubtitlesEndpoint = "https://api.opensubtitles.com/api/v1";
    private string _loadedOpenSubtitlesApiKey = string.Empty;
    private string _loadedOpenSubtitlesUsername = string.Empty;
    private string _loadedOpenSubtitlesPassword = string.Empty;
    private string _loadedOpenSubtitlesLanguageCode = "zh-cn";
    private string _tmdbConfigStatusKind = ApiConfigStatusUntested;
    private string _omdbConfigStatusKind = ApiConfigStatusUntested;
    private string _openSubtitlesConfigStatusKind = ApiConfigStatusUntested;
    private string _aiConfigStatusKind = ApiConfigStatusUntested;
    private bool _isOpenSubtitlesEnabled = true;
    private OpenSubtitlesLanguageOption? _selectedOpenSubtitlesLanguage;
    private string _aiBaseUrl = string.Empty;
    private string _aiApiKey = string.Empty;
    private string _aiModel = string.Empty;
    private string _loadedAiBaseUrl = string.Empty;
    private string _loadedAiApiKey = string.Empty;
    private string _loadedAiModel = string.Empty;
    private string _aiDetailCorrectionModel = "deepseek-v4-pro";
    private string _aiDetailCorrectionTimeoutSeconds = "90";
    private string _aiBatchCorrectionModel = "deepseek-v4-pro";
    private string _aiBatchCorrectionTimeoutSeconds = "75";
    private string _aiScanTvUncertainRangeModel = "deepseek-v4-flash";
    private string _aiScanTvUncertainRangeTimeoutSeconds = "300";
    private string _aiScanTvFullRangeModel = "deepseek-v4-flash";
    private string _aiScanTvFullRangeTimeoutSeconds = "18";
    private string _aiScanMovieTaggingModel = "deepseek-v4-flash";
    private string _aiScanMovieTaggingTimeoutSeconds = "45";
    private string _aiRecommendationModel = "deepseek-v4-flash";
    private string _aiRecommendationTimeoutSeconds = "90";
    private string _aiWatchProfileModel = "deepseek-v4-pro";
    private string _aiWatchProfileTimeoutSeconds = "180";
    private string _loadedAiDetailCorrectionModel = "deepseek-v4-pro";
    private string _loadedAiDetailCorrectionTimeoutSeconds = "90";
    private string _loadedAiBatchCorrectionModel = "deepseek-v4-pro";
    private string _loadedAiBatchCorrectionTimeoutSeconds = "75";
    private string _loadedAiScanTvUncertainRangeModel = "deepseek-v4-flash";
    private string _loadedAiScanTvUncertainRangeTimeoutSeconds = "300";
    private string _loadedAiScanTvFullRangeModel = "deepseek-v4-flash";
    private string _loadedAiScanTvFullRangeTimeoutSeconds = "18";
    private string _loadedAiScanMovieTaggingModel = "deepseek-v4-flash";
    private string _loadedAiScanMovieTaggingTimeoutSeconds = "45";
    private string _loadedAiRecommendationModel = "deepseek-v4-flash";
    private string _loadedAiRecommendationTimeoutSeconds = "90";
    private string _loadedAiWatchProfileModel = "deepseek-v4-pro";
    private string _loadedAiWatchProfileTimeoutSeconds = "180";
    private string _selectedThemeMode = ThemeLight;
    private string _connectionStatusMessage = "请先保存 WebDAV 连接配置。";
    private string _scanPathStatusMessage = "当前还没有扫描路径。";
    private string _tmdbStatusMessage = "可在这里保存 TMDB 认证信息。";
    private string _omdbStatusMessage = "可在这里保存 OMDb 认证信息。";
    private string _openSubtitlesStatusMessage = "可在这里保存 OpenSubtitles 在线字幕配置。";
    private string _apiStatusMessage = "可在这里保存大模型配置。";
    private string _themeStatusMessage = "默认使用浅色主题。";
    private string _softwareCacheStatusMessage = "软件缓存状态尚未加载。";
    private string _posterCacheUsageText = "海报缓存占用尚未加载。";
    private string _posterCacheFileCountText = string.Empty;
    private string _posterCacheMaxMbText = "512";
    private string _posterCacheStatusMessage = "海报缓存状态尚未加载。";
    private string _otherCacheUsageText = "其他缓存状态尚未加载。";
    private string _otherCacheDescriptionText = "仅包含可再生成的 TMDB / OMDb 外部元数据缓存。";
    private string _otherCacheStatusMessage = "其他缓存状态尚未加载。";
    private bool _isOtherCacheClearAvailable;
    private string _subtitleCacheUsageText = "在线字幕缓存状态尚未加载。";
    private string _subtitleCacheDetailText = "删除播放器里的在线字幕绑定不会物理删除缓存文件。";
    private string _subtitleCacheStatusMessage = "在线字幕缓存状态尚未加载。";
    private bool _isPosterCacheClearAvailable;
    private bool _isSubtitleCacheClearAvailable;
    private string _selectedCloseWindowBehavior = CloseWindowBehaviorExit;
    private bool _startPlayerFullscreenOnPlay = true;
    private bool _autoScanWebDavOnStartup;
    private string _themeToggleIcon = "sun";
    private string _themeToggleToolTip = "当前浅色主题，切换到深色主题";
    private string _aboutStatusMessage = "XFVerse 影音管理系统";
    private int? _editingScanPathId;
    private string _editingScanPathValue = string.Empty;
    private string _editingScanPathDisplayName = string.Empty;
    private bool _editingScanPathEnabled = true;
    private bool _editingScanPathRecursive = true;
    private int _selectedSettingsTabIndex = GeneralSettingsTabIndex;
    private bool _isGeneralSettingsContentReady;

    public SettingsViewModel(
        ISettingsService settingsService,
        IWebDavService webDavService,
        IThemeService themeService,
        IAppBehaviorPreferencesService appBehaviorPreferencesService,
        ISoftwareCacheManagementService softwareCacheManagementService,
        IConfirmationDialogService confirmationDialogService,
        IOpenSubtitlesClientService openSubtitlesClientService)
        : base("设置", "管理通用设置与 API 配置。")
    {
        _settingsService = settingsService;
        _webDavService = webDavService;
        _themeService = themeService;
        _appBehaviorPreferencesService = appBehaviorPreferencesService;
        _softwareCacheManagementService = softwareCacheManagementService;
        _confirmationDialogService = confirmationDialogService;
        _openSubtitlesClientService = openSubtitlesClientService;
        _themeService.ThemeChanged += OnThemeChanged;

        ThemeModes = _themeService.ThemeModes;
        OpenSubtitlesLanguages = _openSubtitlesClientService.SupportedLanguages
            .Select(x => new OpenSubtitlesLanguageOption(x.Code, LocalizeOpenSubtitlesLanguageName(x)))
            .OrderBy(x => GetOpenSubtitlesLanguagePriority(x.Code))
            .ThenBy(x => x.Name, StringComparer.CurrentCulture)
            .ToList();
        _selectedOpenSubtitlesLanguage = OpenSubtitlesLanguages.FirstOrDefault(x => x.Code == "zh-cn")
                                         ?? OpenSubtitlesLanguages.FirstOrDefault();
        SaveConnectionCommand = new AsyncRelayCommand(SaveConnectionAsync);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        SaveTmdbSettingsCommand = new AsyncRelayCommand(SaveTmdbSettingsAsync, HasTmdbSettingsChanges);
        TestTmdbConnectionCommand = new AsyncRelayCommand(TestTmdbConnectionAsync);
        SaveOmdbSettingsCommand = new AsyncRelayCommand(SaveOmdbSettingsAsync, HasOmdbSettingsChanges);
        TestOmdbConnectionCommand = new AsyncRelayCommand(TestOmdbConnectionAsync);
        SaveOpenSubtitlesSettingsCommand = new AsyncRelayCommand(SaveOpenSubtitlesSettingsAsync, HasOpenSubtitlesSettingsChanges);
        TestOpenSubtitlesConnectionCommand = new AsyncRelayCommand(TestOpenSubtitlesConnectionAsync);
        SaveAiSettingsCommand = new AsyncRelayCommand(SaveAiSettingsAsync, HasAiSettingsChanges);
        TestAiSettingsCommand = new AsyncRelayCommand(TestAiSettingsAsync);
        SaveThemeSettingsCommand = new AsyncRelayCommand(SaveThemeSettingsAsync);
        ToggleThemeSettingsCommand = new AsyncRelayCommand(ToggleThemeSettingsAsync);
        BeginAddScanPathCommand = new RelayCommand(BeginAddScanPath);
        SaveScanPathCommand = new AsyncRelayCommand(SaveScanPathAsync);
        EditScanPathCommand = new RelayCommand(EditScanPath);
        DeleteScanPathCommand = new AsyncRelayCommand(DeleteScanPathAsync);
        ToggleScanPathCommand = new AsyncRelayCommand(ToggleScanPathAsync);
        CancelEditScanPathCommand = new RelayCommand(CancelEditScanPath);
        SavePosterCacheLimitCommand = new AsyncRelayCommand(SavePosterCacheLimitAsync);
        ClearPosterCacheCommand = new AsyncRelayCommand(ClearPosterCacheAsync, () => IsPosterCacheClearAvailable);
        ClearOtherCacheCommand = new AsyncRelayCommand(ClearOtherCacheAsync, () => IsOtherCacheClearAvailable);
        ClearSubtitleCacheCommand = new AsyncRelayCommand(ClearSubtitleCacheAsync, () => IsSubtitleCacheClearAvailable);
        RefreshSoftwareCacheCommand = new AsyncRelayCommand(RefreshSoftwareCacheAsync);
        ToggleAboutDetailsCommand = new RelayCommand(ToggleAboutDetails);
        OpenOfficialWebsiteCommand = new RelayCommand(OpenOfficialWebsite);
        SelectSettingsTabCommand = new RelayCommand(SelectSettingsTab);
        SelectThemeModeCommand = new AsyncRelayCommand(SelectThemeModeAsync);
        SelectCloseWindowBehaviorCommand = new AsyncRelayCommand(SelectCloseWindowBehaviorAsync);
        SetPlayerFullscreenOnPlayCommand = new AsyncRelayCommand(SetPlayerFullscreenOnPlayAsync);
        SetAutoWebDavScanCommand = new AsyncRelayCommand(SetAutoWebDavScanAsync);
    }

    public ObservableCollection<ScanPath> ScanPaths { get; } = [];

    public IReadOnlyList<string> ThemeModes { get; }

    public IReadOnlyList<OpenSubtitlesLanguageOption> OpenSubtitlesLanguages { get; }

    public AsyncRelayCommand SaveConnectionCommand { get; }

    public AsyncRelayCommand TestConnectionCommand { get; }

    public AsyncRelayCommand SaveTmdbSettingsCommand { get; }

    public AsyncRelayCommand TestTmdbConnectionCommand { get; }

    public AsyncRelayCommand SaveOmdbSettingsCommand { get; }

    public AsyncRelayCommand TestOmdbConnectionCommand { get; }

    public AsyncRelayCommand SaveOpenSubtitlesSettingsCommand { get; }

    public AsyncRelayCommand TestOpenSubtitlesConnectionCommand { get; }

    public AsyncRelayCommand SaveAiSettingsCommand { get; }

    public AsyncRelayCommand TestAiSettingsCommand { get; }

    public AsyncRelayCommand SaveThemeSettingsCommand { get; }

    public AsyncRelayCommand ToggleThemeSettingsCommand { get; }

    public RelayCommand BeginAddScanPathCommand { get; }

    public AsyncRelayCommand SaveScanPathCommand { get; }

    public RelayCommand EditScanPathCommand { get; }

    public AsyncRelayCommand DeleteScanPathCommand { get; }

    public AsyncRelayCommand ToggleScanPathCommand { get; }

    public RelayCommand CancelEditScanPathCommand { get; }

    public AsyncRelayCommand SavePosterCacheLimitCommand { get; }

    public AsyncRelayCommand ClearPosterCacheCommand { get; }

    public AsyncRelayCommand ClearOtherCacheCommand { get; }

    public AsyncRelayCommand ClearSubtitleCacheCommand { get; }

    public AsyncRelayCommand RefreshSoftwareCacheCommand { get; }

    public RelayCommand ToggleAboutDetailsCommand { get; }

    public RelayCommand OpenOfficialWebsiteCommand { get; }

    public RelayCommand SelectSettingsTabCommand { get; }

    public AsyncRelayCommand SelectThemeModeCommand { get; }

    public AsyncRelayCommand SelectCloseWindowBehaviorCommand { get; }

    public AsyncRelayCommand SetPlayerFullscreenOnPlayCommand { get; }

    public AsyncRelayCommand SetAutoWebDavScanCommand { get; }

    public int SelectedSettingsTabIndex
    {
        get => _selectedSettingsTabIndex;
        set
        {
            if (!SetProperty(ref _selectedSettingsTabIndex, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsGeneralSettingsTabSelected));
            OnPropertyChanged(nameof(IsApiSettingsTabSelected));
        }
    }

    public bool IsGeneralSettingsTabSelected => SelectedSettingsTabIndex == GeneralSettingsTabIndex;

    public bool IsApiSettingsTabSelected => SelectedSettingsTabIndex == ApiSettingsTabIndex;

    public bool IsGeneralSettingsContentReady
    {
        get => _isGeneralSettingsContentReady;
        private set => SetProperty(ref _isGeneralSettingsContentReady, value);
    }

    public int? ConnectionId
    {
        get => _connectionId;
        private set
        {
            if (SetProperty(ref _connectionId, value))
            {
                OnPropertyChanged(nameof(HasSavedConnection));
            }
        }
    }

    public string ConnectionName { get => _connectionName; set => SetProperty(ref _connectionName, value); }

    public string BaseUrl { get => _baseUrl; set => SetProperty(ref _baseUrl, value); }

    public string Username { get => _username; set => SetProperty(ref _username, value); }

    public string Password { get => _password; set => SetProperty(ref _password, value); }

    public bool IsConnectionEnabled { get => _isConnectionEnabled; set => SetProperty(ref _isConnectionEnabled, value); }

    public string TmdbReadAccessToken
    {
        get => _tmdbReadAccessToken;
        set
        {
            if (SetProperty(ref _tmdbReadAccessToken, value))
            {
                OnTmdbInputsChanged();
            }
        }
    }

    public string TmdbApiKey
    {
        get => _tmdbApiKey;
        set
        {
            if (SetProperty(ref _tmdbApiKey, value))
            {
                OnTmdbInputsChanged();
            }
        }
    }

    public string OmdbApiKey
    {
        get => _omdbApiKey;
        set
        {
            if (SetProperty(ref _omdbApiKey, value))
            {
                OnOmdbInputsChanged();
            }
        }
    }

    public string OpenSubtitlesEndpoint
    {
        get => _openSubtitlesEndpoint;
        set
        {
            if (SetProperty(ref _openSubtitlesEndpoint, value))
            {
                OnOpenSubtitlesInputsChanged();
            }
        }
    }

    public string OpenSubtitlesApiKey
    {
        get => _openSubtitlesApiKey;
        set
        {
            if (SetProperty(ref _openSubtitlesApiKey, value))
            {
                OnOpenSubtitlesInputsChanged();
            }
        }
    }

    public string OpenSubtitlesUsername
    {
        get => _openSubtitlesUsername;
        set
        {
            if (SetProperty(ref _openSubtitlesUsername, value))
            {
                OnOpenSubtitlesInputsChanged();
            }
        }
    }

    public string OpenSubtitlesPassword
    {
        get => _openSubtitlesPassword;
        set
        {
            if (SetProperty(ref _openSubtitlesPassword, value))
            {
                OnOpenSubtitlesInputsChanged();
            }
        }
    }

    public bool IsOpenSubtitlesEnabled
    {
        get => true;
        set
        {
            if (SetProperty(ref _isOpenSubtitlesEnabled, true))
            {
                OnPropertyChanged(nameof(OpenSubtitlesConfigStatusText));
                OnOpenSubtitlesInputsChanged();
            }
        }
    }

    public OpenSubtitlesLanguageOption? SelectedOpenSubtitlesLanguage
    {
        get => _selectedOpenSubtitlesLanguage;
        set
        {
            if (SetProperty(ref _selectedOpenSubtitlesLanguage, value))
            {
                OnOpenSubtitlesInputsChanged();
            }
        }
    }

    public string AiBaseUrl
    {
        get => _aiBaseUrl;
        set
        {
            if (SetProperty(ref _aiBaseUrl, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiApiKey
    {
        get => _aiApiKey;
        set
        {
            if (SetProperty(ref _aiApiKey, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiModel
    {
        get => _aiModel;
        set
        {
            if (SetProperty(ref _aiModel, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiDetailCorrectionModel
    {
        get => _aiDetailCorrectionModel;
        set
        {
            if (SetProperty(ref _aiDetailCorrectionModel, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiDetailCorrectionTimeoutSeconds
    {
        get => _aiDetailCorrectionTimeoutSeconds;
        set
        {
            if (SetProperty(ref _aiDetailCorrectionTimeoutSeconds, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiBatchCorrectionModel
    {
        get => _aiBatchCorrectionModel;
        set
        {
            if (SetProperty(ref _aiBatchCorrectionModel, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiBatchCorrectionTimeoutSeconds
    {
        get => _aiBatchCorrectionTimeoutSeconds;
        set
        {
            if (SetProperty(ref _aiBatchCorrectionTimeoutSeconds, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiScanTvUncertainRangeModel
    {
        get => _aiScanTvUncertainRangeModel;
        set
        {
            if (SetProperty(ref _aiScanTvUncertainRangeModel, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiScanTvUncertainRangeTimeoutSeconds
    {
        get => _aiScanTvUncertainRangeTimeoutSeconds;
        set
        {
            if (SetProperty(ref _aiScanTvUncertainRangeTimeoutSeconds, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiScanTvFullRangeModel
    {
        get => _aiScanTvFullRangeModel;
        set
        {
            if (SetProperty(ref _aiScanTvFullRangeModel, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiScanTvFullRangeTimeoutSeconds
    {
        get => _aiScanTvFullRangeTimeoutSeconds;
        set
        {
            if (SetProperty(ref _aiScanTvFullRangeTimeoutSeconds, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiScanMovieTaggingModel
    {
        get => _aiScanMovieTaggingModel;
        set
        {
            if (SetProperty(ref _aiScanMovieTaggingModel, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiScanMovieTaggingTimeoutSeconds
    {
        get => _aiScanMovieTaggingTimeoutSeconds;
        set
        {
            if (SetProperty(ref _aiScanMovieTaggingTimeoutSeconds, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiRecommendationModel
    {
        get => _aiRecommendationModel;
        set
        {
            if (SetProperty(ref _aiRecommendationModel, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiRecommendationTimeoutSeconds
    {
        get => _aiRecommendationTimeoutSeconds;
        set
        {
            if (SetProperty(ref _aiRecommendationTimeoutSeconds, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiWatchProfileModel
    {
        get => _aiWatchProfileModel;
        set
        {
            if (SetProperty(ref _aiWatchProfileModel, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string AiWatchProfileTimeoutSeconds
    {
        get => _aiWatchProfileTimeoutSeconds;
        set
        {
            if (SetProperty(ref _aiWatchProfileTimeoutSeconds, value))
            {
                OnAiInputsChanged();
            }
        }
    }

    public string SelectedThemeMode
    {
        get => _selectedThemeMode;
        set
        {
            if (!SetProperty(ref _selectedThemeMode, NormalizeThemeMode(value)))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSystemThemeSelected));
            OnPropertyChanged(nameof(IsLightThemeSelected));
            OnPropertyChanged(nameof(IsDarkThemeSelected));
        }
    }

    public bool IsSystemThemeSelected => IsThemeModeSelected(ThemeSystem);

    public bool IsLightThemeSelected => IsThemeModeSelected(ThemeLight);

    public bool IsDarkThemeSelected => IsThemeModeSelected(ThemeDark);

    public string ThemeToggleIcon
    {
        get => _themeToggleIcon;
        private set => SetProperty(ref _themeToggleIcon, value);
    }

    public string ThemeToggleToolTip
    {
        get => _themeToggleToolTip;
        private set => SetProperty(ref _themeToggleToolTip, value);
    }

    public string SelectedCloseWindowBehavior
    {
        get => _selectedCloseWindowBehavior;
        private set
        {
            if (!SetProperty(ref _selectedCloseWindowBehavior, NormalizeCloseWindowBehavior(value)))
            {
                return;
            }

            OnPropertyChanged(nameof(IsCloseWindowExitSelected));
            OnPropertyChanged(nameof(IsCloseWindowTraySelected));
            OnPropertyChanged(nameof(CloseWindowBehaviorText));
            OnPropertyChanged(nameof(CloseWindowBehaviorDetailText));
        }
    }

    public bool IsCloseWindowExitSelected => string.Equals(
        SelectedCloseWindowBehavior,
        CloseWindowBehaviorExit,
        StringComparison.OrdinalIgnoreCase);

    public bool IsCloseWindowTraySelected => string.Equals(
        SelectedCloseWindowBehavior,
        CloseWindowBehaviorTray,
        StringComparison.OrdinalIgnoreCase);

    public string CloseWindowBehaviorText => IsCloseWindowTraySelected ? "缩小到托盘" : "退出软件";

    public string CloseWindowBehaviorDetailText => IsCloseWindowTraySelected
        ? "关闭主窗口时隐藏到系统托盘，可从托盘恢复或退出。"
        : "关闭主窗口时直接退出 XFVerse。";

    public bool StartPlayerFullscreenOnPlay
    {
        get => _startPlayerFullscreenOnPlay;
        private set
        {
            if (!SetProperty(ref _startPlayerFullscreenOnPlay, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsPlayerFullscreenOnPlayEnabled));
            OnPropertyChanged(nameof(IsPlayerFullscreenOnPlayDisabled));
            OnPropertyChanged(nameof(AutoFullscreenDetailText));
        }
    }

    public bool IsPlayerFullscreenOnPlayEnabled => StartPlayerFullscreenOnPlay;

    public bool IsPlayerFullscreenOnPlayDisabled => !StartPlayerFullscreenOnPlay;

    public string AutoFullscreenDetailText => StartPlayerFullscreenOnPlay
        ? "点击播放后播放器窗口默认进入全屏。"
        : "点击播放后播放器以普通窗口打开。";

    public bool AutoScanWebDavOnStartup
    {
        get => _autoScanWebDavOnStartup;
        private set
        {
            if (!SetProperty(ref _autoScanWebDavOnStartup, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsAutoWebDavScanEnabled));
            OnPropertyChanged(nameof(IsAutoWebDavScanDisabled));
            OnPropertyChanged(nameof(AutoWebDavScanDetailText));
        }
    }

    public bool IsAutoWebDavScanEnabled => AutoScanWebDavOnStartup;

    public bool IsAutoWebDavScanDisabled => !IsAutoWebDavScanEnabled;

    public string AutoWebDavScanDetailText => AutoScanWebDavOnStartup
        ? "下次启动时自动执行 WebDAV 扫描。"
        : "启动时不自动扫描 WebDAV。";

    public string TmdbConfigStatusText =>
        string.IsNullOrWhiteSpace(TmdbReadAccessToken) && string.IsNullOrWhiteSpace(TmdbApiKey)
            ? "未配置"
            : "已配置";

    public string OmdbConfigStatusText => string.IsNullOrWhiteSpace(OmdbApiKey) ? "未配置" : "已配置";

    public string OpenSubtitlesConfigStatusText
    {
        get
        {
            return string.IsNullOrWhiteSpace(OpenSubtitlesApiKey) ? "缺少 API Key" : "已配置";
        }
    }

    public string AiConfigStatusText =>
        string.IsNullOrWhiteSpace(AiBaseUrl)
        || string.IsNullOrWhiteSpace(AiApiKey)
        || string.IsNullOrWhiteSpace(AiModel)
            ? "未配置"
            : "已配置";

    public string TmdbConfigStatusKind
    {
        get => _tmdbConfigStatusKind;
        private set => SetProperty(ref _tmdbConfigStatusKind, value);
    }

    public string OmdbConfigStatusKind
    {
        get => _omdbConfigStatusKind;
        private set => SetProperty(ref _omdbConfigStatusKind, value);
    }

    public string OpenSubtitlesConfigStatusKind
    {
        get => _openSubtitlesConfigStatusKind;
        private set => SetProperty(ref _openSubtitlesConfigStatusKind, value);
    }

    public string AiConfigStatusKind
    {
        get => _aiConfigStatusKind;
        private set => SetProperty(ref _aiConfigStatusKind, value);
    }

    public string ConnectionStatusMessage { get => _connectionStatusMessage; set => SetProperty(ref _connectionStatusMessage, value); }

    public string ScanPathStatusMessage { get => _scanPathStatusMessage; set => SetProperty(ref _scanPathStatusMessage, value); }

    public string TmdbStatusMessage { get => _tmdbStatusMessage; set => SetProperty(ref _tmdbStatusMessage, value); }

    public string OmdbStatusMessage { get => _omdbStatusMessage; set => SetProperty(ref _omdbStatusMessage, value); }

    public string OpenSubtitlesStatusMessage { get => _openSubtitlesStatusMessage; set => SetProperty(ref _openSubtitlesStatusMessage, value); }

    public string ApiStatusMessage { get => _apiStatusMessage; set => SetProperty(ref _apiStatusMessage, value); }

    public string ThemeStatusMessage { get => _themeStatusMessage; set => SetProperty(ref _themeStatusMessage, value); }

    public string SoftwareCacheStatusMessage { get => _softwareCacheStatusMessage; set => SetProperty(ref _softwareCacheStatusMessage, value); }

    public string PosterCacheUsageText { get => _posterCacheUsageText; set => SetProperty(ref _posterCacheUsageText, value); }

    public string PosterCacheFileCountText { get => _posterCacheFileCountText; set => SetProperty(ref _posterCacheFileCountText, value); }

    public string PosterCacheMaxMbText { get => _posterCacheMaxMbText; set => SetProperty(ref _posterCacheMaxMbText, value); }

    public string PosterCacheStatusMessage { get => _posterCacheStatusMessage; set => SetProperty(ref _posterCacheStatusMessage, value); }

    public bool IsPosterCacheClearAvailable
    {
        get => _isPosterCacheClearAvailable;
        set
        {
            if (SetProperty(ref _isPosterCacheClearAvailable, value))
            {
                ClearPosterCacheCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string OtherCacheUsageText { get => _otherCacheUsageText; set => SetProperty(ref _otherCacheUsageText, value); }

    public string OtherCacheDescriptionText { get => _otherCacheDescriptionText; set => SetProperty(ref _otherCacheDescriptionText, value); }

    public string OtherCacheStatusMessage { get => _otherCacheStatusMessage; set => SetProperty(ref _otherCacheStatusMessage, value); }

    public bool IsOtherCacheClearAvailable
    {
        get => _isOtherCacheClearAvailable;
        set
        {
            if (SetProperty(ref _isOtherCacheClearAvailable, value))
            {
                ClearOtherCacheCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SubtitleCacheUsageText { get => _subtitleCacheUsageText; set => SetProperty(ref _subtitleCacheUsageText, value); }

    public string SubtitleCacheDetailText { get => _subtitleCacheDetailText; set => SetProperty(ref _subtitleCacheDetailText, value); }

    public string SubtitleCacheStatusMessage { get => _subtitleCacheStatusMessage; set => SetProperty(ref _subtitleCacheStatusMessage, value); }

    public bool IsSubtitleCacheClearAvailable
    {
        get => _isSubtitleCacheClearAvailable;
        set
        {
            if (SetProperty(ref _isSubtitleCacheClearAvailable, value))
            {
                ClearSubtitleCacheCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AboutStatusMessage { get => _aboutStatusMessage; set => SetProperty(ref _aboutStatusMessage, value); }

    public string AppVersionText => $"版本 {GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}";

    public bool HasSavedConnection => ConnectionId.HasValue;

    public int? EditingScanPathId
    {
        get => _editingScanPathId;
        private set
        {
            if (SetProperty(ref _editingScanPathId, value))
            {
                OnPropertyChanged(nameof(IsEditingExistingScanPath));
                OnPropertyChanged(nameof(ScanPathEditorTitle));
                OnPropertyChanged(nameof(ScanPathSubmitButtonText));
            }
        }
    }

    public string EditingScanPathValue { get => _editingScanPathValue; set => SetProperty(ref _editingScanPathValue, value); }

    public string EditingScanPathDisplayName { get => _editingScanPathDisplayName; set => SetProperty(ref _editingScanPathDisplayName, value); }

    public bool EditingScanPathEnabled { get => _editingScanPathEnabled; set => SetProperty(ref _editingScanPathEnabled, value); }

    public bool EditingScanPathRecursive { get => _editingScanPathRecursive; set => SetProperty(ref _editingScanPathRecursive, value); }

    public bool IsEditingExistingScanPath => EditingScanPathId.HasValue;

    public string ScanPathEditorTitle => IsEditingExistingScanPath ? "编辑扫描路径" : "新增扫描路径";

    public string ScanPathSubmitButtonText => IsEditingExistingScanPath ? "保存修改" : "新增路径";

    public override Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var appSettingTask = _settingsService.GetApplicationSettingAsync(cancellationToken);
            var behaviorPreferencesTask = _appBehaviorPreferencesService.LoadAsync(cancellationToken);

            await Task.WhenAll(appSettingTask, behaviorPreferencesTask);
            ApplyApplicationSetting(appSettingTask.Result);
            ApplyBehaviorPreferences(behaviorPreferencesTask.Result);

            await LoadSoftwareCacheAsync(cancellationToken);
            IsGeneralSettingsContentReady = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ConnectionStatusMessage = $"加载设置失败：{exception.Message}";
            TmdbStatusMessage = "TMDB 配置尚未加载。";
            OmdbStatusMessage = "OMDb 配置尚未加载。";
            OpenSubtitlesStatusMessage = "OpenSubtitles 配置尚未加载。";
            ApiStatusMessage = "大模型配置尚未加载。";
            SoftwareCacheStatusMessage = "软件缓存状态尚未加载。";
            IsGeneralSettingsContentReady = true;
        }
    }

    private void ApplyConnection(WebDavConnectionModel connection)
    {
        ConnectionId = connection.Id;
        ConnectionName = connection.Name;
        BaseUrl = connection.BaseUrl;
        Username = connection.Username;
        Password = connection.Password;
        IsConnectionEnabled = connection.IsEnabled;
        ConnectionStatusMessage = ConnectionId.HasValue
            ? "已加载当前 WebDAV 连接配置。"
            : "当前还没有保存 WebDAV 连接。";
    }

    private void ApplyApplicationSetting(ApplicationSettingModel applicationSetting)
    {
        _applicationSettingId = applicationSetting.Id;
        TmdbReadAccessToken = applicationSetting.TmdbReadAccessToken;
        TmdbApiKey = applicationSetting.TmdbApiKey;
        OmdbApiKey = applicationSetting.OmdbApiKey;
        OpenSubtitlesEndpoint = string.IsNullOrWhiteSpace(applicationSetting.OpenSubtitlesEndpoint)
            ? "https://api.opensubtitles.com/api/v1"
            : applicationSetting.OpenSubtitlesEndpoint;
        OpenSubtitlesApiKey = applicationSetting.OpenSubtitlesApiKey;
        OpenSubtitlesUsername = applicationSetting.OpenSubtitlesUsername;
        OpenSubtitlesPassword = applicationSetting.OpenSubtitlesPassword;
        _openSubtitlesToken = applicationSetting.OpenSubtitlesToken;
        IsOpenSubtitlesEnabled = true;
        SelectedOpenSubtitlesLanguage = FindOpenSubtitlesLanguage(applicationSetting.OpenSubtitlesDefaultLanguageCode);
        AiBaseUrl = applicationSetting.AiBaseUrl;
        AiApiKey = applicationSetting.AiApiKey;
        ApplyAiRouting(applicationSetting.AiRouting ?? AiModelRoutingSettings.FromStoredValue(applicationSetting.AiModel));
        MarkTmdbInputsAsPersisted();
        MarkOmdbInputsAsPersisted();
        MarkOpenSubtitlesInputsAsPersisted();
        MarkAiInputsAsPersisted();
        SelectedThemeMode = NormalizeThemeMode(applicationSetting.ThemeMode);
        UpdateThemePresentation(SelectedThemeMode);
        TmdbStatusMessage = "已加载 TMDB 配置。";
        OmdbStatusMessage = "已加载 OMDb 配置。";
        OpenSubtitlesStatusMessage = "已加载 OpenSubtitles 配置。";
        ApiStatusMessage = "已加载大模型配置。";
        ThemeStatusMessage = $"当前主题：{FormatThemeMode(SelectedThemeMode)}";
    }

    private void ApplyBehaviorPreferences(AppBehaviorPreferencesModel preferences)
    {
        SelectedCloseWindowBehavior = preferences.CloseWindowBehavior;
        StartPlayerFullscreenOnPlay = preferences.StartPlayerFullscreenOnPlay;
        AutoScanWebDavOnStartup = preferences.AutoScanWebDavOnStartup;
    }

    private void ApplyAiRouting(AiModelRoutingSettings routing)
    {
        AiModel = routing.DefaultModel;
        AiDetailCorrectionModel = routing.SingleSourceCorrection.Model;
        AiDetailCorrectionTimeoutSeconds = routing.SingleSourceCorrection.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        AiBatchCorrectionModel = routing.BatchCorrection.Model;
        AiBatchCorrectionTimeoutSeconds = routing.BatchCorrection.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        AiScanTvUncertainRangeModel = routing.ScanTvUncertainRange.Model;
        AiScanTvUncertainRangeTimeoutSeconds = routing.ScanTvUncertainRange.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        AiScanTvFullRangeModel = routing.ScanTvFullRange.Model;
        AiScanTvFullRangeTimeoutSeconds = routing.ScanTvFullRange.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        AiScanMovieTaggingModel = routing.ScanMovieTagging.Model;
        AiScanMovieTaggingTimeoutSeconds = routing.ScanMovieTagging.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        AiRecommendationModel = routing.Recommendation.Model;
        AiRecommendationTimeoutSeconds = routing.Recommendation.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        AiWatchProfileModel = routing.WatchProfile.Model;
        AiWatchProfileTimeoutSeconds = routing.WatchProfile.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
    }

    private void OnTmdbInputsChanged()
    {
        OnPropertyChanged(nameof(TmdbConfigStatusText));
        TmdbConfigStatusKind = ApiConfigStatusUntested;
        SaveTmdbSettingsCommand.RaiseCanExecuteChanged();
    }

    private void OnOmdbInputsChanged()
    {
        OnPropertyChanged(nameof(OmdbConfigStatusText));
        OmdbConfigStatusKind = ApiConfigStatusUntested;
        SaveOmdbSettingsCommand.RaiseCanExecuteChanged();
    }

    private void OnOpenSubtitlesInputsChanged()
    {
        OnPropertyChanged(nameof(OpenSubtitlesConfigStatusText));
        OpenSubtitlesConfigStatusKind = ApiConfigStatusUntested;
        SaveOpenSubtitlesSettingsCommand.RaiseCanExecuteChanged();
    }

    private void OnAiInputsChanged()
    {
        OnPropertyChanged(nameof(AiConfigStatusText));
        AiConfigStatusKind = ApiConfigStatusUntested;
        SaveAiSettingsCommand.RaiseCanExecuteChanged();
    }

    private bool HasTmdbSettingsChanges()
    {
        return !StringEqualsPersisted(TmdbReadAccessToken, _loadedTmdbReadAccessToken)
               || !StringEqualsPersisted(TmdbApiKey, _loadedTmdbApiKey);
    }

    private bool HasOmdbSettingsChanges()
    {
        return !StringEqualsPersisted(OmdbApiKey, _loadedOmdbApiKey);
    }

    private bool HasOpenSubtitlesSettingsChanges()
    {
        return !StringEqualsPersisted(NormalizePersistedText(OpenSubtitlesEndpoint).TrimEnd('/'), _loadedOpenSubtitlesEndpoint.TrimEnd('/'))
               || !StringEqualsPersisted(OpenSubtitlesApiKey, _loadedOpenSubtitlesApiKey)
               || !StringEqualsPersisted(OpenSubtitlesUsername, _loadedOpenSubtitlesUsername)
               || !string.Equals(OpenSubtitlesPassword ?? string.Empty, _loadedOpenSubtitlesPassword ?? string.Empty, StringComparison.Ordinal)
               || !string.Equals(GetSelectedOpenSubtitlesLanguageCode(), _loadedOpenSubtitlesLanguageCode, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasAiSettingsChanges()
    {
        return !StringEqualsPersisted(AiBaseUrl, _loadedAiBaseUrl)
               || !StringEqualsPersisted(AiApiKey, _loadedAiApiKey)
               || !StringEqualsPersisted(AiModel, _loadedAiModel)
               || !StringEqualsPersisted(AiDetailCorrectionModel, _loadedAiDetailCorrectionModel)
               || !StringEqualsPersisted(AiDetailCorrectionTimeoutSeconds, _loadedAiDetailCorrectionTimeoutSeconds)
               || !StringEqualsPersisted(AiBatchCorrectionModel, _loadedAiBatchCorrectionModel)
               || !StringEqualsPersisted(AiBatchCorrectionTimeoutSeconds, _loadedAiBatchCorrectionTimeoutSeconds)
               || !StringEqualsPersisted(AiScanTvUncertainRangeModel, _loadedAiScanTvUncertainRangeModel)
               || !StringEqualsPersisted(AiScanTvUncertainRangeTimeoutSeconds, _loadedAiScanTvUncertainRangeTimeoutSeconds)
               || !StringEqualsPersisted(AiScanTvFullRangeModel, _loadedAiScanTvFullRangeModel)
               || !StringEqualsPersisted(AiScanTvFullRangeTimeoutSeconds, _loadedAiScanTvFullRangeTimeoutSeconds)
               || !StringEqualsPersisted(AiScanMovieTaggingModel, _loadedAiScanMovieTaggingModel)
               || !StringEqualsPersisted(AiScanMovieTaggingTimeoutSeconds, _loadedAiScanMovieTaggingTimeoutSeconds)
               || !StringEqualsPersisted(AiRecommendationModel, _loadedAiRecommendationModel)
               || !StringEqualsPersisted(AiRecommendationTimeoutSeconds, _loadedAiRecommendationTimeoutSeconds)
               || !StringEqualsPersisted(AiWatchProfileModel, _loadedAiWatchProfileModel)
               || !StringEqualsPersisted(AiWatchProfileTimeoutSeconds, _loadedAiWatchProfileTimeoutSeconds);
    }

    private void MarkTmdbInputsAsPersisted()
    {
        _loadedTmdbReadAccessToken = NormalizePersistedText(TmdbReadAccessToken);
        _loadedTmdbApiKey = NormalizePersistedText(TmdbApiKey);
        SaveTmdbSettingsCommand.RaiseCanExecuteChanged();
    }

    private void MarkOmdbInputsAsPersisted()
    {
        _loadedOmdbApiKey = NormalizePersistedText(OmdbApiKey);
        SaveOmdbSettingsCommand.RaiseCanExecuteChanged();
    }

    private void MarkOpenSubtitlesInputsAsPersisted()
    {
        _loadedOpenSubtitlesEndpoint = NormalizePersistedText(OpenSubtitlesEndpoint).TrimEnd('/');
        _loadedOpenSubtitlesApiKey = NormalizePersistedText(OpenSubtitlesApiKey);
        _loadedOpenSubtitlesUsername = NormalizePersistedText(OpenSubtitlesUsername);
        _loadedOpenSubtitlesPassword = OpenSubtitlesPassword ?? string.Empty;
        _loadedOpenSubtitlesLanguageCode = GetSelectedOpenSubtitlesLanguageCode();
        SaveOpenSubtitlesSettingsCommand.RaiseCanExecuteChanged();
    }

    private void MarkAiInputsAsPersisted()
    {
        _loadedAiBaseUrl = NormalizePersistedText(AiBaseUrl);
        _loadedAiApiKey = NormalizePersistedText(AiApiKey);
        _loadedAiModel = NormalizePersistedText(AiModel);
        _loadedAiDetailCorrectionModel = NormalizePersistedText(AiDetailCorrectionModel);
        _loadedAiDetailCorrectionTimeoutSeconds = NormalizePersistedText(AiDetailCorrectionTimeoutSeconds);
        _loadedAiBatchCorrectionModel = NormalizePersistedText(AiBatchCorrectionModel);
        _loadedAiBatchCorrectionTimeoutSeconds = NormalizePersistedText(AiBatchCorrectionTimeoutSeconds);
        _loadedAiScanTvUncertainRangeModel = NormalizePersistedText(AiScanTvUncertainRangeModel);
        _loadedAiScanTvUncertainRangeTimeoutSeconds = NormalizePersistedText(AiScanTvUncertainRangeTimeoutSeconds);
        _loadedAiScanTvFullRangeModel = NormalizePersistedText(AiScanTvFullRangeModel);
        _loadedAiScanTvFullRangeTimeoutSeconds = NormalizePersistedText(AiScanTvFullRangeTimeoutSeconds);
        _loadedAiScanMovieTaggingModel = NormalizePersistedText(AiScanMovieTaggingModel);
        _loadedAiScanMovieTaggingTimeoutSeconds = NormalizePersistedText(AiScanMovieTaggingTimeoutSeconds);
        _loadedAiRecommendationModel = NormalizePersistedText(AiRecommendationModel);
        _loadedAiRecommendationTimeoutSeconds = NormalizePersistedText(AiRecommendationTimeoutSeconds);
        _loadedAiWatchProfileModel = NormalizePersistedText(AiWatchProfileModel);
        _loadedAiWatchProfileTimeoutSeconds = NormalizePersistedText(AiWatchProfileTimeoutSeconds);
        SaveAiSettingsCommand.RaiseCanExecuteChanged();
    }

    private string GetSelectedOpenSubtitlesLanguageCode()
    {
        return string.IsNullOrWhiteSpace(SelectedOpenSubtitlesLanguage?.Code)
            ? "zh-cn"
            : SelectedOpenSubtitlesLanguage.Code.Trim();
    }

    private static bool StringEqualsPersisted(string? current, string? loaded)
    {
        return string.Equals(
            NormalizePersistedText(current),
            NormalizePersistedText(loaded),
            StringComparison.Ordinal);
    }

    private static string NormalizePersistedText(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private AiModelRoutingSettings BuildAiRoutingFromInputs()
    {
        return new AiModelRoutingSettings
        {
            DefaultModel = RequireModel(AiModel, "默认模型"),
            SingleSourceCorrection = new AiModelRoutingSettings.AiModelRoute(
                RequireModel(AiDetailCorrectionModel, "详情页 AI 修正模型"),
                ParsePositiveSeconds(AiDetailCorrectionTimeoutSeconds, "详情页 AI 修正超时")),
            BatchCorrection = new AiModelRoutingSettings.AiModelRoute(
                RequireModel(AiBatchCorrectionModel, "批量 AI 识别模型"),
                ParsePositiveSeconds(AiBatchCorrectionTimeoutSeconds, "批量 AI 识别超时")),
            ScanTvUncertainRange = new AiModelRoutingSettings.AiModelRoute(
                RequireModel(AiScanTvUncertainRangeModel, "扫描 TV 不确定范围模型"),
                ParsePositiveSeconds(AiScanTvUncertainRangeTimeoutSeconds, "扫描 TV 不确定范围超时")),
            ScanTvFullRange = new AiModelRoutingSettings.AiModelRoute(
                RequireModel(AiScanTvFullRangeModel, "扫描 TV 全量范围模型"),
                ParsePositiveSeconds(AiScanTvFullRangeTimeoutSeconds, "扫描 TV 全量范围超时")),
            ScanMovieTagging = new AiModelRoutingSettings.AiModelRoute(
                RequireModel(AiScanMovieTaggingModel, "扫描电影标签模型"),
                ParsePositiveSeconds(AiScanMovieTaggingTimeoutSeconds, "扫描电影标签超时")),
            Recommendation = new AiModelRoutingSettings.AiModelRoute(
                RequireModel(AiRecommendationModel, "AI 推荐模型"),
                ParsePositiveSeconds(AiRecommendationTimeoutSeconds, "AI 推荐超时")),
            WatchProfile = new AiModelRoutingSettings.AiModelRoute(
                RequireModel(AiWatchProfileModel, "观影画像模型"),
                ParsePositiveSeconds(AiWatchProfileTimeoutSeconds, "观影画像超时"))
        };
    }

    private OpenSubtitlesClientOptions BuildOpenSubtitlesOptionsFromInputs()
    {
        return new OpenSubtitlesClientOptions
        {
            Endpoint = OpenSubtitlesEndpoint,
            ApiKey = OpenSubtitlesApiKey,
            Username = OpenSubtitlesUsername,
            Password = OpenSubtitlesPassword,
            Token = _openSubtitlesToken,
            DefaultLanguageCode = GetSelectedOpenSubtitlesLanguageCode()
        };
    }

    private void ClearOpenSubtitlesTokenIfCredentialsChanged()
    {
        if (!HasOpenSubtitlesCredentialInputsChanged())
        {
            return;
        }

        _openSubtitlesToken = string.Empty;
    }

    private bool HasOpenSubtitlesCredentialInputsChanged()
    {
        return !string.Equals(NormalizeOpenSubtitlesText(OpenSubtitlesEndpoint).TrimEnd('/'), NormalizeOpenSubtitlesText(_loadedOpenSubtitlesEndpoint).TrimEnd('/'), StringComparison.Ordinal)
               || !string.Equals(NormalizeOpenSubtitlesText(OpenSubtitlesApiKey), NormalizeOpenSubtitlesText(_loadedOpenSubtitlesApiKey), StringComparison.Ordinal)
               || !string.Equals(NormalizeOpenSubtitlesText(OpenSubtitlesUsername), NormalizeOpenSubtitlesText(_loadedOpenSubtitlesUsername), StringComparison.Ordinal)
               || !string.Equals(OpenSubtitlesPassword ?? string.Empty, _loadedOpenSubtitlesPassword ?? string.Empty, StringComparison.Ordinal);
    }

    private static string NormalizeOpenSubtitlesText(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static bool IsOpenSubtitlesAuthFailure(OpenSubtitlesErrorKind errorKind)
    {
        return errorKind is OpenSubtitlesErrorKind.Unauthorized or OpenSubtitlesErrorKind.Forbidden;
    }

    private static bool IsOpenSubtitlesProbeSuccessful(OpenSubtitlesProbeResult result)
    {
        return result.IsApiKeyConfigured
               && result.IsApiKeyAccepted
               && (!result.LoginAttempted || result.LoginSucceeded);
    }

    private void ApplyOpenSubtitlesInputs(ApplicationSettingModel settings)
    {
        settings.OpenSubtitlesEndpoint = string.IsNullOrWhiteSpace(OpenSubtitlesEndpoint)
            ? "https://api.opensubtitles.com/api/v1"
            : OpenSubtitlesEndpoint;
        settings.OpenSubtitlesApiKey = OpenSubtitlesApiKey;
        settings.OpenSubtitlesUsername = OpenSubtitlesUsername;
        settings.OpenSubtitlesPassword = OpenSubtitlesPassword;
        settings.OpenSubtitlesToken = _openSubtitlesToken;
        settings.OpenSubtitlesDefaultLanguageCode = GetSelectedOpenSubtitlesLanguageCode();
        settings.IsOpenSubtitlesEnabled = true;
    }

    private OpenSubtitlesLanguageOption? FindOpenSubtitlesLanguage(string? code)
    {
        var normalized = string.IsNullOrWhiteSpace(code) ? "zh-cn" : code.Trim();
        return OpenSubtitlesLanguages.FirstOrDefault(
                   x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase))
               ?? OpenSubtitlesLanguages.FirstOrDefault(
                   x => string.Equals(x.Code, "zh-cn", StringComparison.OrdinalIgnoreCase))
               ?? OpenSubtitlesLanguages.FirstOrDefault();
    }

    private static int GetOpenSubtitlesLanguagePriority(string code)
    {
        return code.ToLowerInvariant() switch
        {
            "zh-cn" => 0,
            "zh-tw" => 1,
            "ze" => 2,
            "zh-ca" => 3,
            "en" => 4,
            "ja" => 5,
            "ko" => 6,
            "fr" => 7,
            "de" => 8,
            "es" => 9,
            "ru" => 10,
            "it" => 11,
            "pt-br" => 12,
            "pt-pt" => 13,
            _ => 100
        };
    }

    private static string LocalizeOpenSubtitlesLanguageName(OpenSubtitlesLanguageOption language)
    {
        return language.Code.ToLowerInvariant() switch
        {
            "ab" => "阿布哈兹语",
            "af" => "南非荷兰语",
            "sq" => "阿尔巴尼亚语",
            "am" => "阿姆哈拉语",
            "ar" => "阿拉伯语",
            "an" => "阿拉贡语",
            "hy" => "亚美尼亚语",
            "as" => "阿萨姆语",
            "at" => "阿斯图里亚斯语",
            "az-az" => "阿塞拜疆语",
            "eu" => "巴斯克语",
            "be" => "白俄罗斯语",
            "bn" => "孟加拉语",
            "bs" => "波斯尼亚语",
            "br" => "布列塔尼语",
            "bg" => "保加利亚语",
            "my" => "缅甸语",
            "ca" => "加泰罗尼亚语",
            "ze" => "中英双语",
            "zh-ca" => "粤语中文",
            "zh-cn" => "简体中文",
            "zh-tw" => "繁体中文",
            "hr" => "克罗地亚语",
            "cs" => "捷克语",
            "da" => "丹麦语",
            "pr" => "达里语",
            "nl" => "荷兰语",
            "en" => "英语",
            "eo" => "世界语",
            "et" => "爱沙尼亚语",
            "ex" => "埃斯特雷马杜拉语",
            "fi" => "芬兰语",
            "fr" => "法语",
            "gd" => "盖尔语",
            "gl" => "加利西亚语",
            "ka" => "格鲁吉亚语",
            "de" => "德语",
            "el" => "希腊语",
            "he" => "希伯来语",
            "hi" => "印地语",
            "hu" => "匈牙利语",
            "is" => "冰岛语",
            "ig" => "伊博语",
            "id" => "印度尼西亚语",
            "ia" => "国际语",
            "ga" => "爱尔兰语",
            "it" => "意大利语",
            "ja" => "日语",
            "kn" => "卡纳达语",
            "kk" => "哈萨克语",
            "km" => "高棉语",
            "ko" => "韩语",
            "ku" => "库尔德语",
            "lv" => "拉脱维亚语",
            "lt" => "立陶宛语",
            "lb" => "卢森堡语",
            "mk" => "马其顿语",
            "ms" => "马来语",
            "ml" => "马拉雅拉姆语",
            "ma" => "曼尼普尔语",
            "mr" => "马拉地语",
            "mn" => "蒙古语",
            "me" => "黑山语",
            "nv" => "纳瓦霍语",
            "ne" => "尼泊尔语",
            "se" => "北萨米语",
            "no" => "挪威语",
            "oc" => "奥克语",
            "or" => "奥里亚语",
            "fa" => "波斯语",
            "pl" => "波兰语",
            "pt-pt" => "葡萄牙语",
            "pt-br" => "葡萄牙语（巴西）",
            "pm" => "葡萄牙语（莫桑比克）",
            "ps" => "普什图语",
            "ro" => "罗马尼亚语",
            "ru" => "俄语",
            "sx" => "桑塔利语",
            "sr" => "塞尔维亚语",
            "sd" => "信德语",
            "si" => "僧伽罗语",
            "sk" => "斯洛伐克语",
            "sl" => "斯洛文尼亚语",
            "so" => "索马里语",
            "az-zb" => "南阿塞拜疆语",
            "es" => "西班牙语",
            "sp" => "西班牙语（欧洲）",
            "ea" => "西班牙语（拉美）",
            "sw" => "斯瓦希里语",
            "sv" => "瑞典语",
            "sy" => "叙利亚语",
            "tl" => "他加禄语",
            "ta" => "泰米尔语",
            "tt" => "鞑靼语",
            "te" => "泰卢固语",
            "tm-td" => "德顿语",
            "th" => "泰语",
            "tp" => "道本语",
            "tr" => "土耳其语",
            "tk" => "土库曼语",
            "uk" => "乌克兰语",
            "ur" => "乌尔都语",
            "uz" => "乌兹别克语",
            "vi" => "越南语",
            "cy" => "威尔士语",
            _ => language.Name
        };
    }

    private static string FormatOpenSubtitlesProbeResult(OpenSubtitlesProbeResult result)
    {
        if (!result.IsApiKeyConfigured)
        {
            return "OpenSubtitles API Key 未配置：请先填写在线字幕 API Key 后再测试。";
        }

        if (!result.IsApiKeyAccepted)
        {
            return FormatOpenSubtitlesProbeFailure(result);
        }

        if (result.LoginAttempted && !result.LoginSucceeded)
        {
            return $"API Key 可用，但账号/密码登录失败：{FormatOpenSubtitlesProbeFailure(result)} 如不需要登录，可清空账号和密码后使用 API key-only 模式。";
        }

        var loginText = result.LoginAttempted
            ? result.LoginSucceeded ? "登录成功" : "登录失败"
            : "未配置账号密码，已跳过登录";
        var quotaText = result.QuotaProbeSucceeded
            ? $"额度：remaining={result.RemainingDownloads?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, allowed={result.AllowedDownloads?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}"
            : "额度：未能提前查询，将在下载返回中提示";
        return $"API Key 可用；{loginText}；{quotaText}。";
    }

    private static string FormatOpenSubtitlesProbeFailure(OpenSubtitlesProbeResult result)
    {
        return result.ErrorKind switch
        {
            OpenSubtitlesErrorKind.NotConfigured => "未填写 API Key。",
            OpenSubtitlesErrorKind.Unauthorized => "OpenSubtitles 鉴权失败：请检查 API Key 是否复制完整、是否仍有效；如果配置了账号密码，也请检查账号密码。",
            OpenSubtitlesErrorKind.Forbidden => "OpenSubtitles 拒绝访问：当前 API Key 无效、无权限或已被禁用，请重新填写有效 API Key 后再测试。",
            OpenSubtitlesErrorKind.RateLimited => "OpenSubtitles 测试请求被限流或额度受限，请稍后再试。",
            OpenSubtitlesErrorKind.ServerError => "OpenSubtitles 服务暂时不可用，请稍后再试。",
            OpenSubtitlesErrorKind.Network => "无法连接 OpenSubtitles：请检查网络、代理或防火墙设置。",
            OpenSubtitlesErrorKind.InvalidResponse => "OpenSubtitles 返回格式异常：测试请求没有得到有效的字幕搜索响应。",
            _ => string.IsNullOrWhiteSpace(result.Message)
                ? "OpenSubtitles 测试失败，原因未知。"
                : $"OpenSubtitles 测试失败：{result.Message}"
        };
    }

    private static string RequireModel(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label}不能为空。");
        }

        return value.Trim();
    }

    private static int ParsePositiveSeconds(string value, string label)
    {
        if (!int.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
        {
            throw new InvalidOperationException($"{label}必须是正整数秒数。");
        }

        return seconds;
    }

    private async Task SaveConnectionAsync()
    {
        try
        {
            var connection = await _settingsService.SaveConnectionAsync(
                new WebDavConnectionModel
                {
                    Id = ConnectionId,
                    Name = ConnectionName,
                    BaseUrl = BaseUrl,
                    Username = Username,
                    Password = Password,
                    IsEnabled = IsConnectionEnabled
                });

            ApplyConnection(connection);
            ConnectionStatusMessage = "WebDAV 连接配置已保存。";

            if (ConnectionId.HasValue)
            {
                await LoadScanPathsAsync(ConnectionId.Value, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            ConnectionStatusMessage = exception.Message;
        }
    }

    private async Task TestConnectionAsync()
    {
        var result = await _webDavService.TestConnectionAsync(
            new WebDavConnectionModel
            {
                Id = ConnectionId,
                Name = ConnectionName,
                BaseUrl = BaseUrl,
                Username = Username,
                Password = Password,
                IsEnabled = IsConnectionEnabled
            });

        ConnectionStatusMessage = result.Message;
    }

    private async Task SaveTmdbSettingsAsync()
    {
        try
        {
            var saved = await SaveApplicationSettingsAsync(settings =>
            {
                settings.TmdbReadAccessToken = TmdbReadAccessToken;
                settings.TmdbApiKey = TmdbApiKey;
            });
            _applicationSettingId = saved.Id;
            TmdbReadAccessToken = saved.TmdbReadAccessToken;
            TmdbApiKey = saved.TmdbApiKey;
            MarkTmdbInputsAsPersisted();
            TmdbStatusMessage = "TMDB 认证信息已保存。";
            await TestTmdbConnectionAsync();
        }
        catch (Exception exception)
        {
            TmdbConfigStatusKind = ApiConfigStatusFailure;
            TmdbStatusMessage = $"保存 TMDB 配置失败：{exception.Message}";
        }
    }

    private async Task TestTmdbConnectionAsync()
    {
        var hasTmdbCredential = !string.IsNullOrWhiteSpace(TmdbReadAccessToken)
                                || !string.IsNullOrWhiteSpace(TmdbApiKey);
        if (!hasTmdbCredential)
        {
            TmdbConfigStatusKind = ApiConfigStatusFailure;
            TmdbStatusMessage = "请先填写 TMDB Read Access Token 或 API Key。";
            return;
        }

        try
        {
            TmdbConfigStatusKind = ApiConfigStatusUntested;
            TmdbStatusMessage = "正在测试 TMDB 连接...";
            var settings = await _settingsService.GetApplicationSettingAsync();
            await TestTmdbAsync(settings.TmdbBaseUrl);
            TmdbConfigStatusKind = ApiConfigStatusSuccess;
            TmdbStatusMessage = "TMDB 连接正常。";
        }
        catch (Exception exception)
        {
            TmdbConfigStatusKind = ApiConfigStatusFailure;
            TmdbStatusMessage = $"TMDB 连接失败：{exception.Message}";
        }
    }

    private async Task SaveOmdbSettingsAsync()
    {
        try
        {
            var saved = await SaveApplicationSettingsAsync(settings =>
            {
                settings.OmdbApiKey = OmdbApiKey;
            });
            _applicationSettingId = saved.Id;
            OmdbApiKey = saved.OmdbApiKey;
            MarkOmdbInputsAsPersisted();
            OmdbStatusMessage = "OMDb 认证信息已保存。";
            await TestOmdbConnectionAsync();
        }
        catch (Exception exception)
        {
            OmdbConfigStatusKind = ApiConfigStatusFailure;
            OmdbStatusMessage = $"保存 OMDb 配置失败：{exception.Message}";
        }
    }

    private async Task TestOmdbConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(OmdbApiKey))
        {
            OmdbConfigStatusKind = ApiConfigStatusFailure;
            OmdbStatusMessage = "请先填写 OMDb API Key。";
            return;
        }

        try
        {
            OmdbConfigStatusKind = ApiConfigStatusUntested;
            OmdbStatusMessage = "正在测试 OMDb 连接...";
            await TestOmdbAsync();
            OmdbConfigStatusKind = ApiConfigStatusSuccess;
            OmdbStatusMessage = "OMDb 连接正常。";
        }
        catch (Exception exception)
        {
            OmdbConfigStatusKind = ApiConfigStatusFailure;
            OmdbStatusMessage = $"OMDb 连接失败：{exception.Message}";
        }
    }

    private async Task SaveOpenSubtitlesSettingsAsync()
    {
        try
        {
            ClearOpenSubtitlesTokenIfCredentialsChanged();
            var saved = await SaveApplicationSettingsAsync(settings =>
            {
                ApplyOpenSubtitlesInputs(settings);
            });
            _applicationSettingId = saved.Id;
            OpenSubtitlesEndpoint = saved.OpenSubtitlesEndpoint;
            OpenSubtitlesApiKey = saved.OpenSubtitlesApiKey;
            OpenSubtitlesUsername = saved.OpenSubtitlesUsername;
            OpenSubtitlesPassword = saved.OpenSubtitlesPassword;
            _openSubtitlesToken = saved.OpenSubtitlesToken;
            IsOpenSubtitlesEnabled = true;
            SelectedOpenSubtitlesLanguage = FindOpenSubtitlesLanguage(saved.OpenSubtitlesDefaultLanguageCode);
            MarkOpenSubtitlesInputsAsPersisted();
            OpenSubtitlesStatusMessage = "OpenSubtitles 配置已保存。";
            await TestOpenSubtitlesConnectionAsync();
        }
        catch (Exception exception)
        {
            OpenSubtitlesConfigStatusKind = ApiConfigStatusFailure;
            OpenSubtitlesStatusMessage = $"保存 OpenSubtitles 配置失败：{exception.Message}";
        }
    }

    private async Task TestOpenSubtitlesConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(OpenSubtitlesApiKey))
        {
            OpenSubtitlesConfigStatusKind = ApiConfigStatusFailure;
            OpenSubtitlesStatusMessage = "请先填写 OpenSubtitles API Key。";
            return;
        }

        try
        {
            OpenSubtitlesConfigStatusKind = ApiConfigStatusUntested;
            OpenSubtitlesStatusMessage = "正在探测 OpenSubtitles API 能力...";
            ClearOpenSubtitlesTokenIfCredentialsChanged();
            var result = await _openSubtitlesClientService.ProbeAsync(BuildOpenSubtitlesOptionsFromInputs());
            if (!string.IsNullOrWhiteSpace(result.Token))
            {
                _openSubtitlesToken = result.Token;
            }
            else if (IsOpenSubtitlesAuthFailure(result.ErrorKind) && !string.IsNullOrWhiteSpace(_openSubtitlesToken))
            {
                _openSubtitlesToken = string.Empty;
            }

            OpenSubtitlesConfigStatusKind = IsOpenSubtitlesProbeSuccessful(result)
                ? ApiConfigStatusSuccess
                : ApiConfigStatusFailure;
            OpenSubtitlesStatusMessage = FormatOpenSubtitlesProbeResult(result);
        }
        catch (Exception exception)
        {
            OpenSubtitlesConfigStatusKind = ApiConfigStatusFailure;
            OpenSubtitlesStatusMessage = $"OpenSubtitles 探测失败：{exception.GetType().Name}";
        }
    }

    private async Task SaveAiSettingsAsync()
    {
        try
        {
            var saved = await SaveApplicationSettingsAsync(settings =>
            {
                settings.AiBaseUrl = AiBaseUrl;
                settings.AiApiKey = AiApiKey;
                settings.AiModel = AiModel;
                settings.AiRouting = BuildAiRoutingFromInputs();
            });
            _applicationSettingId = saved.Id;
            AiBaseUrl = saved.AiBaseUrl;
            AiApiKey = saved.AiApiKey;
            ApplyAiRouting(saved.AiRouting);
            MarkAiInputsAsPersisted();
            ApiStatusMessage = "AI 接口配置已保存。";
            await TestAiSettingsAsync();
        }
        catch (Exception exception)
        {
            AiConfigStatusKind = ApiConfigStatusFailure;
            ApiStatusMessage = $"保存 AI 接口配置失败：{exception.Message}";
        }
    }

    private async Task TestAiSettingsAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AiBaseUrl)
                || string.IsNullOrWhiteSpace(AiApiKey)
                || string.IsNullOrWhiteSpace(AiModel))
            {
                AiConfigStatusKind = ApiConfigStatusFailure;
                ApiStatusMessage = "请先填写 AI Base URL、API Key 和默认模型。";
                return;
            }

            var endpoint = BuildAiProbeEndpoint(AiBaseUrl);
            var routing = BuildAiRoutingFromInputs();
            var models = CollectAiProbeModels(routing);
            if (models.Count == 0)
            {
                AiConfigStatusKind = ApiConfigStatusFailure;
                ApiStatusMessage = "请至少填写一个可测试的 AI 模型。";
                return;
            }

            AiConfigStatusKind = ApiConfigStatusUntested;
            ApiStatusMessage = $"正在测试 AI 模型：共 {models.Count} 个。";
            var failures = new List<string>();
            var successCount = 0;

            for (var index = 0; index < models.Count; index++)
            {
                var model = models[index];
                ApiStatusMessage = $"正在测试 AI 模型 {index + 1}/{models.Count}：{model}";
                try
                {
                    await TestAiModelAsync(endpoint, AiApiKey, model);
                    successCount++;
                }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
                {
                    failures.Add($"{model}：{FormatAiProbeError(exception)}");
                }
            }

            if (failures.Count == 0)
            {
                AiConfigStatusKind = ApiConfigStatusSuccess;
                ApiStatusMessage = $"AI 连接正常，已测试 {successCount} 个去重模型：{string.Join("、", models)}。";
                return;
            }

            AiConfigStatusKind = ApiConfigStatusFailure;
            ApiStatusMessage = $"AI 测试完成，{successCount}/{models.Count} 通过；失败：{string.Join("；", failures)}";
        }
        catch (Exception exception)
        {
            AiConfigStatusKind = ApiConfigStatusFailure;
            ApiStatusMessage = $"AI 测试失败：{FormatAiProbeError(exception)}";
        }
    }

    private static Uri BuildAiProbeEndpoint(string baseUrl)
    {
        var normalized = NormalizePersistedText(baseUrl).TrimEnd('/');
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("AI Base URL 必须是有效的绝对地址。");
        }

        var endpoint = normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? normalized + "/chat/completions"
            : normalized + "/v1/chat/completions";
        return new Uri(endpoint, UriKind.Absolute);
    }

    private static IReadOnlyList<string> CollectAiProbeModels(AiModelRoutingSettings routing)
    {
        var models = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddModel(routing.DefaultModel);
        AddModel(routing.SingleSourceCorrection.Model);
        AddModel(routing.BatchCorrection.Model);
        AddModel(routing.ScanTvUncertainRange.Model);
        AddModel(routing.ScanTvFullRange.Model);
        AddModel(routing.ScanMovieTagging.Model);
        AddModel(routing.Recommendation.Model);
        AddModel(routing.WatchProfile.Model);
        return models;

        void AddModel(string? model)
        {
            var normalized = NormalizePersistedText(model);
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                return;
            }

            models.Add(normalized);
        }
    }

    private async Task TestAiModelAsync(Uri endpoint, string apiKey, string model)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = new[]
            {
                new { role = "system", content = "You are a connectivity test." },
                new { role = "user", content = "Reply with OK." }
            },
            ["temperature"] = 0,
            ["max_tokens"] = 8
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(AiProbeTimeoutSeconds));
        using var response = await _aiApiHttpClient.SendAsync(request, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        if (!document.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("响应缺少 choices。");
        }
    }

    private static string FormatAiProbeError(Exception exception)
    {
        return exception switch
        {
            TaskCanceledException => $"请求超时（{AiProbeTimeoutSeconds}s）",
            HttpRequestException => string.IsNullOrWhiteSpace(exception.Message) ? "HTTP 请求失败" : exception.Message,
            JsonException => "响应不是有效 JSON",
            InvalidOperationException => exception.Message,
            _ => exception.GetType().Name
        };
    }

    private async Task TestTmdbAsync(string configuredBaseUrl)
    {
        Exception? lastException = null;
        foreach (var baseUri in BuildTmdbApiBaseUris(configuredBaseUrl))
        {
            try
            {
                var requestUri = !string.IsNullOrWhiteSpace(TmdbReadAccessToken)
                    ? new Uri(baseUri, "configuration")
                    : new Uri(baseUri, $"configuration?api_key={Uri.EscapeDataString(TmdbApiKey.Trim())}");
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(TmdbReadAccessToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TmdbReadAccessToken.Trim());
                }

                using var response = await _metadataApiHttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                if (!document.RootElement.TryGetProperty("images", out _))
                {
                    throw new InvalidOperationException("返回内容不符合 TMDB 配置接口格式。");
                }

                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                lastException = exception;
            }
        }

        throw new InvalidOperationException(lastException?.Message ?? "无法访问 TMDB。");
    }

    private async Task TestOmdbAsync()
    {
        var requestUri = new Uri(
            $"http://www.omdbapi.com/?i=tt1375666&apikey={Uri.EscapeDataString(OmdbApiKey.Trim())}",
            UriKind.Absolute);
        using var response = await _metadataApiHttpClient.GetAsync(requestUri);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        var isSuccess = root.TryGetProperty("Response", out var responseProperty)
                        && string.Equals(responseProperty.GetString(), "True", StringComparison.OrdinalIgnoreCase);
        if (!isSuccess)
        {
            var error = root.TryGetProperty("Error", out var errorProperty)
                ? errorProperty.GetString()
                : "OMDb 返回失败。";
            throw new InvalidOperationException(error);
        }
    }

    private static IEnumerable<Uri> BuildTmdbApiBaseUris(string? configuredBaseUrl)
    {
        const string defaultPrimaryApiBaseUrl = "https://api.tmdb.org/3/";
        const string defaultFallbackApiBaseUrl = "https://api.themoviedb.org/3/";
        var primary = new Uri(NormalizeTmdbApiBaseUrl(configuredBaseUrl, defaultPrimaryApiBaseUrl), UriKind.Absolute);
        var fallback = new Uri(defaultFallbackApiBaseUrl, UriKind.Absolute);

        yield return primary;
        if (!Uri.Compare(primary, fallback, UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase).Equals(0))
        {
            yield return fallback;
        }
    }

    private static string NormalizeTmdbApiBaseUrl(string? baseUrl, string fallbackBaseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? fallbackBaseUrl : baseUrl.Trim();
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        if (!normalized.EndsWith("/3/", StringComparison.Ordinal))
        {
            normalized = normalized.TrimEnd('/') + "/3/";
        }

        return normalized;
    }

    private async Task SaveThemeSettingsAsync()
    {
        await _themeService.ApplyAndSaveAsync(SelectedThemeMode);
        var settings = await _settingsService.GetApplicationSettingAsync();
        _applicationSettingId = settings.Id;
        SelectedThemeMode = NormalizeThemeMode(settings.ThemeMode);
        UpdateThemePresentation(SelectedThemeMode);
        ThemeStatusMessage = $"主题已切换为：{FormatThemeMode(SelectedThemeMode)}";
    }

    private async Task ToggleThemeSettingsAsync()
    {
        SelectedThemeMode = string.Equals(SelectedThemeMode, ThemeDark, StringComparison.OrdinalIgnoreCase)
            ? ThemeLight
            : ThemeDark;
        await SaveThemeSettingsAsync();
    }

    private async Task<ApplicationSettingModel> SaveApplicationSettingsAsync(Action<ApplicationSettingModel> configureSettings)
    {
        var existing = await _settingsService.GetApplicationSettingAsync();
        var settings = CopyApplicationSettings(existing);
        configureSettings(settings);
        var saved = await _settingsService.SaveApplicationSettingAsync(settings);
        _applicationSettingId = saved.Id;
        return saved;
    }

    private static ApplicationSettingModel CopyApplicationSettings(ApplicationSettingModel settings)
    {
        return new ApplicationSettingModel
        {
            Id = settings.Id,
            TmdbReadAccessToken = settings.TmdbReadAccessToken,
            TmdbApiKey = settings.TmdbApiKey,
            OmdbApiKey = settings.OmdbApiKey,
            ThemeMode = settings.ThemeMode,
            AiBaseUrl = settings.AiBaseUrl,
            AiApiKey = settings.AiApiKey,
            AiModel = settings.AiModel,
            AiRouting = settings.AiRouting.Clone(),
            RecentAiRecommendationsJson = settings.RecentAiRecommendationsJson,
            CurrentAiRecommendationsJson = settings.CurrentAiRecommendationsJson,
            AiRecommendationLibraryFingerprint = settings.AiRecommendationLibraryFingerprint,
            TmdbBaseUrl = settings.TmdbBaseUrl,
            OpenSubtitlesEndpoint = settings.OpenSubtitlesEndpoint,
            OpenSubtitlesApiKey = settings.OpenSubtitlesApiKey,
            OpenSubtitlesUsername = settings.OpenSubtitlesUsername,
            OpenSubtitlesPassword = settings.OpenSubtitlesPassword,
            OpenSubtitlesToken = settings.OpenSubtitlesToken,
            OpenSubtitlesDefaultLanguageCode = settings.OpenSubtitlesDefaultLanguageCode,
            IsOpenSubtitlesEnabled = true
        };
    }

    private async Task LoadSoftwareCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            ApplySoftwareCacheOverview(await _softwareCacheManagementService.GetOverviewAsync(cancellationToken));
            SoftwareCacheStatusMessage = "已加载软件缓存状态。";
        }
        catch (Exception exception)
        {
            SoftwareCacheStatusMessage = $"加载软件缓存状态失败：{FormatCacheError(exception)}";
        }
    }

    private async Task RefreshSoftwareCacheAsync()
    {
        try
        {
            ApplySoftwareCacheOverview(await _softwareCacheManagementService.GetOverviewAsync());
            SoftwareCacheStatusMessage = "软件缓存状态已刷新。";
        }
        catch (Exception exception)
        {
            SoftwareCacheStatusMessage = $"刷新软件缓存状态失败：{FormatCacheError(exception)}";
        }
    }

    private async Task SavePosterCacheLimitAsync()
    {
        try
        {
            var maxBytes = ParseMegabytes(PosterCacheMaxMbText, "海报缓存容量上限");
            ApplySoftwareCacheOverview(await _softwareCacheManagementService.SavePosterCacheLimitAsync(maxBytes));
            PosterCacheStatusMessage = $"海报缓存容量上限已保存为 {FormatFileSize(maxBytes)}，已按上限裁剪旧缓存。";
            SoftwareCacheStatusMessage = "海报缓存设置已保存。";
        }
        catch (Exception exception)
        {
            PosterCacheStatusMessage = $"保存海报缓存容量上限失败：{FormatCacheError(exception)}";
        }
    }

    private async Task ClearPosterCacheAsync()
    {
        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "清理海报缓存？",
            "将删除本机海报缓存文件。不会删除影片、用户数据、视频缓存或人格海报；后续打开页面时会重新生成海报缓存。",
            "清理",
            "取消",
            ConfirmationDialogVariant.Danger);

        if (!confirmed)
        {
            PosterCacheStatusMessage = "已取消清理海报缓存。";
            return;
        }

        try
        {
            var result = await _softwareCacheManagementService.ClearAsync(SoftwareCacheCategoryKind.PosterCache);
            if (result.Succeeded)
            {
                PosterCacheImageBehavior.ClearMemoryCache();
            }

            ApplySoftwareCacheOverview(await _softwareCacheManagementService.GetOverviewAsync());
            PosterCacheStatusMessage = result.Succeeded
                ? $"已清理海报缓存，删除 {result.DeletedItemCount} 个文件，释放 {FormatFileSize(result.FreedBytes)}。"
                : $"清理海报缓存失败：{result.Error ?? "部分缓存文件无法删除。"}";
            SoftwareCacheStatusMessage = "软件缓存状态已更新。";
        }
        catch (Exception exception)
        {
            PosterCacheStatusMessage = $"清理海报缓存失败：{FormatCacheError(exception)}";
        }
    }

    private async Task ClearOtherCacheAsync()
    {
        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "清理其他缓存？",
            "将只清理可再生成的 TMDB / OMDb 外部元数据缓存。不会删除影片、收藏、播放进度、观影历史、用户配置或推荐偏好。",
            "清理",
            "取消",
            ConfirmationDialogVariant.Danger);

        if (!confirmed)
        {
            OtherCacheStatusMessage = "已取消清理其他缓存。";
            return;
        }

        try
        {
            var result = await _softwareCacheManagementService.ClearAsync(SoftwareCacheCategoryKind.OtherCache);
            ApplySoftwareCacheOverview(await _softwareCacheManagementService.GetOverviewAsync());
            OtherCacheStatusMessage = result.Succeeded
                ? $"已清理其他缓存，删除 {result.DeletedItemCount} 条记录，估算释放 {FormatFileSize(result.FreedBytes)}。"
                : $"清理其他缓存失败：{result.Error ?? "缓存记录无法删除。"}";
            SoftwareCacheStatusMessage = "软件缓存状态已更新。";
        }
        catch (Exception exception)
        {
            OtherCacheStatusMessage = $"清理其他缓存失败：{FormatCacheError(exception)}";
        }
    }

    private async Task ClearSubtitleCacheAsync()
    {
        var confirmed = await _confirmationDialogService.ConfirmAsync(
            "清理孤立在线字幕缓存？",
            "只会删除在线字幕缓存目录中没有被 Movie、Episode 或当前播放源绑定引用的字幕文件。不会删除仍在绑定中的字幕缓存、本地媒体、WebDAV 文件或扫描发现的外挂字幕。",
            "清理孤立缓存",
            "取消",
            ConfirmationDialogVariant.Danger);

        if (!confirmed)
        {
            SubtitleCacheStatusMessage = "已取消清理在线字幕孤立缓存。";
            return;
        }

        try
        {
            var result = await _softwareCacheManagementService.ClearAsync(SoftwareCacheCategoryKind.SubtitleCache);
            ApplySoftwareCacheOverview(await _softwareCacheManagementService.GetOverviewAsync());
            SubtitleCacheStatusMessage = result.Succeeded
                ? $"已清理在线字幕孤立缓存，删除 {result.DeletedItemCount} 个文件，释放 {FormatFileSize(result.FreedBytes)}。"
                : $"在线字幕孤立缓存清理未完全完成：{FormatCacheError(result.Error)}";
            SoftwareCacheStatusMessage = "软件缓存状态已更新。";
        }
        catch (Exception exception)
        {
            SubtitleCacheStatusMessage = $"清理在线字幕孤立缓存失败：{FormatCacheError(exception)}";
        }
    }

    private void ApplySoftwareCacheOverview(SoftwareCacheOverview overview)
    {
        PosterCacheUsageText = FormatFileSize(overview.PosterCache.UsedBytes);
        PosterCacheFileCountText = $"{overview.PosterCache.ItemCount} 个文件";
        PosterCacheMaxMbText = FormatMegabytes(overview.PosterCacheMaxBytes);
        IsPosterCacheClearAvailable = overview.PosterCache.IsClearable;
        PosterCacheStatusMessage = overview.PosterCache.IsClearable
            ? $"可清理 {overview.PosterCache.ClearableItemCount} 个海报缓存文件，预计释放 {FormatFileSize(overview.PosterCache.ClearableBytes)}。"
            : overview.PosterCache.ClearUnavailableReason;

        OtherCacheDescriptionText = overview.OtherCache.Description;
        OtherCacheUsageText = overview.OtherCache.ItemCount > 0
            ? $"{overview.OtherCache.ItemCount} 条记录，估算 {FormatFileSize(overview.OtherCache.UsedBytes)}"
            : "当前没有可清理的其他缓存。";
        IsOtherCacheClearAvailable = overview.OtherCache.IsClearable;
        OtherCacheStatusMessage = overview.OtherCache.IsClearable
            ? "可清理 TMDB / OMDb 外部元数据缓存。"
            : overview.OtherCache.ClearUnavailableReason;

        SubtitleCacheUsageText = overview.SubtitleCache.ItemCount > 0
            ? $"{overview.SubtitleCache.ItemCount} 个文件，占用 {FormatFileSize(overview.SubtitleCache.UsedBytes)}"
            : "当前没有在线字幕缓存文件。";
        SubtitleCacheDetailText = overview.SubtitleCache.DetailText;
        IsSubtitleCacheClearAvailable = overview.SubtitleCache.IsClearable;
        SubtitleCacheStatusMessage = overview.SubtitleCache.IsClearable
            ? $"可清理 {overview.SubtitleCache.ClearableItemCount} 个孤立字幕文件，预计释放 {FormatFileSize(overview.SubtitleCache.ClearableBytes)}。"
            : overview.SubtitleCache.ClearUnavailableReason;
    }

    private static long ParseMegabytes(string value, string fieldName)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var number)
            && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
        {
            throw new InvalidOperationException($"{fieldName} 必须是有效数字。");
        }

        if (number <= 0)
        {
            throw new InvalidOperationException($"{fieldName} 必须大于 0。");
        }

        return (long)Math.Round(number * BytesPerMb, MidpointRounding.AwayFromZero);
    }

    private static string FormatMegabytes(long bytes)
    {
        var megabytes = (decimal)bytes / BytesPerMb;
        return megabytes % 1 == 0
            ? megabytes.ToString("0", CultureInfo.CurrentCulture)
            : megabytes.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value:0} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }

    private static string FormatCacheError(Exception exception)
    {
        return exception is InvalidOperationException or FormatException
            ? exception.Message
            : exception.GetType().Name;
    }

    private static string FormatCacheError(string? error)
    {
        return error switch
        {
            null or "" => "缓存清理失败。",
            "OnlineSubtitleBindingReferenceUnavailable" => "无法读取在线字幕绑定引用，已停止清理以避免误删仍在使用的字幕缓存。",
            "SomeOrphanSubtitleCacheFilesCouldNotBeDeleted" => "部分孤立字幕缓存文件无法删除，可能正在被播放器或系统占用。",
            _ => error
        };
    }

    private void BeginAddScanPath()
    {
        if (!HasSavedConnection)
        {
            ScanPathStatusMessage = "请先保存 WebDAV 连接配置。";
            return;
        }

        EditingScanPathId = null;
        EditingScanPathValue = string.Empty;
        EditingScanPathDisplayName = string.Empty;
        EditingScanPathEnabled = true;
        EditingScanPathRecursive = true;
        ScanPathStatusMessage = "正在新增扫描路径。";
    }

    private void EditScanPath(object? parameter)
    {
        if (parameter is not ScanPath scanPath)
        {
            return;
        }

        EditingScanPathId = scanPath.Id;
        EditingScanPathValue = scanPath.Path;
        EditingScanPathDisplayName = scanPath.DisplayName;
        EditingScanPathEnabled = scanPath.IsEnabled;
        EditingScanPathRecursive = scanPath.IsRecursive;
        ScanPathStatusMessage = $"正在编辑：{scanPath.DisplayName}";
    }

    private async Task SaveScanPathAsync()
    {
        if (!ConnectionId.HasValue)
        {
            ScanPathStatusMessage = "请先保存 WebDAV 连接配置。";
            return;
        }

        try
        {
            var savedScanPath = await _settingsService.SaveScanPathAsync(
                new ScanPath
                {
                    Id = EditingScanPathId ?? 0,
                    SourceConnectionId = ConnectionId.Value,
                    Path = EditingScanPathValue,
                    DisplayName = EditingScanPathDisplayName,
                    IsEnabled = EditingScanPathEnabled,
                    IsRecursive = EditingScanPathRecursive
                });

            await LoadScanPathsAsync(ConnectionId.Value, CancellationToken.None);
            ScanPathStatusMessage = $"扫描路径已保存：{savedScanPath.DisplayName}";
            CancelEditScanPath();
        }
        catch (Exception exception)
        {
            ScanPathStatusMessage = exception.Message;
        }
    }

    private async Task DeleteScanPathAsync(object? parameter)
    {
        if (parameter is not ScanPath scanPath || !ConnectionId.HasValue)
        {
            return;
        }

        await _settingsService.DeleteScanPathAsync(scanPath.Id);
        await LoadScanPathsAsync(ConnectionId.Value, CancellationToken.None);
        ScanPathStatusMessage = $"已删除扫描路径：{scanPath.DisplayName}";

        if (EditingScanPathId == scanPath.Id)
        {
            CancelEditScanPath();
        }
    }

    private async Task ToggleScanPathAsync(object? parameter)
    {
        if (parameter is not ScanPath scanPath || !ConnectionId.HasValue)
        {
            return;
        }

        await _settingsService.SetScanPathEnabledAsync(scanPath.Id, !scanPath.IsEnabled);
        await LoadScanPathsAsync(ConnectionId.Value, CancellationToken.None);
        ScanPathStatusMessage = scanPath.IsEnabled
            ? $"已停用扫描路径：{scanPath.DisplayName}"
            : $"已启用扫描路径：{scanPath.DisplayName}";
    }

    private void CancelEditScanPath()
    {
        EditingScanPathId = null;
        EditingScanPathValue = string.Empty;
        EditingScanPathDisplayName = string.Empty;
        EditingScanPathEnabled = true;
        EditingScanPathRecursive = true;
    }

    private void ToggleAboutDetails()
    {
        AboutStatusMessage = "本地桌面影音库；媒体源文件仍保留在本地目录或 WebDAV 远端。";
    }

    private void OpenOfficialWebsite()
    {
        try
        {
            Process.Start(new ProcessStartInfo(OfficialWebsiteUrl)
            {
                UseShellExecute = true
            });
            AboutStatusMessage = "正在打开 XFVerse 官网。";
        }
        catch (Exception exception)
        {
            AboutStatusMessage = $"打开官网失败：{exception.Message}";
        }
    }

    private void SelectSettingsTab(object? parameter)
    {
        if (!int.TryParse(parameter?.ToString(), out var tabIndex)
            || tabIndex < GeneralSettingsTabIndex
            || tabIndex > ApiSettingsTabIndex)
        {
            return;
        }

        SelectedSettingsTabIndex = tabIndex;
    }

    private async Task SelectThemeModeAsync(object? parameter)
    {
        var themeMode = NormalizeThemeMode(parameter?.ToString());
        if (string.Equals(SelectedThemeMode, themeMode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedThemeMode = themeMode;
        await SaveThemeSettingsAsync();
    }

    private async Task SelectCloseWindowBehaviorAsync(object? parameter)
    {
        var closeBehavior = NormalizeCloseWindowBehavior(parameter?.ToString());
        if (string.Equals(SelectedCloseWindowBehavior, closeBehavior, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var preferences = await _appBehaviorPreferencesService.LoadAsync();
        preferences.CloseWindowBehavior = closeBehavior;
        await _appBehaviorPreferencesService.SaveAsync(preferences);
        ApplyBehaviorPreferences(preferences);
    }

    private async Task SetPlayerFullscreenOnPlayAsync(object? parameter)
    {
        var startFullscreen = ParseBooleanParameter(parameter);
        if (StartPlayerFullscreenOnPlay == startFullscreen)
        {
            return;
        }

        var preferences = await _appBehaviorPreferencesService.LoadAsync();
        preferences.StartPlayerFullscreenOnPlay = startFullscreen;
        await _appBehaviorPreferencesService.SaveAsync(preferences);
        ApplyBehaviorPreferences(preferences);
    }

    private async Task SetAutoWebDavScanAsync(object? parameter)
    {
        var autoScan = ParseBooleanParameter(parameter);
        if (AutoScanWebDavOnStartup == autoScan)
        {
            return;
        }

        var preferences = await _appBehaviorPreferencesService.LoadAsync();
        preferences.AutoScanWebDavOnStartup = autoScan;
        await _appBehaviorPreferencesService.SaveAsync(preferences);
        ApplyBehaviorPreferences(preferences);
    }

    private void OnThemeChanged(object? sender, string themeMode)
    {
        UpdateThemePresentation(themeMode);
        ThemeStatusMessage = $"当前主题：{FormatThemeMode(SelectedThemeMode)}";
    }

    private bool IsThemeModeSelected(string themeMode)
    {
        return string.Equals(SelectedThemeMode, themeMode, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeThemeMode(string? themeMode)
    {
        if (string.Equals(themeMode, ThemeSystem, StringComparison.OrdinalIgnoreCase))
        {
            return ThemeSystem;
        }

        return string.Equals(themeMode, ThemeDark, StringComparison.OrdinalIgnoreCase)
            ? ThemeDark
            : ThemeLight;
    }

    private static string NormalizeCloseWindowBehavior(string? closeWindowBehavior)
    {
        return string.Equals(closeWindowBehavior, CloseWindowBehaviorTray, StringComparison.OrdinalIgnoreCase)
            ? CloseWindowBehaviorTray
            : CloseWindowBehaviorExit;
    }

    private static bool ParseBooleanParameter(object? parameter)
    {
        if (parameter is bool value)
        {
            return value;
        }

        return bool.TryParse(parameter?.ToString(), out var parsed) && parsed;
    }

    private static string FormatThemeMode(string themeMode)
    {
        return NormalizeThemeMode(themeMode) switch
        {
            ThemeSystem => "跟随系统",
            ThemeDark => "深色",
            _ => "浅色"
        };
    }

    private void UpdateThemePresentation(string? themeMode)
    {
        if (string.Equals(themeMode, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            ThemeToggleIcon = "moon-stars";
            ThemeToggleToolTip = "当前深色主题，切换到浅色主题";
            return;
        }

        ThemeToggleIcon = "sun";
        ThemeToggleToolTip = "当前浅色主题，切换到深色主题";
    }

    private async Task LoadScanPathsAsync(int sourceConnectionId, CancellationToken cancellationToken)
    {
        var scanPaths = await _settingsService.GetScanPathsAsync(sourceConnectionId, cancellationToken);
        ScanPaths.Clear();

        foreach (var scanPath in scanPaths)
        {
            ScanPaths.Add(scanPath);
        }

        ScanPathStatusMessage = ScanPaths.Count == 0
            ? "当前还没有扫描路径。"
            : $"已加载 {ScanPaths.Count} 条扫描路径。";
    }
}
