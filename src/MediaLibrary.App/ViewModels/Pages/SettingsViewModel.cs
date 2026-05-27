using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using MediaLibrary.App.Models.Caches;
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
    private readonly ISoftwareCacheManagementService _softwareCacheManagementService;
    private readonly IConfirmationDialogService _confirmationDialogService;
    private readonly IOpenSubtitlesClientService _openSubtitlesClientService;
    private readonly HttpClient _metadataApiHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };
    private const long BytesPerMb = 1024L * 1024L;
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
    private string _openSubtitlesEndpoint = "https://api.opensubtitles.com/api/v1";
    private string _openSubtitlesApiKey = string.Empty;
    private string _openSubtitlesUsername = string.Empty;
    private string _openSubtitlesPassword = string.Empty;
    private string _openSubtitlesToken = string.Empty;
    private string _loadedOpenSubtitlesEndpoint = "https://api.opensubtitles.com/api/v1";
    private string _loadedOpenSubtitlesApiKey = string.Empty;
    private string _loadedOpenSubtitlesUsername = string.Empty;
    private string _loadedOpenSubtitlesPassword = string.Empty;
    private bool _isOpenSubtitlesEnabled;
    private OpenSubtitlesLanguageOption? _selectedOpenSubtitlesLanguage;
    private string _aiBaseUrl = string.Empty;
    private string _aiApiKey = string.Empty;
    private string _aiModel = string.Empty;
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
    private string _selectedThemeMode = "Light";
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
    private bool _isSubtitleCacheClearAvailable;
    private string _aboutStatusMessage = "XFVerse 影音管理系统";
    private int? _editingScanPathId;
    private string _editingScanPathValue = string.Empty;
    private string _editingScanPathDisplayName = string.Empty;
    private bool _editingScanPathEnabled = true;
    private bool _editingScanPathRecursive = true;

    public SettingsViewModel(
        ISettingsService settingsService,
        IWebDavService webDavService,
        IThemeService themeService,
        ISoftwareCacheManagementService softwareCacheManagementService,
        IConfirmationDialogService confirmationDialogService,
        IOpenSubtitlesClientService openSubtitlesClientService)
        : base("设置", "管理通用设置与 API 配置。")
    {
        _settingsService = settingsService;
        _webDavService = webDavService;
        _themeService = themeService;
        _softwareCacheManagementService = softwareCacheManagementService;
        _confirmationDialogService = confirmationDialogService;
        _openSubtitlesClientService = openSubtitlesClientService;

        ThemeModes = _themeService.ThemeModes;
        OpenSubtitlesLanguages = _openSubtitlesClientService.SupportedLanguages;
        _selectedOpenSubtitlesLanguage = OpenSubtitlesLanguages.FirstOrDefault(x => x.Code == "zh-cn")
                                         ?? OpenSubtitlesLanguages.FirstOrDefault();
        SaveConnectionCommand = new AsyncRelayCommand(SaveConnectionAsync);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
        SaveTmdbSettingsCommand = new AsyncRelayCommand(SaveTmdbSettingsAsync);
        TestTmdbConnectionCommand = new AsyncRelayCommand(TestTmdbConnectionAsync);
        SaveOmdbSettingsCommand = new AsyncRelayCommand(SaveOmdbSettingsAsync);
        TestOmdbConnectionCommand = new AsyncRelayCommand(TestOmdbConnectionAsync);
        SaveOpenSubtitlesSettingsCommand = new AsyncRelayCommand(SaveOpenSubtitlesSettingsAsync);
        TestOpenSubtitlesConnectionCommand = new AsyncRelayCommand(TestOpenSubtitlesConnectionAsync);
        SaveAiSettingsCommand = new AsyncRelayCommand(SaveAiSettingsAsync);
        SaveThemeSettingsCommand = new AsyncRelayCommand(SaveThemeSettingsAsync);
        BeginAddScanPathCommand = new RelayCommand(BeginAddScanPath);
        SaveScanPathCommand = new AsyncRelayCommand(SaveScanPathAsync);
        EditScanPathCommand = new RelayCommand(EditScanPath);
        DeleteScanPathCommand = new AsyncRelayCommand(DeleteScanPathAsync);
        ToggleScanPathCommand = new AsyncRelayCommand(ToggleScanPathAsync);
        CancelEditScanPathCommand = new RelayCommand(CancelEditScanPath);
        SavePosterCacheLimitCommand = new AsyncRelayCommand(SavePosterCacheLimitAsync);
        ClearPosterCacheCommand = new AsyncRelayCommand(ClearPosterCacheAsync);
        ClearOtherCacheCommand = new AsyncRelayCommand(ClearOtherCacheAsync, () => IsOtherCacheClearAvailable);
        ClearSubtitleCacheCommand = new AsyncRelayCommand(ClearSubtitleCacheAsync, () => IsSubtitleCacheClearAvailable);
        RefreshSoftwareCacheCommand = new AsyncRelayCommand(RefreshSoftwareCacheAsync);
        ToggleAboutDetailsCommand = new RelayCommand(ToggleAboutDetails);
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

    public AsyncRelayCommand SaveThemeSettingsCommand { get; }

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

    public string TmdbReadAccessToken { get => _tmdbReadAccessToken; set => SetProperty(ref _tmdbReadAccessToken, value); }

    public string TmdbApiKey { get => _tmdbApiKey; set => SetProperty(ref _tmdbApiKey, value); }

    public string OmdbApiKey { get => _omdbApiKey; set => SetProperty(ref _omdbApiKey, value); }

    public string OpenSubtitlesEndpoint { get => _openSubtitlesEndpoint; set => SetProperty(ref _openSubtitlesEndpoint, value); }

    public string OpenSubtitlesApiKey { get => _openSubtitlesApiKey; set => SetProperty(ref _openSubtitlesApiKey, value); }

    public string OpenSubtitlesUsername { get => _openSubtitlesUsername; set => SetProperty(ref _openSubtitlesUsername, value); }

    public string OpenSubtitlesPassword { get => _openSubtitlesPassword; set => SetProperty(ref _openSubtitlesPassword, value); }

    public bool IsOpenSubtitlesEnabled { get => _isOpenSubtitlesEnabled; set => SetProperty(ref _isOpenSubtitlesEnabled, value); }

    public OpenSubtitlesLanguageOption? SelectedOpenSubtitlesLanguage
    {
        get => _selectedOpenSubtitlesLanguage;
        set => SetProperty(ref _selectedOpenSubtitlesLanguage, value);
    }

    public string AiBaseUrl { get => _aiBaseUrl; set => SetProperty(ref _aiBaseUrl, value); }

    public string AiApiKey { get => _aiApiKey; set => SetProperty(ref _aiApiKey, value); }

    public string AiModel { get => _aiModel; set => SetProperty(ref _aiModel, value); }

    public string AiDetailCorrectionModel { get => _aiDetailCorrectionModel; set => SetProperty(ref _aiDetailCorrectionModel, value); }

    public string AiDetailCorrectionTimeoutSeconds { get => _aiDetailCorrectionTimeoutSeconds; set => SetProperty(ref _aiDetailCorrectionTimeoutSeconds, value); }

    public string AiBatchCorrectionModel { get => _aiBatchCorrectionModel; set => SetProperty(ref _aiBatchCorrectionModel, value); }

    public string AiBatchCorrectionTimeoutSeconds { get => _aiBatchCorrectionTimeoutSeconds; set => SetProperty(ref _aiBatchCorrectionTimeoutSeconds, value); }

    public string AiScanTvUncertainRangeModel { get => _aiScanTvUncertainRangeModel; set => SetProperty(ref _aiScanTvUncertainRangeModel, value); }

    public string AiScanTvUncertainRangeTimeoutSeconds { get => _aiScanTvUncertainRangeTimeoutSeconds; set => SetProperty(ref _aiScanTvUncertainRangeTimeoutSeconds, value); }

    public string AiScanTvFullRangeModel { get => _aiScanTvFullRangeModel; set => SetProperty(ref _aiScanTvFullRangeModel, value); }

    public string AiScanTvFullRangeTimeoutSeconds { get => _aiScanTvFullRangeTimeoutSeconds; set => SetProperty(ref _aiScanTvFullRangeTimeoutSeconds, value); }

    public string AiScanMovieTaggingModel { get => _aiScanMovieTaggingModel; set => SetProperty(ref _aiScanMovieTaggingModel, value); }

    public string AiScanMovieTaggingTimeoutSeconds { get => _aiScanMovieTaggingTimeoutSeconds; set => SetProperty(ref _aiScanMovieTaggingTimeoutSeconds, value); }

    public string AiRecommendationModel { get => _aiRecommendationModel; set => SetProperty(ref _aiRecommendationModel, value); }

    public string AiRecommendationTimeoutSeconds { get => _aiRecommendationTimeoutSeconds; set => SetProperty(ref _aiRecommendationTimeoutSeconds, value); }

    public string AiWatchProfileModel { get => _aiWatchProfileModel; set => SetProperty(ref _aiWatchProfileModel, value); }

    public string AiWatchProfileTimeoutSeconds { get => _aiWatchProfileTimeoutSeconds; set => SetProperty(ref _aiWatchProfileTimeoutSeconds, value); }

    public string SelectedThemeMode { get => _selectedThemeMode; set => SetProperty(ref _selectedThemeMode, value); }

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

            await appSettingTask;
            ApplyApplicationSetting(appSettingTask.Result);

            await LoadSoftwareCacheAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            ConnectionStatusMessage = $"加载设置失败：{exception.Message}";
            TmdbStatusMessage = "TMDB 配置尚未加载。";
            OmdbStatusMessage = "OMDb 配置尚未加载。";
            OpenSubtitlesStatusMessage = "OpenSubtitles 配置尚未加载。";
            ApiStatusMessage = "大模型配置尚未加载。";
            SoftwareCacheStatusMessage = "软件缓存状态尚未加载。";
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
        _loadedOpenSubtitlesEndpoint = OpenSubtitlesEndpoint;
        _loadedOpenSubtitlesApiKey = OpenSubtitlesApiKey;
        _loadedOpenSubtitlesUsername = OpenSubtitlesUsername;
        _loadedOpenSubtitlesPassword = OpenSubtitlesPassword;
        IsOpenSubtitlesEnabled = applicationSetting.IsOpenSubtitlesEnabled;
        SelectedOpenSubtitlesLanguage = FindOpenSubtitlesLanguage(applicationSetting.OpenSubtitlesDefaultLanguageCode);
        AiBaseUrl = applicationSetting.AiBaseUrl;
        AiApiKey = applicationSetting.AiApiKey;
        ApplyAiRouting(applicationSetting.AiRouting ?? AiModelRoutingSettings.FromStoredValue(applicationSetting.AiModel));
        SelectedThemeMode = string.IsNullOrWhiteSpace(applicationSetting.ThemeMode) ? "Light" : applicationSetting.ThemeMode;
        TmdbStatusMessage = "已加载 TMDB 配置。";
        OmdbStatusMessage = "已加载 OMDb 配置。";
        OpenSubtitlesStatusMessage = "已加载 OpenSubtitles 配置。";
        ApiStatusMessage = "已加载大模型配置。";
        ThemeStatusMessage = $"当前主题：{SelectedThemeMode}";
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
            DefaultLanguageCode = SelectedOpenSubtitlesLanguage?.Code ?? "zh-cn"
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
        return !string.Equals(NormalizeOpenSubtitlesText(OpenSubtitlesEndpoint), NormalizeOpenSubtitlesText(_loadedOpenSubtitlesEndpoint), StringComparison.Ordinal)
               || !string.Equals(NormalizeOpenSubtitlesText(OpenSubtitlesApiKey), NormalizeOpenSubtitlesText(_loadedOpenSubtitlesApiKey), StringComparison.Ordinal)
               || !string.Equals(NormalizeOpenSubtitlesText(OpenSubtitlesUsername), NormalizeOpenSubtitlesText(_loadedOpenSubtitlesUsername), StringComparison.Ordinal)
               || !string.Equals(OpenSubtitlesPassword ?? string.Empty, _loadedOpenSubtitlesPassword ?? string.Empty, StringComparison.Ordinal);
    }

    private void MarkOpenSubtitlesInputsAsPersisted()
    {
        _loadedOpenSubtitlesEndpoint = OpenSubtitlesEndpoint;
        _loadedOpenSubtitlesApiKey = OpenSubtitlesApiKey;
        _loadedOpenSubtitlesUsername = OpenSubtitlesUsername;
        _loadedOpenSubtitlesPassword = OpenSubtitlesPassword;
    }

    private static string NormalizeOpenSubtitlesText(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static bool IsOpenSubtitlesAuthFailure(OpenSubtitlesErrorKind errorKind)
    {
        return errorKind is OpenSubtitlesErrorKind.Unauthorized or OpenSubtitlesErrorKind.Forbidden;
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
        settings.OpenSubtitlesDefaultLanguageCode = SelectedOpenSubtitlesLanguage?.Code ?? "zh-cn";
        settings.IsOpenSubtitlesEnabled = IsOpenSubtitlesEnabled;
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
            TmdbStatusMessage = "TMDB 认证信息已保存。";
        }
        catch (Exception exception)
        {
            TmdbStatusMessage = $"保存 TMDB 配置失败：{exception.Message}";
        }
    }

    private async Task TestTmdbConnectionAsync()
    {
        var hasTmdbCredential = !string.IsNullOrWhiteSpace(TmdbReadAccessToken)
                                || !string.IsNullOrWhiteSpace(TmdbApiKey);
        if (!hasTmdbCredential)
        {
            TmdbStatusMessage = "请先填写 TMDB Read Access Token 或 API Key。";
            return;
        }

        try
        {
            TmdbStatusMessage = "正在测试 TMDB 连接...";
            var settings = await _settingsService.GetApplicationSettingAsync();
            await TestTmdbAsync(settings.TmdbBaseUrl);
            TmdbStatusMessage = "TMDB 连接正常。";
        }
        catch (Exception exception)
        {
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
            OmdbStatusMessage = "OMDb 认证信息已保存。";
        }
        catch (Exception exception)
        {
            OmdbStatusMessage = $"保存 OMDb 配置失败：{exception.Message}";
        }
    }

    private async Task TestOmdbConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(OmdbApiKey))
        {
            OmdbStatusMessage = "请先填写 OMDb API Key。";
            return;
        }

        try
        {
            OmdbStatusMessage = "正在测试 OMDb 连接...";
            await TestOmdbAsync();
            OmdbStatusMessage = "OMDb 连接正常。";
        }
        catch (Exception exception)
        {
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
            MarkOpenSubtitlesInputsAsPersisted();
            OpenSubtitlesStatusMessage = "OpenSubtitles 配置已保存。";
        }
        catch (Exception exception)
        {
            OpenSubtitlesStatusMessage = $"保存 OpenSubtitles 配置失败：{exception.Message}";
        }
    }

    private async Task TestOpenSubtitlesConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(OpenSubtitlesApiKey))
        {
            OpenSubtitlesStatusMessage = "请先填写 OpenSubtitles API Key。";
            return;
        }

        try
        {
            OpenSubtitlesStatusMessage = "正在探测 OpenSubtitles API 能力...";
            ClearOpenSubtitlesTokenIfCredentialsChanged();
            var result = await _openSubtitlesClientService.ProbeAsync(BuildOpenSubtitlesOptionsFromInputs());
            if (!string.IsNullOrWhiteSpace(result.Token))
            {
                _openSubtitlesToken = result.Token;
                await SaveApplicationSettingsAsync(settings =>
                {
                    ApplyOpenSubtitlesInputs(settings);
                });
                MarkOpenSubtitlesInputsAsPersisted();
            }
            else if (IsOpenSubtitlesAuthFailure(result.ErrorKind) && !string.IsNullOrWhiteSpace(_openSubtitlesToken))
            {
                _openSubtitlesToken = string.Empty;
                await SaveApplicationSettingsAsync(settings =>
                {
                    ApplyOpenSubtitlesInputs(settings);
                });
                MarkOpenSubtitlesInputsAsPersisted();
            }

            OpenSubtitlesStatusMessage = FormatOpenSubtitlesProbeResult(result);
        }
        catch (Exception exception)
        {
            OpenSubtitlesStatusMessage = $"OpenSubtitles 探测失败：{exception.GetType().Name}";
        }
    }

    private async Task SaveAiSettingsAsync()
    {
        await SaveApplicationSettingsAsync();
        ApiStatusMessage = "AI 接口配置已保存。";
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
        ThemeStatusMessage = $"主题已切换为：{SelectedThemeMode}";
    }

    private async Task SaveApplicationSettingsAsync()
    {
        try
        {
            await SaveApplicationSettingsAsync(settings =>
            {
                settings.Id = _applicationSettingId;
                settings.TmdbReadAccessToken = TmdbReadAccessToken;
                settings.TmdbApiKey = TmdbApiKey;
                settings.OmdbApiKey = OmdbApiKey;
                settings.ThemeMode = SelectedThemeMode;
                settings.AiBaseUrl = AiBaseUrl;
                settings.AiApiKey = AiApiKey;
                settings.AiModel = AiModel;
                settings.AiRouting = BuildAiRoutingFromInputs();
            });
        }
        catch (Exception exception)
        {
            ApiStatusMessage = $"保存接口配置失败：{exception.Message}";
        }
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
            IsOpenSubtitlesEnabled = settings.IsOpenSubtitlesEnabled
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
        AboutStatusMessage = "关于详情将在后续最终 UI 阶段完善。";
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
