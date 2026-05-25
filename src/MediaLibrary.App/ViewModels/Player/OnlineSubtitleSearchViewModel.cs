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
    private readonly IOpenSubtitlesClientService _openSubtitlesClientService;
    private readonly ISettingsService _settingsService;
    private readonly OnlineSubtitleSearchContext _context;
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

    public OnlineSubtitleSearchViewModel(
        IOpenSubtitlesClientService openSubtitlesClientService,
        ISettingsService settingsService,
        OnlineSubtitleSearchContext context)
    {
        _openSubtitlesClientService = openSubtitlesClientService;
        _settingsService = settingsService;
        _context = context;

        Languages = _openSubtitlesClientService.SupportedLanguages;
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
        _quotaHint = "下载额度将在下载时根据 OpenSubtitles 返回结果提示。";

        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsLoading);
    }

    public IReadOnlyList<OpenSubtitlesLanguageOption> Languages { get; }

    public IReadOnlyList<OnlineSubtitleSearchTypeOption> Types { get; }

    public IReadOnlyList<OnlineSubtitleSortOption> SortOptions { get; }

    public ObservableCollection<OnlineSubtitleSearchResultViewModel> Results { get; } = [];

    public AsyncRelayCommand SearchCommand { get; }

    public string WindowTitle => "搜索在线字幕";

    public string ContextSummary => _context.ContextSummary;

    public string SafeFileNameHint => _context.SafeFileName;

    public OpenSubtitlesLanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public OnlineSubtitleSearchTypeOption? SelectedType
    {
        get => _selectedType;
        set => SetProperty(ref _selectedType, value);
    }

    public OnlineSubtitleSortOption? SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (SetProperty(ref _selectedSort, value))
            {
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
            foreach (var item in page.Results)
            {
                _allResults.Add(new OnlineSubtitleSearchResultViewModel(
                    item,
                    ComputeMatchScore(item, request),
                    SelectedLanguage?.Code ?? "zh-cn"));
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
            OrderDirection = string.IsNullOrWhiteSpace(orderBy) ? string.Empty : "desc"
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
        var ordered = sortKey switch
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

    public string SeriesImdbId { get; init; } = string.Empty;

    public int? SeriesTmdbId { get; init; }

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
            MovieReleaseYear = session?.MovieReleaseYear
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

public sealed class OnlineSubtitleSearchResultViewModel
{
    public OnlineSubtitleSearchResultViewModel(OpenSubtitlesSearchItem item, int matchScore, string requestedLanguageCode)
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

    public string PrimaryText => ReleaseName;

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

    public string MatchScoreText => $"匹配度 {MatchScore}";

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

    public string DisplayName { get; init; } = string.Empty;

    public string ToolTip { get; init; } = string.Empty;
}
