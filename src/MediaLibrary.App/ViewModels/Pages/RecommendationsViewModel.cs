using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.Services.Implementations;
using MediaLibrary.App.Services.Interfaces;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class RecommendationsViewModel : PageViewModelBase
{
    private const string RecommendationStatusEmpty = "Empty";
    private const string RecommendationStatusError = "Error";
    private const string RecommendationStatusMissingSeed = "MissingSeed";
    private const int CandidatePoolLowWatermark = 9;
    private const string CandidatePoolRefillWaitingMessage = "正在补充推荐候选，请稍候";
    private const string CandidatePoolRefillFailedMessage = "候选补充失败，请稍后重试";
    private const string CandidatePoolRefillNoCandidatesMessage = "本次没有补充到新的候选影片，请稍后重试";
    private static readonly string AiPoolDiagnosticLogPath = DiagnosticLogPathResolver.Resolve("ai-pool-debug.log");
    private static readonly object AiPoolDiagnosticFileLock = new();
    private const string RefreshBatchText = "换一批";
    private const string RetryRecommendationText = "重试";
    private const string RecommendationIncompleteMessage = "AI 推荐本次请求未完成，请稍后重试";
    private const string RecommendationIncompletePreservedMessage = "AI 推荐本次更新未完成，已保留当前结果";
    private const string MissingRecommendationSeedMessage = "先标记几部影片，让 AI 更懂你";
    private const string EmptyRecommendationMessage = "当前筛选条件下暂无可推荐影片";
    private const string PlaybackSourceAll = "全部";
    private const string PlaybackSourceWithSource = "有播放源";
    private const string PlaybackSourceWithoutSource = "无播放源";
    private const string WatchFilterAll = "全部";
    private const string WatchFilterWatched = "已看";
    private const string WatchFilterUnwatched = "未看";
    private readonly IRecommendationService _recommendationService;
    private readonly IUserCollectionService _userCollectionService;
    private readonly INavigationStateService _navigationStateService;
    private readonly IDataRefreshService _dataRefreshService;
    private readonly IRecommendationPreferenceService _recommendationPreferenceService;
    private int _batchSeed;
    private string _selectedLibraryScope = PlaybackSourceAll;
    private string _selectedWatchFilter = WatchFilterUnwatched;
    private string _statusMessage = "为你量身“定制”下一部影片";
    private string _refreshBatchButtonText = RefreshBatchText;
    private bool _isLoading;
    private bool _isRecommendationError;
    private bool _canRequestRecommendations = true;
    private int _loadVersion;
    private string? _displayedCombinationKey;
    private CancellationTokenSource? _loadCancellation;
    private bool _suppressNextRecommendationChangedReload;
    private bool _isWaitingForCandidatePoolRefill;
    private bool _isCustomPreferenceEnabled;
    private bool _isPreferenceDialogOpen;
    private bool _isApplyingPreferenceState;
    private bool _isActive;
    private string _savedCustomPreferenceText = string.Empty;
    private string _originalCustomPreferenceText = string.Empty;
    private string _draftCustomPreferenceText = string.Empty;

    public RecommendationsViewModel(
        IRecommendationService recommendationService,
        IUserCollectionService userCollectionService,
        INavigationStateService navigationStateService,
        IDataRefreshService dataRefreshService,
        IRecommendationPreferenceService recommendationPreferenceService)
        : base("AI 推荐", "基于已看记录生成 3 部推荐，并按播放源和观看状态筛选。")
    {
        _recommendationService = recommendationService;
        _userCollectionService = userCollectionService;
        _navigationStateService = navigationStateService;
        _dataRefreshService = dataRefreshService;
        _recommendationPreferenceService = recommendationPreferenceService;
        Recommendations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowEmptyLoadingSpinner));
        _dataRefreshService.DataChanged += OnDataChanged;

        OpenMovieCommand = new RelayCommand(OpenMovie);
        AddWantToWatchCommand = new AsyncRelayCommand(AddWantToWatchAsync, CanToggleWantToWatch);
        ToggleNotInterestedCommand = new AsyncRelayCommand(ToggleNotInterestedAsync, CanToggleNotInterested);
        SelectLibraryScopeCommand = new RelayCommand(value => SelectedLibraryScope = GetPlaybackSourceOptionValue(value));
        SelectWatchFilterCommand = new RelayCommand(value => SelectedWatchFilter = GetWatchFilterOptionValue(value));
        SetCustomPreferenceEnabledCommand = new RelayCommand(SetCustomPreferenceEnabled, _ => CanEditCustomPreference);
        EditPreferenceCommand = new AsyncRelayCommand(OpenPreferenceDialogAsync, () => CanEditCustomPreference);
        ConfirmPreferenceCommand = new AsyncRelayCommand(ConfirmPreferenceAsync, () => CanConfirmPreference);
        CancelPreferenceCommand = new RelayCommand(CancelPreferenceDialog);
        ClearPreferenceCommand = new AsyncRelayCommand(ClearPreferenceAsync, () => CanClearPreference);
        RefreshBatchCommand = new AsyncRelayCommand(
            RefreshBatchAsync,
            () => !IsLoading && CanRequestRecommendations && !_isWaitingForCandidatePoolRefill && !IsPreferenceDialogOpen);
    }

    public ObservableCollection<AiRecommendationItem> Recommendations { get; } = [];

    public IReadOnlyList<string> LibraryScopeOptions { get; } =
    [
        PlaybackSourceAll,
        PlaybackSourceWithSource,
        PlaybackSourceWithoutSource
    ];

    public IReadOnlyList<string> WatchFilterOptions { get; } =
    [
        WatchFilterAll,
        WatchFilterWatched,
        WatchFilterUnwatched
    ];

    public RelayCommand OpenMovieCommand { get; }

    public AsyncRelayCommand AddWantToWatchCommand { get; }

    public AsyncRelayCommand ToggleNotInterestedCommand { get; }

    public RelayCommand SelectLibraryScopeCommand { get; }

    public RelayCommand SelectWatchFilterCommand { get; }

    public RelayCommand SetCustomPreferenceEnabledCommand { get; }

    public AsyncRelayCommand EditPreferenceCommand { get; }

    public AsyncRelayCommand ConfirmPreferenceCommand { get; }

    public RelayCommand CancelPreferenceCommand { get; }

    public AsyncRelayCommand ClearPreferenceCommand { get; }

    public AsyncRelayCommand RefreshBatchCommand { get; }

    public string SelectedLibraryScope
    {
        get => _selectedLibraryScope;
        set
        {
            if (SetProperty(ref _selectedLibraryScope, value))
            {
                _batchSeed = 0;
                OnPropertyChanged(nameof(PlaybackSourceButtonText));
                _ = RequestReloadAsync(forceRefresh: false);
            }
        }
    }

    public string SelectedWatchFilter
    {
        get => _selectedWatchFilter;
        set
        {
            if (SetProperty(ref _selectedWatchFilter, value))
            {
                _batchSeed = 0;
                OnPropertyChanged(nameof(WatchStatusButtonText));
                _ = RequestReloadAsync(forceRefresh: false);
            }
        }
    }

    public string PlaybackSourceButtonText => $"播放源：{SelectedLibraryScope}";

    public string WatchStatusButtonText => $"观看状态：{SelectedWatchFilter}";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, NormalizeStatusMessage(value));
    }

    public string RefreshBatchButtonText
    {
        get => _refreshBatchButtonText;
        private set => SetProperty(ref _refreshBatchButtonText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshBatchCommand.RaiseCanExecuteChanged();
                ToggleNotInterestedCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanChangeFilters));
                OnPropertyChanged(nameof(ShowEmptyLoadingSpinner));
                OnCustomPreferenceBusyStateChanged();
            }
        }
    }

    public bool ShowEmptyLoadingSpinner => IsLoading && Recommendations.Count == 0;

    public bool CanChangeFilters => !IsLoading
                                   && !_isRecommendationError
                                   && !_isWaitingForCandidatePoolRefill
                                   && !IsPreferenceDialogOpen;

    public bool CanEditCustomPreference => !IsLoading && !_isWaitingForCandidatePoolRefill;

    public int CustomPreferenceMaxLength => RecommendationPreferenceModel.MaxTextLength;

    public bool IsCustomPreferenceEnabled
    {
        get => _isCustomPreferenceEnabled;
        set
        {
            var previousValue = _isCustomPreferenceEnabled;
            if (SetProperty(ref _isCustomPreferenceEnabled, value))
            {
                OnPropertyChanged(nameof(CustomPreferenceToggleText));
                OnPropertyChanged(nameof(CustomPreferenceSwitchText));
                OnPropertyChanged(nameof(IsCustomPreferenceDisabled));
                OnPropertyChanged(nameof(CanClearPreference));
                ClearPreferenceCommand.RaiseCanExecuteChanged();
                if (!_isApplyingPreferenceState)
                {
                    _ = SaveCustomPreferenceEnabledAsync(value, previousValue);
                }
            }
        }
    }

    public string CustomPreferenceToggleText => IsCustomPreferenceEnabled
        ? "自定义偏好已启用"
        : "自定义偏好未启用";

    public string CustomPreferenceSwitchText => IsCustomPreferenceEnabled ? "开" : "关";

    public bool IsCustomPreferenceDisabled => !IsCustomPreferenceEnabled;

    public bool IsPreferenceDialogOpen
    {
        get => _isPreferenceDialogOpen;
        private set
        {
            if (SetProperty(ref _isPreferenceDialogOpen, value))
            {
                RefreshBatchCommand.RaiseCanExecuteChanged();
                SetCustomPreferenceEnabledCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanChangeFilters));
            }
        }
    }

    public string DraftCustomPreferenceText
    {
        get => _draftCustomPreferenceText;
        set
        {
            if (SetProperty(ref _draftCustomPreferenceText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(DraftCustomPreferenceLength));
                OnPropertyChanged(nameof(DraftCustomPreferenceCountText));
                OnPropertyChanged(nameof(IsDraftPreferenceTooLong));
                OnPropertyChanged(nameof(HasPreferenceDraftChanged));
                OnPropertyChanged(nameof(CanConfirmPreference));
                ConfirmPreferenceCommand.RaiseCanExecuteChanged();
                ClearPreferenceCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int DraftCustomPreferenceLength => DraftCustomPreferenceText.Length;

    public string DraftCustomPreferenceCountText => $"{DraftCustomPreferenceLength} / {CustomPreferenceMaxLength}";

    public bool IsDraftPreferenceTooLong => DraftCustomPreferenceLength > CustomPreferenceMaxLength;

    public bool HasPreferenceDraftChanged => !string.Equals(
        NormalizePreferenceText(DraftCustomPreferenceText),
        NormalizePreferenceText(_originalCustomPreferenceText),
        StringComparison.Ordinal);

    public bool CanConfirmPreference => !IsDraftPreferenceTooLong
                                        && CanEditCustomPreference
                                        && HasPreferenceDraftChanged;

    public bool CanClearPreference => CanEditCustomPreference
                                      && (!string.IsNullOrEmpty(NormalizePreferenceText(_savedCustomPreferenceText))
                                          || IsCustomPreferenceEnabled);

    public bool CanRequestRecommendations
    {
        get => _canRequestRecommendations;
        private set
        {
            if (SetProperty(ref _canRequestRecommendations, value))
            {
                RefreshBatchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private void SetWaitingForCandidatePoolRefill(bool isWaiting)
    {
        if (_isWaitingForCandidatePoolRefill == isWaiting)
        {
            return;
        }

        _isWaitingForCandidatePoolRefill = isWaiting;
        RefreshBatchCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanChangeFilters));
        OnCustomPreferenceBusyStateChanged();
    }

    private void OnCustomPreferenceBusyStateChanged()
    {
        SetCustomPreferenceEnabledCommand.RaiseCanExecuteChanged();
        EditPreferenceCommand.RaiseCanExecuteChanged();
        ConfirmPreferenceCommand.RaiseCanExecuteChanged();
        ClearPreferenceCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditCustomPreference));
        OnPropertyChanged(nameof(CanConfirmPreference));
        OnPropertyChanged(nameof(CanClearPreference));
    }

    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        _isActive = true;
        await LoadCustomPreferenceAsync(cancellationToken);
        await RequestReloadAsync(forceRefresh: false, cancellationToken);
    }

    public override void Deactivate()
    {
        _isActive = false;
        ClosePreferenceDialog();
    }

    private void OnDataChanged(object? sender, AppDataChangedEventArgs e)
    {
        if (!_isActive)
        {
            return;
        }

        if (e.Reason == AppDataChangeReason.RecommendationChanged)
        {
            if (_suppressNextRecommendationChangedReload)
            {
                _suppressNextRecommendationChangedReload = false;
                return;
            }

            if (_isRecommendationError)
            {
                var errorRefreshVersion = _loadVersion;
                _ = Application.Current.Dispatcher.InvokeAsync(() => _ = RefreshPreviewAfterErrorDataChangeAsync(errorRefreshVersion));
                return;
            }

            _ = Application.Current.Dispatcher.InvokeAsync(() => _ = RequestReloadAsync(forceRefresh: false));
            return;
        }

        if (e.Reason is not (AppDataChangeReason.CollectionChanged or AppDataChangeReason.MetadataChanged))
        {
            return;
        }

        var refreshVersion = _loadVersion;
        _ = Application.Current.Dispatcher.InvokeAsync(() => _ = RefreshRecommendationItemStatesAsync(refreshVersion));
    }

    private async Task LoadCustomPreferenceAsync(CancellationToken cancellationToken = default)
    {
        var preference = await _recommendationPreferenceService.GetAsync(cancellationToken);
        ApplyCustomPreference(preference);
    }

    private static string NormalizeStatusMessage(string value)
    {
        var normalized = value.Trim();
        while (normalized.Length > 0 && IsTrimmedStatusEnding(normalized[^1]))
        {
            normalized = normalized[..^1].TrimEnd();
        }

        return normalized;
    }

    private static bool IsTrimmedStatusEnding(char value)
    {
        return value is '。' or '.' or '…';
    }

    private async Task SaveCustomPreferenceEnabledAsync(bool isEnabled, bool previousValue)
    {
        try
        {
            var saved = await _recommendationPreferenceService.SaveAsync(
                new RecommendationPreferenceModel
                {
                    IsEnabled = isEnabled,
                    Text = _savedCustomPreferenceText
                });
            ApplyCustomPreference(saved);
            StatusMessage = isEnabled
                ? "自定义推荐偏好已开启，下一次推荐将生效"
                : "自定义推荐偏好已关闭，下一次推荐将生效";
        }
        catch (Exception exception)
        {
            _isApplyingPreferenceState = true;
            try
            {
                IsCustomPreferenceEnabled = previousValue;
            }
            finally
            {
                _isApplyingPreferenceState = false;
            }

            StatusMessage = $"自定义推荐偏好已保存失败：{exception.Message}";
        }
    }

    private async Task OpenPreferenceDialogAsync()
    {
        try
        {
            var preference = await _recommendationPreferenceService.GetAsync();
            ApplyCustomPreference(preference);
            _originalCustomPreferenceText = preference.Text;
            DraftCustomPreferenceText = preference.Text;
            OnPreferenceDraftBaselineChanged();
            IsPreferenceDialogOpen = true;
        }
        catch (Exception exception)
        {
            StatusMessage = $"自定义推荐偏好已加载失败：{exception.Message}";
        }
    }

    private void SetCustomPreferenceEnabled(object? value)
    {
        if (!CanEditCustomPreference)
        {
            return;
        }

        IsCustomPreferenceEnabled = bool.TryParse(value?.ToString(), out var isEnabled) && isEnabled;
    }

    private async Task ConfirmPreferenceAsync()
    {
        if (!CanConfirmPreference)
        {
            StatusMessage = IsDraftPreferenceTooLong
                ? $"自定义推荐偏好不能超过 {CustomPreferenceMaxLength} 个字符"
                : "自定义推荐偏好没有变化";
            return;
        }

        try
        {
            var saved = await _recommendationPreferenceService.SaveAsync(
                new RecommendationPreferenceModel
                {
                    IsEnabled = IsCustomPreferenceEnabled,
                    Text = DraftCustomPreferenceText
                });
            ApplyCustomPreference(saved);
            _originalCustomPreferenceText = saved.Text;
            IsPreferenceDialogOpen = false;
            StatusMessage = "推荐偏好已保存，下一次推荐将生效";
        }
        catch (Exception exception)
        {
            StatusMessage = $"自定义推荐偏好已保存失败：{exception.Message}";
        }
    }

    private void CancelPreferenceDialog()
    {
        DraftCustomPreferenceText = _savedCustomPreferenceText;
        _originalCustomPreferenceText = _savedCustomPreferenceText;
        IsPreferenceDialogOpen = false;
    }

    public void ClosePreferenceDialog()
    {
        if (IsPreferenceDialogOpen)
        {
            CancelPreferenceDialog();
        }
    }

    private async Task ClearPreferenceAsync()
    {
        if (!CanClearPreference)
        {
            DraftCustomPreferenceText = string.Empty;
            IsPreferenceDialogOpen = false;
            return;
        }

        try
        {
            var saved = await _recommendationPreferenceService.ClearAsync();
            ApplyCustomPreference(saved);
            _originalCustomPreferenceText = string.Empty;
            DraftCustomPreferenceText = string.Empty;
            IsPreferenceDialogOpen = false;
            StatusMessage = "推荐偏好已清空，下一次推荐将生效";
        }
        catch (Exception exception)
        {
            StatusMessage = $"自定义推荐偏好清空失败：{exception.Message}";
        }
    }

    private void ApplyCustomPreference(RecommendationPreferenceModel preference)
    {
        _savedCustomPreferenceText = preference.Text;
        _isApplyingPreferenceState = true;
        try
        {
            IsCustomPreferenceEnabled = preference.IsEnabled;
        }
        finally
        {
            _isApplyingPreferenceState = false;
        }
    }

    private void OnPreferenceDraftBaselineChanged()
    {
        OnPropertyChanged(nameof(HasPreferenceDraftChanged));
        OnPropertyChanged(nameof(CanConfirmPreference));
        OnPropertyChanged(nameof(CanClearPreference));
        ConfirmPreferenceCommand.RaiseCanExecuteChanged();
        ClearPreferenceCommand.RaiseCanExecuteChanged();
    }

    private string NormalizePreferenceText(string? text)
    {
        return _recommendationPreferenceService.NormalizeText(text);
    }

    private static string GetPlaybackSourceOptionValue(object? value)
    {
        var text = value?.ToString();
        return text switch
        {
            PlaybackSourceWithSource => PlaybackSourceWithSource,
            PlaybackSourceWithoutSource => PlaybackSourceWithoutSource,
            _ => PlaybackSourceAll
        };
    }

    private static string GetWatchFilterOptionValue(object? value)
    {
        var text = value?.ToString();
        return text switch
        {
            WatchFilterWatched => WatchFilterWatched,
            WatchFilterUnwatched => WatchFilterUnwatched,
            _ => WatchFilterAll
        };
    }

    private async Task RefreshBatchAsync()
    {
        if (_isWaitingForCandidatePoolRefill)
        {
            StatusMessage = CandidatePoolRefillWaitingMessage;
            return;
        }

        if (!CanRequestRecommendations)
        {
            return;
        }

        _batchSeed++;
        var completed = await RequestReloadAsync(forceRefresh: true);
        if (completed && CanRequestRecommendations)
        {
            NotifyRecommendationChangedWithoutReload();
        }
    }

    private Task<bool> RequestReloadAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        return LoadRecommendationsAsync(cancellationToken, forceRefresh);
    }

    private async Task<bool> LoadRecommendationsAsync(CancellationToken cancellationToken = default, bool forceRefresh = false)
    {
        _loadCancellation?.Cancel();
        var requestVersion = unchecked(_loadVersion + 1);
        _loadVersion = requestVersion;
        using var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = loadCancellation;
        var activeCancellationToken = loadCancellation.Token;
        var queryOptions = new RecommendationQueryOptions
        {
            WatchFilter = GetWatchFilter(),
            LibraryScope = GetLibraryScope(),
            BatchSeed = _batchSeed,
            Take = 3,
            ForceRefresh = forceRefresh
        };
        var targetCombinationKey = BuildRecommendationCombinationKey(queryOptions);
        var isDifferentDisplayedCombination = IsDifferentDisplayedCombination(targetCombinationKey);
        using var perfScope = AiPerfDiagnostics.BeginScope("ui-recommendations-load", targetCombinationKey, forceRefresh);
        var perfOutcome = "success";
        var perfPath = forceRefresh ? "foreground-generation" : "preview-only";
        var perfItemCount = 0;
        var perfStatus = string.Empty;
        var perfError = string.Empty;

        bool CompleteUi(string path, bool result, int itemCount = 0, string status = "")
        {
            perfPath = path;
            perfItemCount = itemCount;
            perfStatus = status;
            return result;
        }

        try
        {
            IsLoading = true;

            if (isDifferentDisplayedCombination)
            {
                ApplyRecommendations([], targetCombinationKey);
                SetRetryMode(false);
                StatusMessage = "正在等待 AI 分析并推荐影片";
            }

            var state = await _recommendationService.GetRecommendationPreviewStateAsync(queryOptions, activeCancellationToken);
            if (!IsCurrentLoad(requestVersion))
            {
                return false;
            }

            CanRequestRecommendations = state.CanRequest;
            if (IsMissingSeedState(state))
            {
                SetWaitingForCandidatePoolRefill(false);
                ApplyRecommendations([], targetCombinationKey);
                SetRetryMode(false);
                StatusMessage = BuildRecommendationStatusMessage(state);
                return CompleteUi("preview-only", true, status: state.Status);
            }

            if (IsEmptyState(state))
            {
                SetWaitingForCandidatePoolRefill(false);
                ApplyRecommendations(state.Items, targetCombinationKey);
                SetRetryMode(false);
                StatusMessage = BuildRecommendationStatusMessage(state);
                return CompleteUi("preview-only", true, state.Items.Count, state.Status);
            }

            if (!state.CanRequest)
            {
                SetWaitingForCandidatePoolRefill(false);
                ApplyRecommendations([], targetCombinationKey);
                SetRetryMode(false);
                StatusMessage = BuildRecommendationStatusMessage(state);
                return CompleteUi("preview-only", true, status: state.Status);
            }

            if (!forceRefresh)
            {
                if (IsErrorState(state))
                {
                    SetWaitingForCandidatePoolRefill(false);
                    ApplyFinalPreviewState(state, targetCombinationKey, RecommendationIncompleteMessage);
                    return CompleteUi("preview-only", true, state.Items.Count, state.Status);
                }

                if (IsCandidatePoolRefillWaitingState(state))
                {
                    ApplyRecommendations(state.Items, targetCombinationKey);
                    SetRetryMode(false);
                    StatusMessage = CandidatePoolRefillWaitingMessage;
                    SetWaitingForCandidatePoolRefill(true);
                    return CompleteUi("low-water-refill", true, state.Items.Count, state.Status);
                }

                if (state.Items.Count > 0 && (state.HasRequested || state.IsPending))
                {
                    SetWaitingForCandidatePoolRefill(false);
                    ApplyRecommendations(state.Items, targetCombinationKey);
                    SetRetryMode(false);
                    StatusMessage = BuildRecommendationStatusMessage(state);
                    StartLowWaterCandidatePoolRefill(queryOptions, state, requestVersion, "preview-success");
                    return CompleteUi("preview-only", true, state.Items.Count, state.Status);
                }

                if (state.HasRequested)
                {
                    ApplyFinalPreviewState(state, targetCombinationKey, RecommendationIncompleteMessage);
                    return CompleteUi("preview-only", true, state.Items.Count, state.Status);
                }
            }

            StatusMessage = forceRefresh
                ? "正在生成新一批推荐，请稍候"
                : BuildLoadingStatusMessage(targetCombinationKey);

            var recommendations = await _recommendationService.GetRecommendationsAsync(
                queryOptions,
                activeCancellationToken);
            if (!IsCurrentLoad(requestVersion))
            {
                return false;
            }

            if (recommendations.Count > 0)
            {
                SetWaitingForCandidatePoolRefill(false);
                ApplyRecommendations(recommendations, targetCombinationKey);
                SetRetryMode(false);
                StatusMessage = BuildRecommendationStatusMessage();
                NotifyRecommendationChangedWithoutReload();
                StartLowWaterCandidatePoolRefill(
                    queryOptions,
                    requestVersion,
                    forceRefresh ? "shuffle-success" : "generation-success");
                return CompleteUi(
                    forceRefresh && state.CandidatePoolCount > 0 ? "candidate-pool-consume" : "foreground-generation",
                    true,
                    recommendations.Count);
            }

            var refreshedState = await _recommendationService.GetRecommendationPreviewStateAsync(queryOptions, activeCancellationToken);
            if (!IsCurrentLoad(requestVersion))
            {
                return false;
            }

            if (forceRefresh && IsCandidatePoolRefillWaitingState(refreshedState))
            {
                ApplyRecommendations(refreshedState.Items, targetCombinationKey);
                SetRetryMode(false);
                StatusMessage = CandidatePoolRefillWaitingMessage;
                SetWaitingForCandidatePoolRefill(true);
                return CompleteUi("low-water-refill", true, refreshedState.Items.Count, refreshedState.Status);
            }

            ApplyFinalPreviewState(refreshedState, targetCombinationKey, RecommendationIncompleteMessage);
            NotifyRecommendationChangedWithoutReload();
            return CompleteUi("foreground-generation", true, refreshedState.Items.Count, refreshedState.Status);
        }
        catch (OperationCanceledException) when (!IsCurrentLoad(requestVersion))
        {
            perfOutcome = "canceled";
            perfPath = "canceled";
            return false;
        }
        catch (OperationCanceledException)
        {
            perfOutcome = "canceled";
            perfPath = "canceled";
            var result = await ApplyCurrentPreviewAfterCanceledAsync(queryOptions, targetCombinationKey, requestVersion);
            return CompleteUi("canceled", result);
        }
        catch (Exception exception)
        {
            if (!IsCurrentLoad(requestVersion))
            {
                perfOutcome = "failed";
                perfPath = "failed";
                perfError = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }

            ApplyRecommendations([], targetCombinationKey);
            SetWaitingForCandidatePoolRefill(false);
            CanRequestRecommendations = true;
            SetRetryMode(true);
            StatusMessage = $"AI 推荐生成失败：{exception.Message}";
            NotifyRecommendationChangedWithoutReload();
            perfOutcome = "failed";
            perfError = $"{exception.GetType().Name}: {exception.Message}";
            return CompleteUi("failed", true);
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, loadCancellation))
            {
                _loadCancellation = null;
            }

            if (IsCurrentLoad(requestVersion))
            {
                IsLoading = false;
            }

            perfScope.Complete(perfOutcome, perfPath, perfItemCount, perfStatus, perfError);
        }
    }

    private string BuildRecommendationStatusMessage(AiRecommendationPreviewState? state = null)
    {
        if (state is not null)
        {
            if (IsEmptyState(state))
            {
                return string.IsNullOrWhiteSpace(state.Message)
                    ? EmptyRecommendationMessage
                    : state.Message;
            }

            if (IsErrorState(state))
            {
                return string.IsNullOrWhiteSpace(state.Message)
                    ? "AI 推荐生成失败，请稍后重试"
                    : state.Message;
            }

            if (string.Equals(state.Status, RecommendationStatusMissingSeed, StringComparison.OrdinalIgnoreCase)
                || !state.CanRequest)
            {
                return MissingRecommendationSeedMessage;
            }

            if (state.IsPending)
            {
                return string.IsNullOrWhiteSpace(state.Message)
                    ? "正在等待 AI 分析并推荐影片"
                    : state.Message;
            }
        }

        if (Recommendations.Count == 0)
        {
            if (SelectedLibraryScope == PlaybackSourceWithoutSource && SelectedWatchFilter == WatchFilterWatched)
            {
                return "无播放源 + 已看的筛选条件下没有可推荐影片；只有已标记已看的无播放源候选影片会进入该组合";
            }

            if (SelectedLibraryScope == PlaybackSourceWithSource && SelectedWatchFilter == WatchFilterUnwatched)
            {
                return "有播放源的未看影片不足，建议切换到全部";
            }

            return SelectedWatchFilter switch
            {
                WatchFilterWatched => "当前没有符合条件的已看影片",
                WatchFilterUnwatched => "当前筛选条件下可推荐影片不足，请调整筛选条件",
                _ => "当前筛选条件下可推荐影片不足，请调整筛选条件"
            };
        }

        if (Recommendations.Count < 3)
        {
            return $"当前筛选条件下新推荐不足，已展示可用结果（{Recommendations.Count} 部）";
        }

        return $"已为你推荐 {Recommendations.Count} 部影片";
    }

    private static bool IsEmptyState(AiRecommendationPreviewState state)
    {
        return state.HasRequested
               && state.Items.Count == 0
               && string.Equals(state.Status, RecommendationStatusEmpty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsErrorState(AiRecommendationPreviewState state)
    {
        return string.Equals(state.Status, RecommendationStatusError, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissingSeedState(AiRecommendationPreviewState state)
    {
        return string.Equals(state.Status, RecommendationStatusMissingSeed, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCandidatePoolRefillWaitingState(AiRecommendationPreviewState state)
    {
        return state.IsPending
               && state.IsUpdating
               && state.Items.Count > 0
               && state.CandidatePoolCount == 0;
    }

    private bool ShouldUpdateAfterCandidatePoolRefill(
        int requestVersion,
        RecommendationQueryOptions queryOptions)
    {
        var combinationKey = BuildRecommendationCombinationKey(queryOptions);
        return Recommendations.Count > 0
               && (IsCurrentLoad(requestVersion)
                   || _isWaitingForCandidatePoolRefill && IsDisplayedCombination(combinationKey));
    }

    private bool IsCurrentLoad(int requestVersion)
    {
        return requestVersion == _loadVersion;
    }

    private bool IsDifferentDisplayedCombination(string targetCombinationKey)
    {
        return Recommendations.Count > 0
               && !string.Equals(_displayedCombinationKey, targetCombinationKey, StringComparison.Ordinal);
    }

    private bool IsDisplayedCombination(string targetCombinationKey)
    {
        return Recommendations.Count > 0
               && string.Equals(_displayedCombinationKey, targetCombinationKey, StringComparison.Ordinal);
    }

    private string BuildLoadingStatusMessage(string targetCombinationKey)
    {
        return IsDisplayedCombination(targetCombinationKey)
            ? "正在根据新的偏好更新推荐"
            : "正在等待 AI 分析并推荐影片";
    }

    private void SetRetryMode(bool isRetry)
    {
        if (_isRecommendationError != isRetry)
        {
            _isRecommendationError = isRetry;
            OnPropertyChanged(nameof(CanChangeFilters));
        }

        RefreshBatchButtonText = isRetry ? RetryRecommendationText : RefreshBatchText;
    }

    private void NotifyRecommendationChangedWithoutReload()
    {
        _suppressNextRecommendationChangedReload = true;
        _dataRefreshService.NotifyRecommendationChanged();
    }

    private void StartLowWaterCandidatePoolRefill(
        RecommendationQueryOptions queryOptions,
        AiRecommendationPreviewState state,
        int requestVersion,
        string trigger)
    {
        var combinationKey = BuildRecommendationCombinationKey(queryOptions);
        WriteAiPoolCheck(trigger, combinationKey, state);

        if (state.IsPending
            || state.Items.Count == 0
            || state.CandidatePoolCount <= 0
            || state.CandidatePoolCount >= CandidatePoolLowWatermark
            || IsErrorState(state)
            || IsEmptyState(state)
            || IsMissingSeedState(state))
        {
            StartLowWaterCandidatePoolRefill(queryOptions, requestVersion, trigger, state);
            return;
        }

        StatusMessage = "正在后台补充推荐候选";
        StartLowWaterCandidatePoolRefill(queryOptions, requestVersion, trigger, state);
    }

    private void StartLowWaterCandidatePoolRefill(
        RecommendationQueryOptions queryOptions,
        int requestVersion,
        string trigger,
        AiRecommendationPreviewState? diagnosticState = null)
    {
        var refillOptions = new RecommendationQueryOptions
        {
            WatchFilter = queryOptions.WatchFilter,
            LibraryScope = queryOptions.LibraryScope,
            BatchSeed = queryOptions.BatchSeed,
            Take = queryOptions.Take,
            ForceRefresh = false
        };

        _ = RefillCandidatePoolIfLowAsync(refillOptions, requestVersion, trigger, diagnosticState);
    }

    private async Task RefillCandidatePoolIfLowAsync(
        RecommendationQueryOptions queryOptions,
        int requestVersion,
        string trigger,
        AiRecommendationPreviewState? diagnosticState)
    {
        try
        {
            if (diagnosticState is null)
            {
                try
                {
                    diagnosticState = await _recommendationService.GetRecommendationPreviewStateAsync(queryOptions);
                    WriteAiPoolCheck(trigger, BuildRecommendationCombinationKey(queryOptions), diagnosticState);
                }
                catch
                {
                    // Keep the refill path unchanged if the temporary diagnostics read fails.
                }
            }

            var refillResult = await _recommendationService.RefillCandidatePoolIfLowAsync(queryOptions, trigger);
            if (!refillResult.Succeeded)
            {
                if (ShouldUpdateAfterCandidatePoolRefill(requestVersion, queryOptions))
                {
                    var wasWaitingForRefill = _isWaitingForCandidatePoolRefill;
                    SetWaitingForCandidatePoolRefill(false);
                    if (wasWaitingForRefill && refillResult.Outcome == CandidatePoolRefillOutcome.Failed)
                    {
                        await SaveCandidatePoolRefillFailureForRetryAsync(queryOptions, refillResult);
                        SetRetryMode(true);
                        StatusMessage = CandidatePoolRefillFailedMessage;
                        WriteAiPoolRefillUiRecover(refillResult, BuildRecommendationCombinationKey(queryOptions), retryMode: true);
                        NotifyRecommendationChangedWithoutReload();
                    }
                    else
                    {
                        StatusMessage = BuildCandidatePoolRefillRecoveryStatusMessage(refillResult, wasWaitingForRefill);
                        if (wasWaitingForRefill)
                        {
                            WriteAiPoolRefillUiRecover(refillResult, BuildRecommendationCombinationKey(queryOptions));
                        }
                    }
                }

                return;
            }

            if (ShouldUpdateAfterCandidatePoolRefill(requestVersion, queryOptions))
            {
                SetWaitingForCandidatePoolRefill(false);
                StatusMessage = BuildRecommendationStatusMessage();
            }

            NotifyRecommendationChangedWithoutReload();
        }
        catch
        {
            if (ShouldUpdateAfterCandidatePoolRefill(requestVersion, queryOptions))
            {
                var wasWaitingForRefill = _isWaitingForCandidatePoolRefill;
                SetWaitingForCandidatePoolRefill(false);
                if (wasWaitingForRefill)
                {
                    await SaveCandidatePoolRefillFailureForRetryAsync(queryOptions);
                    SetRetryMode(true);
                    StatusMessage = CandidatePoolRefillFailedMessage;
                    WriteAiPoolRefillUiRecover("failed", BuildRecommendationCombinationKey(queryOptions), retryMode: true);
                    NotifyRecommendationChangedWithoutReload();
                }
                else
                {
                    StatusMessage = BuildRecommendationStatusMessage();
                }

                return;
            }

            if (DateTime.UtcNow == DateTime.MinValue)
            {
                StatusMessage = "推荐候选后台补充未完成，当前结果已保留";
            }
        }
    }

    private string BuildCandidatePoolRefillRecoveryStatusMessage(
        CandidatePoolRefillResult refillResult,
        bool wasWaitingForRefill)
    {
        if (!wasWaitingForRefill)
        {
            return BuildRecommendationStatusMessage();
        }

        return refillResult.Outcome switch
        {
            CandidatePoolRefillOutcome.Failed => CandidatePoolRefillFailedMessage,
            CandidatePoolRefillOutcome.NoGeneratedCandidates => CandidatePoolRefillNoCandidatesMessage,
            _ => BuildRecommendationStatusMessage()
        };
    }

    private async Task SaveCandidatePoolRefillFailureForRetryAsync(
        RecommendationQueryOptions queryOptions,
        CandidatePoolRefillResult? refillResult = null)
    {
        try
        {
            await _recommendationService.SaveCandidatePoolRefillFailureAsync(
                queryOptions,
                CandidatePoolRefillFailedMessage,
                refillResult?.Fingerprint);
        }
        catch
        {
            // Keep the local retry state even if persisting the temporary refill failure state fails.
        }
    }

    private async Task RefreshPreviewAfterErrorDataChangeAsync(int requestVersion)
    {
        if (!IsCurrentLoad(requestVersion))
        {
            return;
        }

        var queryOptions = new RecommendationQueryOptions
        {
            WatchFilter = GetWatchFilter(),
            LibraryScope = GetLibraryScope(),
            BatchSeed = _batchSeed,
            Take = 3,
            ForceRefresh = false
        };
        var targetCombinationKey = BuildRecommendationCombinationKey(queryOptions);
        try
        {
            var state = await _recommendationService.GetRecommendationPreviewStateAsync(queryOptions);
            if (!IsCurrentLoad(requestVersion) || !_isRecommendationError)
            {
                return;
            }

            if (IsMissingSeedState(state))
            {
                ApplyFinalPreviewState(state, targetCombinationKey, RecommendationIncompleteMessage);
                return;
            }

            await RefreshRecommendationItemStatesAsync(requestVersion);
        }
        catch
        {
            // Keep the visible Error state. RecommendationChanged during Error must not trigger AI generation.
        }
    }

    private async Task<bool> ApplyCurrentPreviewAfterCanceledAsync(
        RecommendationQueryOptions queryOptions,
        string targetCombinationKey,
        int requestVersion)
    {
        if (!IsCurrentLoad(requestVersion))
        {
            return false;
        }

        try
        {
            var state = await _recommendationService.GetRecommendationPreviewStateAsync(queryOptions, CancellationToken.None);
            if (!IsCurrentLoad(requestVersion))
            {
                return false;
            }

            ApplyFinalPreviewState(state, targetCombinationKey, RecommendationIncompleteMessage);
            return true;
        }
        catch (Exception exception)
        {
            if (!IsCurrentLoad(requestVersion))
            {
                return false;
            }

            if (!IsDisplayedCombination(targetCombinationKey) || Recommendations.Count == 0)
            {
                ApplyRecommendations([], targetCombinationKey);
            }

            StatusMessage = $"AI 推荐状态刷新失败：{exception.Message}";
            return true;
        }
    }

    private void ApplyFinalPreviewState(
        AiRecommendationPreviewState state,
        string targetCombinationKey,
        string fallbackMessage)
    {
        CanRequestRecommendations = state.CanRequest;
        SetWaitingForCandidatePoolRefill(false);
        if (IsMissingSeedState(state))
        {
            ApplyRecommendations([], targetCombinationKey);
            SetRetryMode(false);
            StatusMessage = BuildRecommendationStatusMessage(state);
            return;
        }

        if (IsEmptyState(state))
        {
            ApplyRecommendations(state.Items, targetCombinationKey);
            SetRetryMode(false);
            StatusMessage = BuildRecommendationStatusMessage(state);
            return;
        }

        if (IsErrorState(state))
        {
            if (state.Items.Count > 0)
            {
                ApplyRecommendations(state.Items, targetCombinationKey);
            }

            SetRetryMode(true);
            StatusMessage = string.IsNullOrWhiteSpace(state.Message)
                ? "AI 推荐生成失败，请稍后重试"
                : state.Message;
            return;
        }

        if (state.Items.Count > 0)
        {
            ApplyRecommendations(state.Items, targetCombinationKey);
            SetRetryMode(false);
            StatusMessage = BuildRecommendationStatusMessage(state);
            return;
        }

        if (!state.CanRequest)
        {
            ApplyRecommendations([], targetCombinationKey);
            SetRetryMode(false);
            StatusMessage = BuildRecommendationStatusMessage(state);
            return;
        }

        if (state.HasRequested && !state.IsPending)
        {
            ApplyRecommendations(state.Items, targetCombinationKey);
            SetRetryMode(false);
            StatusMessage = BuildRecommendationStatusMessage(state);
            return;
        }

        if (IsDisplayedCombination(targetCombinationKey))
        {
            StatusMessage = RecommendationIncompletePreservedMessage;
            return;
        }

        ApplyRecommendations([], targetCombinationKey);
        SetRetryMode(false);
        StatusMessage = fallbackMessage;
    }

    private void OpenMovie(object? parameter)
    {
        if (parameter is not AiRecommendationItem item)
        {
            return;
        }

        if (item.MovieId > 0)
        {
            _navigationStateService.RequestNavigation(NavigationPageKey.MovieDetail, item.MovieId);
            return;
        }

        _navigationStateService.RequestExternalMovieDetail(item);
    }

    private async Task AddWantToWatchAsync(object? parameter)
    {
        if (parameter is not AiRecommendationItem item)
        {
            return;
        }

        if (item.IsWatched)
        {
            return;
        }

        var previousState = item.IsWantToWatch;
        var previousNotInterested = item.IsNotInterested;
        try
        {
            if (previousState)
            {
                await _userCollectionService.RemoveWantToWatchAsync(item, changeSource: "Recommendation");
                item.IsWantToWatch = false;
            }
            else
            {
                await _userCollectionService.AddWantToWatchAsync(item, changeSource: "Recommendation");
                item.IsWantToWatch = true;
                item.IsNotInterested = false;
            }

            AddWantToWatchCommand.RaiseCanExecuteChanged();
            ToggleNotInterestedCommand.RaiseCanExecuteChanged();
            _dataRefreshService.NotifyCollectionChanged();
            if (item.TmdbId is > 0)
            {
                _dataRefreshService.NotifyRecommendationChanged();
            }
        }
        catch (Exception exception)
        {
            item.IsWantToWatch = previousState;
            item.IsNotInterested = previousNotInterested;
            AddWantToWatchCommand.RaiseCanExecuteChanged();
            ToggleNotInterestedCommand.RaiseCanExecuteChanged();
            StatusMessage = $"想看状态更新失败：{exception.Message}";
        }
    }

    private bool CanToggleWantToWatch(object? parameter)
    {
        return parameter is AiRecommendationItem { CanToggleWantToWatch: true };
    }

    private async Task ToggleNotInterestedAsync(object? parameter)
    {
        if (parameter is not AiRecommendationItem item || IsLoading)
        {
            return;
        }

        var previousNotInterested = item.IsNotInterested;
        var previousWantToWatch = item.IsWantToWatch;
        var targetNotInterested = !previousNotInterested;
        try
        {
            await _userCollectionService.SetNotInterestedAsync(item, targetNotInterested, changeSource: "Recommendation");
            item.IsNotInterested = targetNotInterested;
            if (targetNotInterested)
            {
                item.IsWantToWatch = false;
                Recommendations.Remove(item);
            }
            StatusMessage = BuildRecommendationStatusMessage();

            AddWantToWatchCommand.RaiseCanExecuteChanged();
            ToggleNotInterestedCommand.RaiseCanExecuteChanged();
            _dataRefreshService.NotifyCollectionChanged();
            NotifyRecommendationChangedWithoutReload();
        }
        catch (Exception exception)
        {
            item.IsNotInterested = previousNotInterested;
            item.IsWantToWatch = previousWantToWatch;
            ToggleNotInterestedCommand.RaiseCanExecuteChanged();
            StatusMessage = $"不想看状态更新失败：{exception.Message}";
        }
    }

    private bool CanToggleNotInterested(object? parameter)
    {
        return !IsLoading && parameter is AiRecommendationItem;
    }

    private async Task RefreshRecommendationItemStatesAsync(int requestVersion, CancellationToken cancellationToken = default)
    {
        if (Recommendations.Count == 0)
        {
            return;
        }

        var collectionItems = await _userCollectionService.GetCollectionItemsAsync(cancellationToken);
        var notInterestedKeys = await _userCollectionService.GetNotInterestedKeysAsync(cancellationToken);
        if (!IsCurrentLoad(requestVersion))
        {
            return;
        }

        var notInterestedItems = new List<AiRecommendationItem>();
        foreach (var item in Recommendations)
        {
            if (notInterestedKeys.Any(key => IsSameNotInterestedKey(key, item)))
            {
                item.IsNotInterested = true;
                notInterestedItems.Add(item);
                continue;
            }

            var collectionItem = collectionItems.FirstOrDefault(x => IsSameRecommendation(x, item));
            if (collectionItem is null)
            {
                if (!item.IsInLibrary)
                {
                    item.IsWantToWatch = false;
                    item.IsWatched = false;
                    item.IsNotInterested = false;
                }

                continue;
            }

            item.IsNotInterested = collectionItem.IsNotInterested;
            if (item.IsNotInterested)
            {
                notInterestedItems.Add(item);
                continue;
            }

            item.IsWantToWatch = collectionItem.IsWantToWatch;
            if (collectionItem is not null)
            {
                item.IsWatched = item.IsInLibrary
                    ? item.IsWatched || collectionItem.IsWatched
                    : collectionItem.IsWatched;
                item.WatchStateText = item.IsWatched ? "已看" : "未看";
            }
        }

        foreach (var item in notInterestedItems)
        {
            Recommendations.Remove(item);
        }

        AddWantToWatchCommand.RaiseCanExecuteChanged();
        ToggleNotInterestedCommand.RaiseCanExecuteChanged();
    }

    private void ApplyRecommendations(IEnumerable<AiRecommendationItem> recommendations, string combinationKey)
    {
        Recommendations.Clear();
        foreach (var recommendation in recommendations)
        {
            Recommendations.Add(recommendation);
        }

        _displayedCombinationKey = combinationKey;
    }

    private static void WriteAiPoolCheck(
        string trigger,
        string combinationKey,
        AiRecommendationPreviewState state)
    {
        WriteAiPoolDiagnostic(
            $"[AI-POOL] event=check trigger={trigger} combination={combinationKey} status={FormatAiPoolValue(state.Status)} current={state.Items.Count} poolRaw={state.CandidatePoolRawCount} poolAvailable={state.CandidatePoolCount} fp={ShortAiPoolFingerprint(state.Fingerprint)}");
    }

    private static void WriteAiPoolRefillUiRecover(
        CandidatePoolRefillResult refillResult,
        string combinationKey,
        bool retryMode = false)
    {
        var reason = refillResult.Outcome switch
        {
            CandidatePoolRefillOutcome.Failed => "failed",
            CandidatePoolRefillOutcome.NoGeneratedCandidates => "no-generated-candidates",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(reason))
        {
            WriteAiPoolRefillUiRecover(reason, combinationKey, retryMode);
        }
    }

    private static void WriteAiPoolRefillUiRecover(
        string reason,
        string combinationKey,
        bool retryMode = false)
    {
        var modeSegment = retryMode ? " mode=retry" : string.Empty;
        WriteAiPoolDiagnostic(
            $"[AI-POOL] event=refill-ui-recover reason={reason}{modeSegment} combination={combinationKey}");
    }

    // TODO: Remove AI-POOL temporary diagnostics after candidate pool validation.
    private static void WriteAiPoolDiagnostic(string message)
    {
        Debug.WriteLine(message);
        Console.WriteLine(message);
        try
        {
            lock (AiPoolDiagnosticFileLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AiPoolDiagnosticLogPath)!);
                File.AppendAllText(
                    AiPoolDiagnosticLogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Temporary diagnostics must never affect recommendation behavior.
        }
    }

    private static string FormatAiPoolValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(none)"
            : value.Trim().Replace(' ', '-');
    }

    private static string ShortAiPoolFingerprint(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return "(none)";
        }

        var value = fingerprint.Trim();
        var lastSeparator = value.LastIndexOf(':');
        if (lastSeparator >= 0 && lastSeparator < value.Length - 1)
        {
            value = value[(lastSeparator + 1)..];
        }

        return value.Length <= 8 ? value : value[..8];
    }

    private static string BuildRecommendationCombinationKey(RecommendationQueryOptions options)
    {
        return $"scope:{options.LibraryScope}|watch:{options.WatchFilter}";
    }

    private RecommendationWatchFilter GetWatchFilter()
    {
        return SelectedWatchFilter switch
        {
            WatchFilterAll => RecommendationWatchFilter.IncludeWatched,
            WatchFilterWatched => RecommendationWatchFilter.WatchedOnly,
            _ => RecommendationWatchFilter.UnwatchedOnly
        };
    }

    private RecommendationLibraryScope GetLibraryScope()
    {
        return SelectedLibraryScope switch
        {
            PlaybackSourceWithSource => RecommendationLibraryScope.InLibraryOnly,
            PlaybackSourceWithoutSource => RecommendationLibraryScope.OutsideLibraryOnly,
            _ => RecommendationLibraryScope.All
        };
    }

    private static bool IsSameRecommendation(CollectionMovieItem collectionItem, AiRecommendationItem recommendation)
    {
        return (recommendation.MovieId > 0 && collectionItem.MovieId == recommendation.MovieId)
               || (recommendation.TmdbId.HasValue && collectionItem.TmdbId == recommendation.TmdbId)
               || (!string.IsNullOrWhiteSpace(recommendation.ImdbId)
                   && string.Equals(
                       recommendation.ImdbId.Trim(),
                       collectionItem.ImdbId?.Trim(),
                       StringComparison.OrdinalIgnoreCase))
               || (collectionItem.ReleaseYear == recommendation.ReleaseYear
                   && string.Equals(
                       NormalizeTitle(collectionItem.Title),
                        NormalizeTitle(recommendation.Title),
                        StringComparison.Ordinal));
    }

    private static bool IsSameNotInterestedKey(NotInterestedMovieKey key, AiRecommendationItem recommendation)
    {
        return (recommendation.MovieId > 0 && key.MovieId == recommendation.MovieId)
               || (recommendation.TmdbId.HasValue && key.TmdbId == recommendation.TmdbId)
               || (!string.IsNullOrWhiteSpace(recommendation.ImdbId)
                   && string.Equals(
                       recommendation.ImdbId.Trim(),
                       key.ImdbId?.Trim(),
                       StringComparison.OrdinalIgnoreCase))
               || (key.ReleaseYear.HasValue
                   && recommendation.ReleaseYear.HasValue
                   && key.ReleaseYear == recommendation.ReleaseYear
                   && string.Equals(
                       NormalizeTitle(key.Title),
                       NormalizeTitle(recommendation.Title),
                       StringComparison.Ordinal));
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var chars = title.Trim().ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch >= 0x4e00 && ch <= 0x9fff);
        return string.Concat(chars);
    }
}
