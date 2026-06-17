using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using MediaLibrary.App.Helpers;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.App.ViewModels.Player;

public sealed class OnlineSubtitleSearchViewModel : ViewModelBase
{
    private const string ProviderName = "OpenSubtitles";
    private readonly IOpenSubtitlesClientService _openSubtitlesClientService;
    private readonly ISettingsService _settingsService;
    private readonly IOnlineSubtitleBindingService _onlineSubtitleBindingService;
    private readonly IOnlineSubtitleCacheService _onlineSubtitleCacheService;
    private readonly OnlineSubtitleSearchContext _context;
    private readonly Func<OnlineSubtitlePlaybackRequest, Task<OnlineSubtitlePlaybackApplyResult>> _applySubtitleAsync;
    private readonly List<OnlineSubtitleSearchResultViewModel> _allResults = [];
    private CancellationTokenSource? _searchCts;
    private OpenSubtitlesLanguageOption? _selectedLanguage;
    private OnlineSubtitleSearchTypeOption? _selectedType;
    private OnlineSubtitleSortOption? _selectedSort;
    private string _searchQuery = string.Empty;
    private string _statusMessage = string.Empty;
    private string _quotaHint = string.Empty;
    private bool _isLoading;
    private int _totalCount;
    private int _totalPages;
    private bool _isSortAscending;

    public OnlineSubtitleSearchViewModel(
        IOpenSubtitlesClientService openSubtitlesClientService,
        ISettingsService settingsService,
        IOnlineSubtitleBindingService onlineSubtitleBindingService,
        IOnlineSubtitleCacheService onlineSubtitleCacheService,
        OnlineSubtitleSearchContext context,
        Func<OnlineSubtitlePlaybackRequest, Task<OnlineSubtitlePlaybackApplyResult>> applySubtitleAsync)
    {
        _openSubtitlesClientService = openSubtitlesClientService;
        _settingsService = settingsService;
        _onlineSubtitleBindingService = onlineSubtitleBindingService;
        _onlineSubtitleCacheService = onlineSubtitleCacheService;
        _context = context;
        _applySubtitleAsync = applySubtitleAsync;

        Languages = _openSubtitlesClientService.SupportedLanguages
            .Select(x => new OpenSubtitlesLanguageOption(x.Code, LocalizeLanguageName(x)))
            .OrderBy(x => GetLanguagePriority(x.Code))
            .ToList();
        Types =
        [
            new OnlineSubtitleSearchTypeOption("movie", "电影"),
            new OnlineSubtitleSearchTypeOption("episode", "电视剧集")
        ];
        SortOptions =
        [
            new OnlineSubtitleSortOption("composite", "综合排序"),
            new OnlineSubtitleSortOption("downloads", "下载量"),
            new OnlineSubtitleSortOption("rating", "评分"),
            new OnlineSubtitleSortOption("uploaded", "上传时间"),
            new OnlineSubtitleSortOption("match", "匹配度")
        ];

        _selectedLanguage = Languages.FirstOrDefault(x => x.Code.Equals(context.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
                            ?? Languages.FirstOrDefault(x => x.Code.Equals("zh-cn", StringComparison.OrdinalIgnoreCase))
                            ?? Languages.FirstOrDefault();
        _selectedType = Types.First(x => x.Key == context.DefaultType);
        _selectedSort = SortOptions.First();
        _searchQuery = context.InitialQuery;
        _quotaHint = string.Empty;

        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsLoading);
        ClearSearchQueryCommand = new RelayCommand(() => SearchQuery = string.Empty);
        ToggleSortDirectionCommand = new RelayCommand(ToggleSortDirection);
        SelectSortCommand = new RelayCommand(parameter =>
        {
            if (parameter is OnlineSubtitleSortOption option)
            {
                SelectedSort = option;
            }
        });
        SelectTypeCommand = new RelayCommand(parameter =>
        {
            if (parameter is OnlineSubtitleSearchTypeOption option)
            {
                SelectedType = option;
            }
        });
        SelectLanguageCommand = new RelayCommand(parameter =>
        {
            if (parameter is OpenSubtitlesLanguageOption option)
            {
                SelectedLanguage = option;
            }
        });
    }

    public IReadOnlyList<OpenSubtitlesLanguageOption> Languages { get; }

    public IReadOnlyList<OnlineSubtitleSearchTypeOption> Types { get; }

    public IReadOnlyList<OnlineSubtitleSortOption> SortOptions { get; }

    public ObservableCollection<OnlineSubtitleSearchResultViewModel> Results { get; } = [];

    public AsyncRelayCommand SearchCommand { get; }

    public RelayCommand ClearSearchQueryCommand { get; }

    public RelayCommand ToggleSortDirectionCommand { get; }

    public RelayCommand SelectSortCommand { get; }

    public RelayCommand SelectTypeCommand { get; }

    public RelayCommand SelectLanguageCommand { get; }

    public string WindowTitle => "搜索在线字幕";

    public string ContextSummary => _context.ContextSummary;

    public string SafeFileNameHint => _context.SafeFileName;

    public string CurrentSourceDisplayText => $"当前播放源：{SafeFileNameHint}";

    public string SortButtonText => BuildFilterButtonText("排序", SelectedSort?.DisplayName);

    public string TypeButtonText => BuildFilterButtonText("类型", SelectedType?.DisplayName);

    public string LanguageButtonText => BuildFilterButtonText("语言", SelectedLanguage?.Name);

    public string SortDirectionButtonToolTip => BuildFilterButtonText("顺序", _isSortAscending ? "升序" : "降序");

    public string SortDirectionIconData => _isSortAscending
        ? "sort-ascending"
        : "sort-descending";

    public OpenSubtitlesLanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                OnPropertyChanged(nameof(LanguageButtonText));
            }
        }
    }

    public OnlineSubtitleSearchTypeOption? SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                OnPropertyChanged(nameof(TypeButtonText));
            }
        }
    }

    public OnlineSubtitleSortOption? SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (SetProperty(ref _selectedSort, value))
            {
                OnPropertyChanged(nameof(SortButtonText));
                ApplySort();
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(IsStatusVisible));
            }
        }
    }

    public string QuotaHint
    {
        get => _quotaHint;
        private set => SetProperty(ref _quotaHint, value);
    }

    public bool IsStatusVisible => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsNotLoading));
            }
        }
    }

    public bool IsNotLoading => !IsLoading;

    public bool HasResults => Results.Count > 0;

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        private set => SetProperty(ref _totalPages, value);
    }

    public async Task InitializeAsync()
    {
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var cancellationToken = _searchCts.Token;

        Results.Clear();
        _allResults.Clear();
        RaiseResultsChanged();
        TotalCount = 0;
        TotalPages = 0;

        var query = SanitizeKeyword(SearchQuery);
        if (string.IsNullOrWhiteSpace(query))
        {
            StatusMessage = "请输入搜索关键词。";
            return;
        }

        IsLoading = true;
        StatusMessage = "正在搜索在线字幕...";

        try
        {
            var settings = await _settingsService.GetApplicationSettingAsync(cancellationToken);
            if (!settings.IsOpenSubtitlesEnabled || string.IsNullOrWhiteSpace(settings.OpenSubtitlesApiKey))
            {
                StatusMessage = "请先到设置页配置并启用在线字幕 API。";
                return;
            }

            var options = BuildOptions(settings);
            var request = BuildSearchRequest(query);
            MpvPlaybackDiagnostics.Write(
                $"online-subtitle-search-start type={request.Type} language={request.Languages} queryLength={query.Length} hasMovieId={(!string.IsNullOrWhiteSpace(request.ImdbId) || request.TmdbId.HasValue).ToString().ToLowerInvariant()} hasParentId={(!string.IsNullOrWhiteSpace(request.ParentImdbId) || request.ParentTmdbId.HasValue).ToString().ToLowerInvariant()} filenameHintLength={request.FileNameHint.Length}");

            var page = await _openSubtitlesClientService.SearchAsync(options, request, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            TotalCount = page.TotalCount;
            TotalPages = page.TotalPages;
            var existingBindings = await GetExistingCachedBindingsAsync(cancellationToken);
            foreach (var item in page.Results)
            {
                var result = new OnlineSubtitleSearchResultViewModel(
                    item,
                    ComputeMatchScore(item, request),
                    SelectedLanguage?.Code ?? "zh-cn",
                    DownloadResultAsync);
                result.IsDownloaded = existingBindings.Any(binding => MatchesExistingCachedBinding(binding, result));
                _allResults.Add(result);
            }

            ApplySort();
            StatusMessage = Results.Count == 0
                ? MapSearchStatus(page.ResultMessage)
                : $"找到 {Results.Count} 条字幕结果。";
            MpvPlaybackDiagnostics.Write(
                $"online-subtitle-search-complete type={request.Type} language={request.Languages} resultCount={Results.Count} totalCount={TotalCount}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            MpvPlaybackDiagnostics.Write($"online-subtitle-search-failed errorType={exception.GetType().Name}");
            StatusMessage = "在线字幕搜索失败，请检查网络、API 配置或稍后重试。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DownloadResultAsync(OnlineSubtitleSearchResultViewModel result)
    {
        if (string.IsNullOrWhiteSpace(result.ProviderFileId))
        {
            result.DownloadStatus = "该结果缺少 OpenSubtitles file id，无法下载。";
            return;
        }

        result.IsDownloading = true;
        result.DownloadStatus = "正在准备下载...";
        StatusMessage = $"正在下载：{result.PrimaryText}";

        try
        {
            var settings = await _settingsService.GetApplicationSettingAsync();
            if (!settings.IsOpenSubtitlesEnabled || string.IsNullOrWhiteSpace(settings.OpenSubtitlesApiKey))
            {
                result.DownloadStatus = "请先到设置页配置并启用在线字幕 API。";
                StatusMessage = result.DownloadStatus;
                return;
            }

            var options = BuildOptions(settings);
            var existing = await FindExistingCachedBindingAsync(result);
            if (existing is not null)
            {
                var existingPath = _onlineSubtitleCacheService.GetAbsolutePath(existing.CacheRelativePath);
                var existingApply = await _applySubtitleAsync(
                    new OnlineSubtitlePlaybackRequest
                    {
                        Binding = existing,
                        AbsolutePath = existingPath,
                        DisplayName = result.PrimaryText,
                        FileName = existing.FileName,
                        ProviderFileId = existing.ProviderFileId
                    });
                result.DownloadStatus = existingApply.Succeeded
                    ? "已存在相同绑定，已直接切换。"
                    : existingApply.Message;
                StatusMessage = result.DownloadStatus;
                if (existingApply.Succeeded)
                {
                    result.IsDownloaded = true;
                    result.DownloadStatus = string.Empty;
                }

                return;
            }

            var download = await _openSubtitlesClientService.DownloadAsync(
                options,
                new OpenSubtitlesDownloadContractRequest
                {
                    FileId = result.ProviderFileId,
                    FileName = FirstNonEmpty(result.FileName, result.ReleaseName, $"{result.ProviderFileId}.srt")
                });

            if (!download.Succeeded)
            {
                var message = MapDownloadFailure(download);
                result.DownloadStatus = message;
                StatusMessage = message;
                return;
            }

            QuotaHint = BuildQuotaHint(download);

            await using var content = new MemoryStream(download.Content, writable: false);
            var cache = await _onlineSubtitleCacheService.SaveAsync(
                ProviderName,
                result.ProviderFileId,
                FirstNonEmpty(download.FileName, result.FileName, result.ReleaseName, $"{result.ProviderFileId}.srt"),
                content);

            OnlineSubtitleBindingListItem? binding = null;
            if (_context.BindingMovieId.HasValue || _context.BindingEpisodeId.HasValue || _context.BindingMediaFileId.HasValue)
            {
                binding = await _onlineSubtitleBindingService.UpsertBindingAsync(
                    new OnlineSubtitleBindingUpsertRequest
                    {
                        MovieId = _context.BindingMovieId,
                        EpisodeId = _context.BindingEpisodeId,
                        MediaFileId = _context.BindingMediaFileId,
                        Provider = ProviderName,
                        ProviderSubtitleId = result.SubtitleId,
                        ProviderFileId = result.ProviderFileId,
                        LanguageCode = result.LanguageCode,
                        LanguageName = result.LanguageName,
                        DisplayName = result.PrimaryText,
                        ReleaseName = result.ReleaseName,
                        FileName = cache.FileName,
                        CacheRelativePath = cache.RelativePath,
                        CacheHash = cache.Hash,
                        Format = cache.Extension.TrimStart('.'),
                        Extension = cache.Extension,
                        DownloadCount = result.DownloadCount,
                        Rating = result.Rating,
                        Votes = result.Votes,
                        IsHearingImpaired = result.IsHearingImpaired,
                        IsMachineTranslated = result.IsMachineTranslated,
                        IsAiTranslated = result.IsAiTranslated,
                        IsTrustedUploader = result.IsTrustedUploader,
                        Fps = result.Fps,
                        UploadedAt = result.UploadedAt,
                        MetadataJson = result.RawMetadataJson
                    });
            }

            var applyResult = await _applySubtitleAsync(
                new OnlineSubtitlePlaybackRequest
                {
                    Binding = binding,
                    AbsolutePath = _onlineSubtitleCacheService.GetAbsolutePath(cache.RelativePath),
                    DisplayName = binding is null ? $"临时 · {result.PrimaryText}" : BuildBindingDisplayName(binding),
                    FileName = cache.FileName,
                    ProviderFileId = result.ProviderFileId
                });

            result.DownloadStatus = applyResult.Succeeded
                ? BuildDownloadSuccessMessage(download, binding is null)
                : applyResult.Message;
            StatusMessage = result.DownloadStatus;
            if (applyResult.Succeeded && binding is not null)
            {
                result.IsDownloaded = true;
                result.DownloadStatus = string.Empty;
            }

            MpvPlaybackDiagnostics.Write(
                $"online-subtitle-download-complete bound={(binding is not null).ToString().ToLowerInvariant()} remaining={(download.Remaining?.ToString(CultureInfo.InvariantCulture) ?? "unknown")}");
        }
        catch (InvalidOperationException exception)
        {
            var message = MapCacheFailure(exception.Message);
            result.DownloadStatus = message;
            StatusMessage = message;
            MpvPlaybackDiagnostics.Write($"online-subtitle-download-failed errorType={exception.GetType().Name}");
        }
        catch (Exception exception)
        {
            result.DownloadStatus = "在线字幕下载失败，请检查网络、API 配置或稍后重试。";
            StatusMessage = result.DownloadStatus;
            MpvPlaybackDiagnostics.Write($"online-subtitle-download-failed errorType={exception.GetType().Name}");
        }
        finally
        {
            result.IsDownloading = false;
        }
    }

    private OpenSubtitlesClientOptions BuildOptions(ApplicationSettingModel settings)
    {
        return new OpenSubtitlesClientOptions
        {
            Endpoint = settings.OpenSubtitlesEndpoint,
            ApiKey = settings.OpenSubtitlesApiKey,
            Username = settings.OpenSubtitlesUsername,
            Password = settings.OpenSubtitlesPassword,
            Token = settings.OpenSubtitlesToken,
            DefaultLanguageCode = SelectedLanguage?.Code ?? settings.OpenSubtitlesDefaultLanguageCode
        };
    }

    private async Task<OnlineSubtitleBindingListItem?> FindExistingCachedBindingAsync(
        OnlineSubtitleSearchResultViewModel result)
    {
        var bindings = await GetExistingCachedBindingsAsync(CancellationToken.None);
        return bindings.FirstOrDefault(x => MatchesExistingCachedBinding(x, result));
    }

    private async Task<IReadOnlyList<OnlineSubtitleBindingListItem>> GetExistingCachedBindingsAsync(CancellationToken cancellationToken)
    {
        if (!_context.BindingMovieId.HasValue && !_context.BindingEpisodeId.HasValue && !_context.BindingMediaFileId.HasValue)
        {
            return [];
        }

        return await _onlineSubtitleBindingService.GetActiveBindingsAsync(
            _context.BindingMovieId,
            _context.BindingEpisodeId,
            _context.BindingMediaFileId,
            cancellationToken);
    }

    private static bool MatchesExistingCachedBinding(
        OnlineSubtitleBindingListItem binding,
        OnlineSubtitleSearchResultViewModel result)
    {
        return binding.HasCacheFile
               && string.Equals(binding.Provider, ProviderName, StringComparison.OrdinalIgnoreCase)
               && (string.Equals(binding.ProviderFileId, result.ProviderFileId, StringComparison.OrdinalIgnoreCase)
                   || (!string.IsNullOrWhiteSpace(result.SubtitleId)
                       && string.Equals(binding.ProviderSubtitleId, result.SubtitleId, StringComparison.OrdinalIgnoreCase)));
    }

    private static string BuildBindingDisplayName(OnlineSubtitleBindingListItem binding)
    {
        var name = FirstNonEmpty(binding.DisplayName, binding.ReleaseName, binding.FileName, $"在线字幕 {binding.Id}");
        var language = FirstNonEmpty(binding.LanguageName, binding.LanguageCode);
        return string.IsNullOrWhiteSpace(language) ? name : $"{language} · {name}";
    }

    private static string BuildQuotaHint(OpenSubtitlesDownloadResult result)
    {
        var parts = new List<string>();
        if (result.Remaining.HasValue)
        {
            parts.Add($"剩余下载次数：{result.Remaining.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (result.Requests.HasValue)
        {
            parts.Add($"本次请求计数：{result.Requests.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(result.ResetTime))
        {
            parts.Add($"重置时间：{result.ResetTime}");
        }

        return parts.Count == 0
            ? "下载已完成；OpenSubtitles 本次未返回剩余额度，后续下载仍以服务端返回为准。"
            : string.Join("；", parts);
    }

    private static string BuildDownloadSuccessMessage(OpenSubtitlesDownloadResult result, bool isTemporary)
    {
        var scope = isTemporary
            ? "字幕已下载并临时加载到当前播放会话。"
            : "字幕已下载、绑定并切换。";
        var quota = BuildQuotaHint(result);
        return $"{scope} {quota}";
    }

    private static string MapDownloadFailure(OpenSubtitlesDownloadResult result)
    {
        return result.ErrorKind switch
        {
            OpenSubtitlesErrorKind.NotConfigured => "OpenSubtitles API Key 未配置，请到设置页填写有效 API Key。",
            OpenSubtitlesErrorKind.Unauthorized => "OpenSubtitles 下载鉴权失败：API Key 或登录 token 已失效，请到设置页重新测试在线字幕配置。",
            OpenSubtitlesErrorKind.Forbidden => "OpenSubtitles 拒绝下载：API Key 无效、无权限、下载额度受限，或下载链接已不可用。",
            OpenSubtitlesErrorKind.RateLimited => "OpenSubtitles 下载被限流或额度不足，请稍后重试；具体额度以下载返回为准。",
            OpenSubtitlesErrorKind.ServerError => "OpenSubtitles 下载服务暂时不可用，请稍后重试。",
            OpenSubtitlesErrorKind.Network => "无法下载字幕文件，请检查网络、代理或防火墙设置。",
            OpenSubtitlesErrorKind.InvalidResponse => "OpenSubtitles 下载响应异常，未拿到可保存的字幕文件。",
            _ => string.IsNullOrWhiteSpace(result.Message)
                ? "OpenSubtitles 下载失败，原因未知。"
                : $"OpenSubtitles 下载失败：{result.Message}"
        };
    }

    private static string MapCacheFailure(string error)
    {
        return error switch
        {
            "UnsupportedSubtitleExtension" => "下载文件格式不支持，仅支持 .srt / .ass / .ssa / .vtt。",
            "SubtitleFileTooLarge" => "下载字幕文件过大，已拒绝保存。",
            "SubtitleFileEmpty" => "下载字幕文件为空，已拒绝保存。",
            "ZipDoesNotContainSupportedSubtitle" => "下载压缩包中没有受支持的字幕文件。",
            "SubtitleCachePathEmpty" or "SubtitleCachePathEscapesRoot" => "字幕缓存路径异常，已拒绝保存。",
            _ => "字幕缓存保存失败，未写入绑定。"
        };
    }

    private OpenSubtitlesSearchRequest BuildSearchRequest(string query)
    {
        var type = SelectedType?.Key ?? _context.DefaultType;
        var language = SelectedLanguage?.Code ?? _context.DefaultLanguageCode;
        var sort = SelectedSort?.Key ?? "composite";
        var useEpisodeFields = type == "episode";
        var orderBy = sort switch
        {
            "downloads" => "download_count",
            "rating" => "ratings",
            "uploaded" => "upload_date",
            _ => string.Empty
        };

        return new OpenSubtitlesSearchRequest
        {
            Query = query,
            ImdbId = useEpisodeFields ? string.Empty : _context.MovieImdbId,
            TmdbId = useEpisodeFields ? null : _context.MovieTmdbId,
            ParentImdbId = useEpisodeFields ? _context.SeriesImdbId : string.Empty,
            ParentTmdbId = useEpisodeFields ? _context.SeriesTmdbId : null,
            SeasonNumber = useEpisodeFields ? _context.SeasonNumber : null,
            EpisodeNumber = useEpisodeFields ? _context.EpisodeNumber : null,
            Languages = language,
            FileNameHint = _context.SafeFileName,
            FileSize = _context.FileSize > 0 ? _context.FileSize : null,
            Year = useEpisodeFields ? null : _context.MovieReleaseYear,
            Type = type,
            Page = 1,
            OrderBy = orderBy,
            OrderDirection = string.IsNullOrWhiteSpace(orderBy)
                ? string.Empty
                : _isSortAscending ? "asc" : "desc"
        };
    }

    private int ComputeMatchScore(OpenSubtitlesSearchItem item, OpenSubtitlesSearchRequest request)
    {
        var score = 0;
        if (item.LanguageCode.Equals(request.Languages, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (request.SeasonNumber.HasValue && item.SeasonNumber == request.SeasonNumber)
        {
            score += 20;
        }

        if (request.EpisodeNumber.HasValue && item.EpisodeNumber == request.EpisodeNumber)
        {
            score += 20;
        }

        if (request.Year.HasValue && item.FeatureYear == request.Year)
        {
            score += 12;
        }

        score += CalculateTextOverlapScore(request.Query, item.FeatureTitle, 18);
        score += CalculateTextOverlapScore(_context.SafeFileNameWithoutExtension, item.ReleaseName, 16);
        score += CalculateTextOverlapScore(_context.SafeFileNameWithoutExtension, item.FileName, 16);

        if (item.IsTrustedUploader)
        {
            score += 6;
        }

        if (item.DownloadCount.HasValue)
        {
            score += Math.Min(8, item.DownloadCount.Value / 500);
        }

        if (item.Rating.HasValue)
        {
            score += Math.Min(8, (int)Math.Round(item.Rating.Value));
        }

        if (item.UploadedAt.HasValue && item.UploadedAt.Value >= DateTime.UtcNow.AddYears(-3))
        {
            score += 3;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static int CalculateTextOverlapScore(string left, string right, int maxScore)
    {
        var leftTokens = Tokenize(left);
        if (leftTokens.Count == 0 || string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        var rightTokens = Tokenize(right);
        if (rightTokens.Count == 0)
        {
            return 0;
        }

        var matches = leftTokens.Count(token => rightTokens.Contains(token));
        return Math.Min(maxScore, (int)Math.Round(maxScore * (matches / (double)leftTokens.Count)));
    }

    private static HashSet<string> Tokenize(string value)
    {
        return Regex.Split(value.ToLowerInvariant(), @"[^a-z0-9\u4e00-\u9fff]+")
            .Where(x => x.Length >= 2)
            .Take(24)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void ApplySort()
    {
        var sortKey = SelectedSort?.Key ?? "composite";
        IEnumerable<OnlineSubtitleSearchResultViewModel> ordered = sortKey switch
        {
            "downloads" => _allResults
                .OrderByDescending(x => x.DownloadCount ?? -1)
                .ThenByDescending(x => x.MatchScore),
            "rating" => _allResults
                .OrderByDescending(x => x.Rating ?? -1d)
                .ThenByDescending(x => x.Votes ?? -1)
                .ThenByDescending(x => x.MatchScore),
            "uploaded" => _allResults
                .OrderByDescending(x => x.UploadedAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.MatchScore),
            "match" => _allResults
                .OrderByDescending(x => x.MatchScore)
                .ThenByDescending(x => x.DownloadCount ?? -1),
            _ => _allResults
                .OrderByDescending(x => x.CompositeScore)
                .ThenByDescending(x => x.MatchScore)
        };

        if (_isSortAscending)
        {
            ordered = ordered.Reverse();
        }

        Results.Clear();
        foreach (var result in ordered)
        {
            Results.Add(result);
        }

        RaiseResultsChanged();
    }

    private void RaiseResultsChanged()
    {
        OnPropertyChanged(nameof(HasResults));
    }

    private static string SanitizeKeyword(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length <= 160)
        {
            return trimmed;
        }

        return trimmed[..160];
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private void ToggleSortDirection()
    {
        _isSortAscending = !_isSortAscending;
        OnPropertyChanged(nameof(SortDirectionButtonToolTip));
        OnPropertyChanged(nameof(SortDirectionIconData));
        ApplySort();
    }

    private static string BuildFilterButtonText(string label, string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? label
            : $"{label}：{value}";
    }

    private static int GetLanguagePriority(string code)
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

    private static string LocalizeLanguageName(OpenSubtitlesLanguageOption language)
    {
        return language.Code.ToLowerInvariant() switch
        {
            "ab" => "阿布哈兹语",
            "af" => "南非语",
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
            "id" => "印尼语",
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

    private static string MapSearchStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Equals("No subtitles returned.", StringComparison.OrdinalIgnoreCase))
        {
            return "未找到匹配的在线字幕。";
        }

        if (message.Contains("API key", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenSubtitles API Key 未配置或不可用：请到设置页填写有效 API Key，并点击测试确认可用。";
        }

        if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || message.Contains("401", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenSubtitles 鉴权失败：请到设置页重新测试在线字幕配置，必要时重新填写 API Key 或账号密码。";
        }

        if (message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            || message.Contains("403", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenSubtitles 拒绝访问：当前 API Key 无效、无权限或已被禁用，请到设置页重新填写有效 API Key。";
        }

        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("quota", StringComparison.OrdinalIgnoreCase)
            || message.Contains("429", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenSubtitles 当前被限流或额度不足，请稍后重试。";
        }

        if (message.Contains("server", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenSubtitles 服务暂时不可用，请稍后重试。";
        }

        return message;
    }
}

public sealed record OnlineSubtitleSearchTypeOption(string Key, string DisplayName);

public sealed record OnlineSubtitleSortOption(string Key, string DisplayName);

public sealed class OnlineSubtitleSearchContext
{
    private const int SafeFileNameLimit = 140;

    public string DefaultLanguageCode { get; init; } = "zh-cn";

    public string DefaultType { get; init; } = "movie";

    public string InitialQuery { get; init; } = string.Empty;

    public string ContextSummary { get; init; } = string.Empty;

    public string SafeFileName { get; init; } = string.Empty;

    public string SafeFileNameWithoutExtension { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public string MovieImdbId { get; init; } = string.Empty;

    public int? MovieTmdbId { get; init; }

    public int? MovieReleaseYear { get; init; }

    public int? BindingMovieId { get; init; }

    public string SeriesImdbId { get; init; } = string.Empty;

    public int? SeriesTmdbId { get; init; }

    public int? BindingEpisodeId { get; init; }

    public int? BindingMediaFileId { get; init; }

    public int? SeasonNumber { get; init; }

    public int? EpisodeNumber { get; init; }

    public static OnlineSubtitleSearchContext FromPlayback(
        PlaybackSessionModel? session,
        PlaybackSourceItem? source,
        string defaultLanguageCode)
    {
        var safeFileName = BuildSafeFileName(source?.FileName);
        var safeFileNameWithoutExtension = StripExtension(safeFileName);
        if (session?.ContentType == PlaybackContentType.Episode)
        {
            var isRecognized = IsRecognized(session.SeasonIdentificationStatus);
            var initialQuery = isRecognized
                ? BuildEpisodeQuery(session.SeriesTitle, session.SeasonNumber, session.EpisodeNumber, session.EpisodeTitle)
                : BuildUnidentifiedEpisodeQuery(safeFileNameWithoutExtension, session.SeasonNumber, session.EpisodeNumber);

            return new OnlineSubtitleSearchContext
            {
                DefaultLanguageCode = NormalizeLanguage(defaultLanguageCode),
                DefaultType = "episode",
                InitialQuery = initialQuery,
                ContextSummary = "当前播放：电视剧集",
                SafeFileName = safeFileName,
                SafeFileNameWithoutExtension = safeFileNameWithoutExtension,
                FileSize = source?.FileSize ?? 0,
                SeriesTmdbId = session.SeriesTmdbId,
                BindingEpisodeId = isRecognized && session.EpisodeId is > 0 ? session.EpisodeId : null,
                BindingMediaFileId = isRecognized ? null : source?.MediaFileId,
                SeasonNumber = session.SeasonNumber > 0 ? session.SeasonNumber : null,
                EpisodeNumber = session.EpisodeNumber > 0 ? session.EpisodeNumber : null
            };
        }

        var recognizedMovie = session is not null && IsRecognized(session.MovieIdentificationStatus);
        return new OnlineSubtitleSearchContext
        {
            DefaultLanguageCode = NormalizeLanguage(defaultLanguageCode),
            DefaultType = "movie",
            InitialQuery = recognizedMovie
                ? BuildMovieQuery(session!.MovieTitle, session.MovieOriginalTitle, session.MovieReleaseYear, safeFileNameWithoutExtension)
                : safeFileNameWithoutExtension,
            ContextSummary = recognizedMovie ? "当前播放：电影" : "当前播放：未识别内容，按电影搜索",
            SafeFileName = safeFileName,
            SafeFileNameWithoutExtension = safeFileNameWithoutExtension,
            FileSize = source?.FileSize ?? 0,
            MovieImdbId = session?.MovieImdbId ?? string.Empty,
            MovieTmdbId = session?.MovieTmdbId,
            MovieReleaseYear = session?.MovieReleaseYear,
            BindingMovieId = recognizedMovie && session!.MovieId > 0 ? session.MovieId : null,
            BindingMediaFileId = recognizedMovie ? null : source?.MediaFileId
        };
    }

    private static bool IsRecognized(IdentificationStatus status)
    {
        return status is IdentificationStatus.Matched or IdentificationStatus.ManualConfirmed;
    }

    private static string BuildMovieQuery(string title, string originalTitle, int? year, string fallback)
    {
        var selectedTitle = FirstNonEmpty(title, originalTitle, fallback);
        if (year is > 0 && !string.IsNullOrWhiteSpace(selectedTitle))
        {
            return $"{selectedTitle} {year.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        return selectedTitle;
    }

    private static string BuildEpisodeQuery(string seriesTitle, int seasonNumber, int episodeNumber, string episodeTitle)
    {
        var episodeCode = seasonNumber > 0 && episodeNumber > 0
            ? $"S{seasonNumber:D2}E{episodeNumber:D2}"
            : episodeNumber > 0
                ? $"E{episodeNumber:D2}"
                : string.Empty;
        return string.Join(
            " ",
            new[] { seriesTitle, episodeCode, episodeTitle }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string BuildUnidentifiedEpisodeQuery(string safeFileName, int seasonNumber, int episodeNumber)
    {
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return seasonNumber > 0 && episodeNumber > 0
                ? $"S{seasonNumber:D2}E{episodeNumber:D2}"
                : string.Empty;
        }

        return safeFileName;
    }

    private static string BuildSafeFileName(string? value)
    {
        var safeName = Path.GetFileName(value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "media";
        }

        safeName = Regex.Replace(safeName, @"[\r\n\t]+", " ");
        safeName = Regex.Replace(safeName, @"\s{2,}", " ").Trim();
        return safeName.Length <= SafeFileNameLimit
            ? safeName
            : safeName[..SafeFileNameLimit];
    }

    private static string StripExtension(string fileName)
    {
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrWhiteSpace(withoutExtension)
            ? fileName
            : withoutExtension;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string NormalizeLanguage(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "zh-cn" : value.Trim();
    }
}

public sealed class OnlineSubtitleSearchResultViewModel : ViewModelBase
{
    private bool _isDownloading;
    private bool _isDownloaded;
    private string _downloadStatus = string.Empty;

    public OnlineSubtitleSearchResultViewModel(
        OpenSubtitlesSearchItem item,
        int matchScore,
        string requestedLanguageCode,
        Func<OnlineSubtitleSearchResultViewModel, Task> downloadAsync)
    {
        SubtitleId = item.SubtitleId;
        ProviderFileId = item.ProviderFileId;
        LanguageCode = item.LanguageCode;
        LanguageName = string.IsNullOrWhiteSpace(item.LanguageName) ? item.LanguageCode : item.LanguageName;
        ReleaseName = FirstNonEmpty(item.ReleaseName, item.FileName, item.FeatureTitle, "未知 release");
        FileName = item.FileName;
        DownloadCount = item.DownloadCount;
        Rating = item.Rating;
        Votes = item.Votes;
        IsHearingImpaired = item.IsHearingImpaired;
        IsMachineTranslated = item.IsMachineTranslated;
        IsAiTranslated = item.IsAiTranslated;
        IsTrustedUploader = item.IsTrustedUploader;
        Fps = item.Fps;
        UploadedAt = item.UploadedAt;
        FeatureTitle = item.FeatureTitle;
        FeatureYear = item.FeatureYear;
        SeasonNumber = item.SeasonNumber;
        EpisodeNumber = item.EpisodeNumber;
        UploaderName = item.UploaderName;
        MatchScore = matchScore;
        CompositeScore = BuildCompositeScore(item, matchScore, requestedLanguageCode);
        Tags = BuildTags(item, matchScore);
        RawMetadataJson = item.RawMetadataJson;
        DownloadCommand = new AsyncRelayCommand(_ => downloadAsync(this), _ => CanDownload);
    }

    public string SubtitleId { get; }

    public string ProviderFileId { get; }

    public string LanguageCode { get; }

    public string LanguageName { get; }

    public string ReleaseName { get; }

    public string FileName { get; }

    public int? DownloadCount { get; }

    public double? Rating { get; }

    public int? Votes { get; }

    public bool IsHearingImpaired { get; }

    public bool IsMachineTranslated { get; }

    public bool IsAiTranslated { get; }

    public bool IsTrustedUploader { get; }

    public double? Fps { get; }

    public DateTime? UploadedAt { get; }

    public string FeatureTitle { get; }

    public int? FeatureYear { get; }

    public int? SeasonNumber { get; }

    public int? EpisodeNumber { get; }

    public string UploaderName { get; }

    public int MatchScore { get; }

    public double CompositeScore { get; }

    public IReadOnlyList<string> Tags { get; }

    public string RawMetadataJson { get; }

    public AsyncRelayCommand DownloadCommand { get; }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(DownloadButtonText));
                DownloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set
        {
            if (SetProperty(ref _isDownloaded, value))
            {
                OnPropertyChanged(nameof(DownloadButtonText));
                DownloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string DownloadStatus
    {
        get => _downloadStatus;
        set
        {
            if (SetProperty(ref _downloadStatus, value))
            {
                OnPropertyChanged(nameof(IsDownloadStatusVisible));
            }
        }
    }

    public bool IsDownloadStatusVisible => !string.IsNullOrWhiteSpace(DownloadStatus);

    public bool CanDownload => !IsDownloading && !IsDownloaded && !string.IsNullOrWhiteSpace(ProviderFileId);

    public string DownloadButtonText => IsDownloaded
        ? "已下载"
        : IsDownloading
            ? "下载中..."
            : "下载";

    public string PrimaryText => ReleaseName;

    public string UploaderDisplayText => FirstNonEmpty(UploaderName, "未知");

    public string UploadedDateDisplayText => UploadedAt.HasValue
        ? UploadedAt.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.CurrentCulture)
        : "未知";

    public string RatingVoteDisplayText
    {
        get
        {
            var rating = Rating.HasValue
                ? Rating.Value.ToString("0.0", CultureInfo.InvariantCulture)
                : "未知";
            var votes = Votes.HasValue
                ? Votes.Value.ToString("N0", CultureInfo.InvariantCulture)
                : "未知";
            return $"{rating}（{votes}票）";
        }
    }

    public string LanguageDisplayText => LocalizeLanguageName(LanguageCode, LanguageName);

    public string DownloadCountDisplayText => DownloadCount.HasValue
        ? DownloadCount.Value.ToString("N0", CultureInfo.InvariantCulture)
        : "未知";

    public string SecondaryText
    {
        get
        {
            var feature = string.Join(
                " ",
                new[]
                {
                    FeatureTitle,
                    FeatureYear.HasValue ? $"({FeatureYear.Value.ToString(CultureInfo.InvariantCulture)})" : string.Empty,
                    SeasonNumber.HasValue || EpisodeNumber.HasValue
                        ? $"S{(SeasonNumber ?? 0):D2}E{(EpisodeNumber ?? 0):D2}"
                        : string.Empty
                }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var parts = new[] { FileName, feature, string.IsNullOrWhiteSpace(UploaderName) ? string.Empty : $"上传者：{UploaderName}" }
                .Where(x => !string.IsNullOrWhiteSpace(x));
            return string.Join(" · ", parts);
        }
    }

    public string MetricsText
    {
        get
        {
            var downloads = DownloadCount.HasValue
                ? DownloadCount.Value.ToString("N0", CultureInfo.InvariantCulture)
                : "未知";
            var rating = Rating.HasValue
                ? Rating.Value.ToString("0.0", CultureInfo.InvariantCulture)
                : "未知";
            var votes = Votes.HasValue
                ? Votes.Value.ToString(CultureInfo.InvariantCulture)
                : "未知";
            var fps = Fps.HasValue
                ? Fps.Value.ToString("0.###", CultureInfo.InvariantCulture)
                : "未知";
            return $"下载 {downloads} · 评分 {rating} / votes {votes} · FPS {fps}";
        }
    }

    public string UploadedAtText => UploadedAt.HasValue
        ? UploadedAt.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.CurrentCulture)
        : "上传时间未知";

    public string MatchScoreText => $"匹配度：{MatchScore}";

    private static string LocalizeLanguageName(string code, string fallback)
    {
        return code.ToLowerInvariant() switch
        {
            "ze" => "中英双语",
            "zh-ca" => "粤语中文",
            "zh-cn" => "简体中文",
            "zh-tw" => "繁体中文",
            "en" => "英语",
            "ja" => "日语",
            "ko" => "韩语",
            "fr" => "法语",
            "de" => "德语",
            "es" or "sp" or "ea" => "西班牙语",
            "it" => "意大利语",
            "ru" => "俄语",
            "pt-pt" => "葡萄牙语",
            "pt-br" => "葡萄牙语（巴西）",
            "ar" => "阿拉伯语",
            "hi" => "印地语",
            "th" => "泰语",
            "vi" => "越南语",
            "id" => "印尼语",
            "tr" => "土耳其语",
            "pl" => "波兰语",
            "nl" => "荷兰语",
            "sv" => "瑞典语",
            "da" => "丹麦语",
            "fi" => "芬兰语",
            "no" => "挪威语",
            _ => string.IsNullOrWhiteSpace(fallback) ? code : fallback
        };
    }

    private static double BuildCompositeScore(OpenSubtitlesSearchItem item, int matchScore, string requestedLanguageCode)
    {
        var score = matchScore * 10d;
        if (item.LanguageCode.Equals(requestedLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            score += 150d;
        }

        score += Math.Min(120d, (item.DownloadCount ?? 0) / 20d);
        score += Math.Min(80d, (item.Rating ?? 0d) * 8d);
        score += item.IsTrustedUploader ? 50d : 0d;
        score += item.UploadedAt.HasValue
            ? Math.Max(0d, 30d - (DateTime.UtcNow - item.UploadedAt.Value.ToUniversalTime()).TotalDays / 90d)
            : 0d;
        return score;
    }

    private static IReadOnlyList<string> BuildTags(OpenSubtitlesSearchItem item, int matchScore)
    {
        var tags = new List<string>();
        AddTag(tags, string.IsNullOrWhiteSpace(item.LanguageName) ? item.LanguageCode : item.LanguageName);
        AddTag(tags, $"匹配 {matchScore}");
        if (item.IsHearingImpaired)
        {
            AddTag(tags, "听障");
        }

        if (item.IsMachineTranslated)
        {
            AddTag(tags, "机器翻译");
        }

        if (item.IsAiTranslated)
        {
            AddTag(tags, "AI 翻译");
        }

        if (item.IsTrustedUploader)
        {
            AddTag(tags, "可信上传者");
        }

        if (item.SeasonNumber.HasValue || item.EpisodeNumber.HasValue)
        {
            AddTag(tags, $"S{(item.SeasonNumber ?? 0):D2}E{(item.EpisodeNumber ?? 0):D2}");
        }

        return tags;
    }

    private static void AddTag(List<string> tags, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            tags.Add(value);
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }
}

public sealed class OnlineSubtitleMenuItemViewModel
{
    public int BindingId { get; init; }

    public int? MovieId { get; init; }

    public int? EpisodeId { get; init; }

    public int? MediaFileId { get; init; }

    public string TargetKind { get; init; } = string.Empty;

    public int TemporaryId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string ToolTip { get; init; } = string.Empty;

    public bool HasCacheFile { get; init; }

    public bool IsTemporary { get; init; }

    public string SubtitleUniqueKey { get; init; } = string.Empty;

    public string CacheRelativePath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;
}

public sealed class OnlineSubtitlePlaybackRequest
{
    public OnlineSubtitleBindingListItem? Binding { get; init; }

    public string AbsolutePath { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string ProviderFileId { get; init; } = string.Empty;
}

public sealed record OnlineSubtitlePlaybackApplyResult(bool Succeeded, string Message);
