using System.Diagnostics;
using System.Text.Json;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class WatchProfileService : IWatchProfileService
{
    private const string ProfileKind = "profile";
    private const string GlobalScopeKey = "global";
    private const int CurrentProfileSchemaVersion = 2;
    private const string CurrentPromptVersion = "wi-profile-range-quadrant-v5";

    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromDays(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] PersonaTypes =
    [
        "情绪沉浸者",
        "悬疑解谜者",
        "类型探索家",
        "经典收藏家",
        "治愈陪伴型",
        "高分严选派",
        "作者导演迷",
        "科幻幻想旅人",
        "现实观察者",
        "动作爽片玩家",
        "文艺审美家",
        "多元杂食者",
        "黑色幽默爱好者",
        "浪漫幻想派",
        "暗黑猎奇者",
        "家庭温情派",
        "历史史诗控",
        "动画想象派",
        "犯罪人性派",
        "轻松下饭派"
    ];

    private static readonly string[] PersonaTypeDefinitions =
    [
        "1. 情绪沉浸者：关键词是强情绪、共鸣、后劲、哭点、心理余韵。边界是重在被情绪击中，不是单纯喜欢美学或现实题材。",
        "2. 悬疑解谜者：关键词是谜团、反转、推理、信息差、真相揭开。边界是重在解谜过程，不是单纯犯罪或人性灰度。",
        "3. 类型探索家：关键词是陌生类型、新国家、新年代、新风格、主动尝试。边界是重在探索未知，不是单纯什么都能看。",
        "4. 经典收藏家：关键词是影史经典、老片、代表作、长期价值、反复回味。边界是重在经典价值和收藏感，不是单纯高分。",
        "5. 治愈陪伴型：关键词是温暖、修复、陪伴、安心、柔软。边界是重在情绪被照顾，不是单纯轻松搞笑。",
        "6. 高分严选派：关键词是评分、口碑、奖项、质量筛选、避雷。边界是重在先看质量背书，不是收藏经典或导演作者。",
        "7. 作者导演迷：关键词是导演风格、镜头语言、作者表达、个人印记。边界是重在创作者表达，不是单纯文艺审美。",
        "8. 科幻幻想旅人：关键词是科幻、奇幻、世界观、设定、宏大想象。边界是重在设定和世界观，不是动画媒介本身。",
        "9. 现实观察者：关键词是现实主义、社会议题、生活质感、普通人、人间观察。边界是重在看见真实世界，不是情绪沉浸或犯罪黑暗面。",
        "10. 动作爽片玩家：关键词是快节奏、动作、刺激、爽感、视觉冲击。边界是重在直接快感和能量释放，不是惊悚猎奇。",
        "11. 文艺审美家：关键词是影像美学、氛围、构图、留白、诗意、慢节奏。边界是重在审美和形式感；和情绪沉浸者区分时看影像气质是否强于情绪冲击；和作者导演迷区分时看最终画面和氛围体验是否强于导演表达。",
        "12. 多元杂食者：关键词是口味宽、兼容度高、类型不挑、随缘观看、覆盖面广。边界是重在什么都能吃；和类型探索家区分时，多元杂食者不一定主动追求新奇。",
        "13. 黑色幽默爱好者：关键词是荒诞、讽刺、冷幽默、反差、荒谬人生、怪诞喜剧。边界是重在笑里带刺；不同于轻松下饭派的低负担快乐，也不同于暗黑猎奇者对阴暗和异常的追求。",
        "14. 浪漫幻想派：关键词是爱情、青春、命运感、遗憾、心动、理想化关系。边界是重在关系中的浪漫和情感想象；比情绪沉浸者更集中在爱情、青春、遗憾和命运感。",
        "15. 暗黑猎奇者：关键词是惊悚、恐怖、怪诞、压抑、边缘题材、异常体验。边界是重在安全距离内探索黑暗和异常；不同于犯罪人性派的道德人性关注，也不同于悬疑解谜者对真相的关注。",
        "16. 家庭温情派：关键词是亲情、成长、家庭关系、代际关系、日常牵绊。边界是重在家庭和成长关系；比治愈陪伴型更聚焦家庭、亲情、成长与和解。",
        "17. 历史史诗控：关键词是历史、战争、传记、时代洪流、宏大叙事、文明感。边界是重在时间尺度和时代重量；不同于经典收藏家的影史地位和收藏价值。",
        "18. 动画想象派：关键词是动画媒介、手绘感、奇思妙想、童心、色彩、幻想表达。边界是重在动画作为表达方式；不同于科幻幻想旅人的世界观和设定偏好。",
        "19. 犯罪人性派：关键词是犯罪、道德困境、人性灰度、社会黑暗面、角色动机。边界是重在犯罪背后的人性和道德；不同于悬疑解谜者的推理真相，也比现实观察者更聚焦犯罪和灰色人性。",
        "20. 轻松下饭派：关键词是喜剧、轻松、短平快、低负担、随手看、放松娱乐。边界是重在低心理成本；不同于治愈陪伴型的温柔修复，也不同于动作爽片玩家的刺激爽感。"
    ];

    private static readonly string[] PersonaSelectionRules =
    [
        "如果解谜过程强于犯罪题材，选悬疑解谜者。",
        "如果道德灰度和人性主题强于解谜，选犯罪人性派。",
        "如果情绪冲击强于画面审美，选情绪沉浸者。",
        "如果影像氛围强于情绪冲击，选文艺审美家。",
        "如果主动探索陌生类型，选类型探索家。",
        "如果口味广泛但无明确探索倾向，选多元杂食者。",
        "如果情绪修复和温暖陪伴强，选治愈陪伴型。",
        "如果低负担娱乐和随手观看强，选轻松下饭派。",
        "如果世界观和设定强，选科幻幻想旅人。",
        "如果动画媒介和童心表达强，选动画想象派。",
        "如果历史时代重量强，选历史史诗控。",
        "如果影史地位和收藏价值强，选经典收藏家。"
    ];

    private static readonly string[] DnaGenes =
    [
        "类型基因",
        "情绪基因",
        "场景基因",
        "叙事基因",
        "节奏基因",
        "探索基因"
    ];

    private static readonly string[] NarrativeTags =
    [
        "线性叙事",
        "多线叙事",
        "反转叙事",
        "开放结局",
        "成长叙事",
        "公路叙事",
        "群像叙事",
        "心理叙事",
        "悬念推进",
        "章节叙事",
        "回忆叙事",
        "非线性叙事",
        "命运交织",
        "日常切片",
        "史诗叙事",
        "寓言叙事",
        "黑色幽默",
        "现实观察",
        "情绪流动",
        "高概念设定"
    ];

    private readonly IWatchProfileInputService _inputService;
    private readonly IWatchInsightCacheService _cacheService;
    private readonly IAiService _aiService;

    public WatchProfileService(
        IWatchProfileInputService inputService,
        IWatchInsightCacheService cacheService,
        IAiService aiService)
    {
        _inputService = inputService;
        _cacheService = cacheService;
        _aiService = aiService;
    }

    public async Task<WatchProfileSnapshot> GetProfileAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var input = await _inputService.BuildProfileInputAsync(cancellationToken);
        if (!input.CanGenerateProfile)
        {
            return CreateInsufficientSnapshot(input);
        }

        var nowUtc = DateTime.UtcNow;
        var cache = await _cacheService.GetAsync(ProfileKind, GlobalScopeKey, cancellationToken);
        WatchProfileSnapshot? cachedProfile = null;
        var deserializeFailed = false;
        if (cache is not null)
        {
            deserializeFailed = !TryDeserializeProfile(cache.PayloadJson, out cachedProfile);
        }

        if (forceRefresh && cache is not null && !deserializeFailed && cachedProfile is not null)
        {
            var missReason = GetCacheMissReason(cache, cachedProfile, input.SourceFingerprint);
            if (missReason is null)
            {
                NormalizeProfile(cachedProfile, input, loadedFromCache: true);
                cachedProfile.IsUnchanged = true;
                cachedProfile.StatusMessage = "画像数据没有变化，已显示最新画像。";
                AddUnique(cachedProfile.WarningMessages, cachedProfile.StatusMessage);
                AddUnique(cachedProfile.Meta.WarningMessages, cachedProfile.StatusMessage);
                Log($"watch-profile-manual-refresh-unchanged fingerprint={ShortFingerprint(input.SourceFingerprint)} promptVersion={CurrentPromptVersion}");
                Log($"watch-profile-ai-skipped reason=fingerprint-and-prompt-version-unchanged fingerprint={ShortFingerprint(input.SourceFingerprint)}");
                return cachedProfile;
            }

            if (string.Equals(missReason, "prompt-version-changed", StringComparison.Ordinal))
            {
                Log($"watch-profile-prompt-version old={FormatPromptVersion(cachedProfile)} current={CurrentPromptVersion}");
            }

            Log(
                "watch-profile-manual-refresh-regenerate "
                + $"reason={missReason} fingerprint={ShortFingerprint(input.SourceFingerprint)}");
        }

        if (!forceRefresh && cache is not null && !deserializeFailed && cachedProfile is not null)
        {
            var missReason = GetCacheMissReason(cache, cachedProfile, input.SourceFingerprint);
            if (missReason is null)
            {
                NormalizeProfile(cachedProfile, input, loadedFromCache: true);
                Log($"watch-profile-cache-hit fingerprint={ShortFingerprint(input.SourceFingerprint)}");
                return cachedProfile;
            }

            if (string.Equals(missReason, "prompt-version-changed", StringComparison.Ordinal))
            {
                NormalizeProfile(
                    cachedProfile,
                    input,
                    loadedFromCache: true,
                    preserveSourceFingerprint: true,
                    preservePromptVersion: true);
                cachedProfile.StatusMessage = "画像生成规则已更新，可手动刷新生成新版画像。";
                AddUnique(cachedProfile.WarningMessages, cachedProfile.StatusMessage);
                AddUnique(cachedProfile.Meta.WarningMessages, cachedProfile.StatusMessage);
                Log($"watch-profile-prompt-version old={FormatPromptVersion(cachedProfile)} current={CurrentPromptVersion}");
                return cachedProfile;
            }

            if (ShouldSkipAutoRefresh(cache, nowUtc))
            {
                NormalizeProfile(cachedProfile, input, loadedFromCache: true, preserveSourceFingerprint: true);
                cachedProfile.StatusMessage = "画像数据已变化，但距离上次自动刷新不足 1 天；将在下次自动刷新周期更新。";
                AddUnique(cachedProfile.WarningMessages, cachedProfile.StatusMessage);
                AddUnique(cachedProfile.Meta.WarningMessages, cachedProfile.StatusMessage);
                Log(
                    "watch-profile-auto-refresh-skipped "
                    + $"reason=within-1-day fingerprint={ShortFingerprint(input.SourceFingerprint)}");
                return cachedProfile;
            }

            Log($"watch-profile-cache-miss reason={missReason} fingerprint={ShortFingerprint(input.SourceFingerprint)}");
        }
        else if (!forceRefresh)
        {
            var reason = cache is null ? "missing" : "deserialize-failed";
            Log($"watch-profile-cache-miss reason={reason} fingerprint={ShortFingerprint(input.SourceFingerprint)}");
        }
        else
        {
            var reason = cache is null
                ? "missing-cache"
                : deserializeFailed ? "cache-invalid" : "manual";
            Log($"watch-profile-cache-miss reason={reason} fingerprint={ShortFingerprint(input.SourceFingerprint)}");
            if (forceRefresh)
            {
                Log($"watch-profile-manual-refresh-regenerate reason={reason} fingerprint={ShortFingerprint(input.SourceFingerprint)}");
            }
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            Log($"watch-profile-ai-start sampleMovies={input.SignalMovieCount}");
            var response = await _aiService.GenerateTextAsync(
                BuildSystemPrompt(),
                BuildUserPrompt(input),
                cancellationToken);
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new InvalidOperationException("AI profile response was empty or AI settings are incomplete.");
            }

            var profile = ParseProfile(response);
            var invalidPersonaType = !IsPersonaTypeValid(profile.Persona?.Type);
            if (invalidPersonaType)
            {
                profile.Meta ??= new WatchProfileMeta();
                profile.WarningMessages ??= [];
                profile.Meta.WarningMessages ??= [];
                var regenerated = await TryRegenerateFallbackPersonaAsync(profile, input, cancellationToken);
                if (!regenerated)
                {
                    ApplyFixedPersonaFallback(profile);
                }

                AddUnique(profile.WarningMessages, "AI 返回非法人格类型，已回退为多元杂食者。");
                AddUnique(profile.Meta.WarningMessages, "AI 返回非法人格类型，已回退为多元杂食者。");
            }

            NormalizeProfile(profile, input, loadedFromCache: false);
            stopwatch.Stop();
            Log($"watch-profile-ai-complete elapsedMs={stopwatch.ElapsedMilliseconds}");

            var payloadJson = JsonSerializer.Serialize(profile, JsonOptions);
            var cacheStopwatch = Stopwatch.StartNew();
            await _cacheService.UpsertAsync(
                ProfileKind,
                GlobalScopeKey,
                payloadJson,
                input.SourceFingerprint,
                expiresAtUtc: null,
                isManualRefresh: forceRefresh,
                cancellationToken);
            cacheStopwatch.Stop();
            Log($"watch-profile-cache-upsert elapsedMs={cacheStopwatch.ElapsedMilliseconds}");
            return profile;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var sanitizedError = AiPerfDiagnostics.SanitizeMessage(exception.Message);
            await _cacheService.SetErrorAsync(ProfileKind, GlobalScopeKey, sanitizedError, cancellationToken);
            Log($"watch-profile-ai-failed errorType={exception.GetType().Name}");

            if (cachedProfile is not null)
            {
                NormalizeProfile(cachedProfile, input, loadedFromCache: true, preserveSourceFingerprint: true);
                cachedProfile.WasAiCalled = true;
                cachedProfile.StatusMessage = "画像刷新失败，已显示上一次画像缓存。";
                cachedProfile.WarningMessages.Add("画像刷新失败，已保留上一次画像缓存。");
                cachedProfile.WarningMessages.Add($"错误类型：{exception.GetType().Name}");
                cachedProfile.Meta.WarningMessages.Add("画像刷新失败，已保留上一次画像缓存。");
                return cachedProfile;
            }

            return CreateErrorSnapshot(input, $"画像生成失败：{exception.GetType().Name}");
        }
    }

    private static string? GetCacheMissReason(
        WatchInsightCacheSnapshot cache,
        WatchProfileSnapshot profile,
        string fingerprint)
    {
        if (cache.IsStale)
        {
            return "stale";
        }

        if (!string.Equals(cache.SourceFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return "fingerprint-changed";
        }

        if (profile.Meta.ProfileSchemaVersion != CurrentProfileSchemaVersion
            || !string.Equals(profile.Meta.PromptVersion, CurrentPromptVersion, StringComparison.Ordinal))
        {
            return "prompt-version-changed";
        }

        return null;
    }

    private static bool ShouldSkipAutoRefresh(WatchInsightCacheSnapshot cache, DateTime nowUtc)
    {
        if (!cache.LastAutoRefreshAtUtc.HasValue)
        {
            return false;
        }

        return nowUtc - cache.LastAutoRefreshAtUtc.Value < AutoRefreshInterval;
    }

    private static bool TryDeserializeProfile(string payloadJson, out WatchProfileSnapshot? profile)
    {
        try
        {
            profile = JsonSerializer.Deserialize<WatchProfileSnapshot>(payloadJson, JsonOptions);
            return profile is not null;
        }
        catch (JsonException)
        {
            profile = null;
            return false;
        }
    }

    private static WatchProfileSnapshot ParseProfile(string text)
    {
        var json = ExtractJsonObject(text);
        ValidateQuadrantPayload(json);
        var profile = JsonSerializer.Deserialize<WatchProfileSnapshot>(json, JsonOptions);
        if (profile is null)
        {
            throw new JsonException("AI profile JSON could not be parsed.");
        }

        return profile;
    }

    private static void ValidateQuadrantPayload(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("quadrant", out var quadrant))
        {
            Log("watch-profile-quadrant-missing error=missing-quadrant");
            throw new JsonException("AI profile JSON is missing quadrant.");
        }

        if (!TryReadQuadrantNumber(quadrant, "xAxisScore")
            || !TryReadQuadrantNumber(quadrant, "yAxisScore"))
        {
            Log("watch-profile-quadrant-missing error=missing-or-invalid-axis");
            throw new JsonException("AI profile JSON is missing valid quadrant x/y scores.");
        }
    }

    private static bool TryReadQuadrantNumber(JsonElement quadrant, string propertyName)
    {
        if (!quadrant.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _);
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new JsonException("AI profile response does not contain a JSON object.");
        }

        return text[start..(end + 1)];
    }

    private static void NormalizeProfile(
        WatchProfileSnapshot profile,
        WatchProfileInputSnapshot input,
        bool loadedFromCache,
        bool preserveSourceFingerprint = false,
        bool preservePromptVersion = false)
    {
        var warnings = new List<string>();
        profile.Meta ??= new WatchProfileMeta();
        profile.Summary ??= new WatchProfileSummary();
        profile.Persona ??= new WatchProfilePersona();
        profile.Quadrant ??= new WatchProfileQuadrant();
        profile.WatchVsLike ??= new WatchProfileWatchVsLike();
        profile.Likes ??= new WatchProfileLikes();
        profile.Dislikes ??= new WatchProfileDislikes();
        profile.FuturePreference ??= new WatchProfileFuturePreference();
        profile.DNA ??= [];
        profile.Caveats ??= [];
        profile.WarningMessages ??= [];
        profile.Meta.WarningMessages ??= [];

        if (!loadedFromCache || profile.Meta.GeneratedAtUtc == default)
        {
            profile.Meta.GeneratedAtUtc = DateTime.UtcNow;
        }

        if (!preserveSourceFingerprint)
        {
            profile.Meta.SourceFingerprint = input.SourceFingerprint;
        }

        if (!preservePromptVersion)
        {
            profile.Meta.ProfileSchemaVersion = CurrentProfileSchemaVersion;
            profile.Meta.PromptVersion = CurrentPromptVersion;
        }

        profile.Meta.SignalMovieCount = input.SignalMovieCount;
        profile.Meta.Confidence = Clamp(profile.Meta.Confidence == 0 ? 60 : profile.Meta.Confidence, 0, 100);
        profile.LoadedFromCache = loadedFromCache;
        profile.IsCacheHit = loadedFromCache;
        profile.WasAiCalled = !loadedFromCache;
        profile.IsUnchanged = false;
        if (string.IsNullOrWhiteSpace(profile.StatusMessage))
        {
            profile.StatusMessage = loadedFromCache ? "已显示缓存画像。" : "画像已生成。";
        }

        profile.HasProfile = true;
        profile.CanGenerateProfile = true;
        profile.InsufficientReason = string.Empty;
        profile.ErrorMessage = string.Empty;

        if (!PersonaTypes.Contains(profile.Persona.Type, StringComparer.Ordinal))
        {
            profile.Persona.Type = "多元杂食者";
            warnings.Add("AI 返回了未知人格类型，已回退为多元杂食者。");
        }

        if (string.IsNullOrWhiteSpace(profile.Persona.Title))
        {
            profile.Persona.Title = profile.Persona.Type;
        }

        profile.Persona.Confidence = Clamp(profile.Persona.Confidence == 0 ? profile.Meta.Confidence : profile.Persona.Confidence, 0, 100);
        profile.DNA = NormalizeDna(profile.DNA, warnings);
        profile.Quadrant.XAxisScore = Clamp(profile.Quadrant.XAxisScore, -100, 100);
        profile.Quadrant.YAxisScore = Clamp(profile.Quadrant.YAxisScore, -100, 100);
        if (string.IsNullOrWhiteSpace(profile.Quadrant.QuadrantName))
        {
            profile.Quadrant.QuadrantName = BuildQuadrantName(profile.Quadrant.XAxisScore, profile.Quadrant.YAxisScore);
        }

        if (string.IsNullOrWhiteSpace(profile.Quadrant.Description))
        {
            warnings.Add("AI 未提供口味象限解释。");
        }

        FillLocalDefaults(profile, input);
        NormalizeProfileText(profile);
        AddUnique(profile.Caveats, "语言字段当前沿用现有 Language 字段，可能不是 TMDB original_language。");
        foreach (var warning in input.WarningMessages.Concat(warnings))
        {
            AddUnique(profile.WarningMessages, warning);
            AddUnique(profile.Meta.WarningMessages, warning);
        }
    }

    private static List<WatchProfileDnaGene> NormalizeDna(
        IEnumerable<WatchProfileDnaGene> source,
        ICollection<string> warnings)
    {
        var sourceByGene = source
            .Where(x => !string.IsNullOrWhiteSpace(x.Gene))
            .GroupBy(x => x.Gene.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var normalized = new List<WatchProfileDnaGene>();
        foreach (var geneName in DnaGenes)
        {
            if (!sourceByGene.TryGetValue(geneName, out var gene))
            {
                gene = new WatchProfileDnaGene
                {
                    Gene = geneName,
                    Label = "数据不足",
                    Description = "该基因需要更多标签或观看行为后再生成。",
                    Confidence = 0
                };
                warnings.Add($"AI 未返回 {geneName}，已补为空状态。");
            }

            gene.Gene = geneName;
            gene.Tags ??= [];
            if (gene.Tags.Count == 0)
            {
                gene.Tags = SplitTags(gene.Label).ToList();
            }

            gene.Tags = geneName == "叙事基因"
                ? NormalizeNarrativeTags(gene.Tags, warnings)
                : NormalizeCommonDnaTags(gene.Tags);

            if (string.IsNullOrWhiteSpace(gene.Label) && gene.Tags.Count > 0)
            {
                gene.Label = string.Join("、", gene.Tags.Take(3));
            }

            gene.Score = Clamp(gene.Score, 0, 100);
            gene.Confidence = Clamp(gene.Confidence, 0, 100);
            if (string.IsNullOrWhiteSpace(gene.Description) && IsProgressDnaGene(gene.Gene))
            {
                warnings.Add($"AI 未返回{gene.Gene}描述，已保留为空，避免伪造画像文案。");
            }

            gene.Description = NormalizeDnaDescription(gene);
            normalized.Add(gene);
        }

        return normalized;
    }

    private static void NormalizeProfileText(WatchProfileSnapshot profile)
    {
        var originalKeywordCount = profile.Summary.Keywords.Count;
        profile.Summary.Keywords = NormalizeSummaryKeywords(profile.Summary.Keywords);
        if (originalKeywordCount != profile.Summary.Keywords.Count)
        {
            Log($"watch-profile-text-dedup-applied field=summary.keywords before={originalKeywordCount} after={profile.Summary.Keywords.Count}");
        }

        profile.Summary.Text = NormalizeProfileSentence(profile.Summary.Text, maxLength: 420);
        profile.Persona.Description = NormalizeProfileSentence(profile.Persona.Description);
        profile.WatchVsLike.Conclusion = NormalizeProfileSentence(profile.WatchVsLike.Conclusion);
    }

    private static List<string> NormalizeSummaryKeywords(IEnumerable<string> keywords)
    {
        var result = new List<string>();
        foreach (var keyword in keywords
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Trim()))
        {
            if (result.Any(existing => AreNearDuplicateKeywords(existing, keyword)))
            {
                continue;
            }

            result.Add(keyword);
            if (result.Count >= 6)
            {
                break;
            }
        }

        return result;
    }

    private static bool AreNearDuplicateKeywords(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (left.Contains(right, StringComparison.OrdinalIgnoreCase)
            || right.Contains(left, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string[] semanticRoots =
        [
            "情绪",
            "沉浸",
            "悬疑",
            "犯罪",
            "治愈",
            "温暖",
            "轻松",
            "喜剧",
            "科幻",
            "奇幻",
            "动画",
            "现实",
            "文艺",
            "经典",
            "探索",
            "新鲜",
            "动作",
            "家庭",
            "历史",
            "浪漫"
        ];

        return semanticRoots.Any(root =>
            left.Contains(root, StringComparison.OrdinalIgnoreCase)
            && right.Contains(root, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeProfileSentence(string text, int maxLength = 240)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = string.Join(
            " ",
            text.Split(['\r', '\n', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].TrimEnd() + "…";
    }

    private static string NormalizeDnaDescription(WatchProfileDnaGene gene)
    {
        if (string.IsNullOrWhiteSpace(gene.Description))
        {
            return string.Empty;
        }

        if (!IsProgressDnaGene(gene.Gene)
            && DescriptionLooksLikeTagList(gene.Description, gene.Tags))
        {
            return BuildTagGeneFallbackDescription(gene.Gene);
        }

        return NormalizeProfileSentence(gene.Description);
    }

    private static bool IsProgressDnaGene(string geneName)
    {
        return string.Equals(geneName, DnaGenes[4], StringComparison.Ordinal)
            || string.Equals(geneName, DnaGenes[5], StringComparison.Ordinal);
    }

    private static bool DescriptionLooksLikeTagList(string description, IReadOnlyCollection<string> tags)
    {
        if (tags.Count == 0)
        {
            return false;
        }

        var hitCount = tags.Count(tag => description.Contains(tag, StringComparison.OrdinalIgnoreCase));
        if (hitCount >= Math.Min(2, tags.Count))
        {
            return true;
        }

        var separators = description.Count(ch => ch is '、' or ',' or '，' or '/' or '；' or ';');
        return separators >= 2 && hitCount > 0;
    }

    private static string BuildTagGeneFallbackDescription(string geneName)
    {
        return geneName switch
        {
            "类型基因" => "你更容易被有明确结构和稳定期待的题材牵引。",
            "情绪基因" => "你的选择更看重观影后的情绪余韵，而不只是轻量消遣。",
            "场景基因" => "这些线索说明你常把电影当作进入特定氛围的方式。",
            "叙事基因" => "你更容易被叙事结构、人物动机或信息推进带动。",
            _ => "继续积累标签和观影记录后，这个维度会更清晰。"
        };
    }

    private static IEnumerable<string> SplitTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(['、', ',', '，', '/', '|', ';', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static List<string> NormalizeCommonDnaTags(IEnumerable<string> tags)
    {
        return tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static List<string> NormalizeNarrativeTags(IEnumerable<string> tags, ICollection<string> warnings)
    {
        var normalized = new List<string>();
        var invalidCount = 0;
        foreach (var tag in tags.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()))
        {
            if (NarrativeTags.Contains(tag, StringComparer.Ordinal))
            {
                if (!normalized.Contains(tag, StringComparer.Ordinal))
                {
                    normalized.Add(tag);
                }
            }
            else
            {
                invalidCount++;
            }

            if (normalized.Count >= 3)
            {
                break;
            }
        }

        if (invalidCount > 0)
        {
            warnings.Add("AI 返回了叙事标签集合外的标签，已在画像层过滤。");
        }

        return normalized;
    }

    private static void FillLocalDefaults(WatchProfileSnapshot profile, WatchProfileInputSnapshot input)
    {
        profile.WatchVsLike.OftenWatchedTypes = input.StatisticsSummary.OftenWatchedTypes.Select(x => x.Label).ToList();
        profile.WatchVsLike.OftenLikedTypes = input.StatisticsSummary.OftenLikedTypes.Select(x => x.Label).ToList();
        profile.WatchVsLike.OftenWantedTypes = input.StatisticsSummary.OftenWantedTypes.Select(x => x.Label).ToList();

        if (profile.Likes.PreferredGenres.Count == 0)
        {
            profile.Likes.PreferredGenres = input.StatisticsSummary.TypeDistribution.Take(5).Select(x => x.Label).ToList();
        }

        if (profile.Likes.PreferredEmotions.Count == 0)
        {
            profile.Likes.PreferredEmotions = input.StatisticsSummary.EmotionDistribution.Take(5).Select(x => x.Label).ToList();
        }

        if (profile.Likes.PreferredScenes.Count == 0)
        {
            profile.Likes.PreferredScenes = input.StatisticsSummary.SceneDistribution.Take(5).Select(x => x.Label).ToList();
        }

        if (profile.Summary.Keywords.Count == 0)
        {
            profile.Summary.Keywords = profile.Likes.PreferredGenres
                .Concat(profile.Likes.PreferredEmotions)
                .Take(6)
                .ToList();
        }
    }

    private static WatchProfileSnapshot CreateInsufficientSnapshot(WatchProfileInputSnapshot input)
    {
        return new WatchProfileSnapshot
        {
            Meta = new WatchProfileMeta
            {
                GeneratedAtUtc = DateTime.UtcNow,
                SourceFingerprint = input.SourceFingerprint,
                ProfileSchemaVersion = CurrentProfileSchemaVersion,
                PromptVersion = CurrentPromptVersion,
                SignalMovieCount = input.SignalMovieCount,
                Confidence = 0,
                WarningMessages = input.WarningMessages.ToList()
            },
            LoadedFromCache = false,
            HasProfile = false,
            CanGenerateProfile = false,
            InsufficientReason = input.InsufficientReason,
            StatusMessage = input.InsufficientReason,
            WasAiCalled = false,
            IsCacheHit = false,
            IsUnchanged = false,
            WarningMessages = input.WarningMessages.ToList(),
            Caveats = [input.InsufficientReason]
        };
    }

    private async Task<bool> TryRegenerateFallbackPersonaAsync(
        WatchProfileSnapshot profile,
        WatchProfileInputSnapshot input,
        CancellationToken cancellationToken)
    {
        try
        {
            Log("watch-profile-persona-fallback-regenerate-start");
            var response = await _aiService.GenerateTextAsync(
                BuildPersonaFallbackSystemPrompt(),
                BuildPersonaFallbackUserPrompt(profile, input),
                cancellationToken);
            if (string.IsNullOrWhiteSpace(response))
            {
                return false;
            }

            var json = ExtractJsonObject(response);
            var persona = JsonSerializer.Deserialize<WatchProfilePersona>(json, JsonOptions);
            if (persona is null)
            {
                return false;
            }

            profile.Persona.Type = "多元杂食者";
            profile.Persona.Title = string.IsNullOrWhiteSpace(persona.Title) ? "多元杂食者" : persona.Title.Trim();
            profile.Persona.Description = string.IsNullOrWhiteSpace(persona.Description)
                ? BuildFixedPersonaFallbackDescription(input)
                : NormalizeProfileSentence(persona.Description);
            profile.Persona.Confidence = Clamp(persona.Confidence, 0, 100);
            Log("watch-profile-persona-fallback-regenerate-complete");
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log($"watch-profile-persona-fallback-regenerate-failed errorType={exception.GetType().Name}");
            return false;
        }
    }

    private static string BuildPersonaFallbackSystemPrompt()
    {
        return """
        你只负责修正观影人格文案。只能输出 JSON 对象，不输出 Markdown 或解释文本。
        persona.type 已固定为“多元杂食者”，不要改变类型。
        title 和 description 必须与“多元杂食者”匹配，description 要基于给定摘要解释口味宽、兼容度高或类型覆盖面广，不要编造推荐结果。
        """;
    }

    private static string BuildPersonaFallbackUserPrompt(
        WatchProfileSnapshot profile,
        WatchProfileInputSnapshot input)
    {
        var payload = new
        {
            fixedType = "多元杂食者",
            signalMovieCount = input.SignalMovieCount,
            summary = profile.Summary,
            watchVsLike = profile.WatchVsLike,
            statisticsSummary = input.StatisticsSummary,
            sampleCounts = new
            {
                watched = input.WatchedSamples.Count,
                favorite = input.FavoriteSamples.Count,
                wantToWatch = input.WantToWatchSamples.Count,
                notInterested = input.NotInterestedSamples.Count
            }
        };

        return $$"""
        请只返回以下 JSON：
        {"title":"多元杂食者","description":"","confidence":0}

        输入数据：
        {{JsonSerializer.Serialize(payload, JsonOptions)}}
        """;
    }

    private static void ApplyFixedPersonaFallback(WatchProfileSnapshot profile)
    {
        profile.Persona ??= new WatchProfilePersona();
        profile.Persona.Type = "多元杂食者";
        profile.Persona.Title = "多元杂食者";
        profile.Persona.Description = "你的观影信号暂时难以落到单一人格上，更像是在多个类型和情绪方向之间自然切换。";
        profile.Persona.Confidence = Clamp(profile.Persona.Confidence == 0 ? 50 : profile.Persona.Confidence, 0, 100);
    }

    private static string BuildFixedPersonaFallbackDescription(WatchProfileInputSnapshot input)
    {
        return input.SignalMovieCount >= 8
            ? "你的观影信号分布在多个类型和情绪方向上，更像自然扩展的多元口味，而不是被单一题材绑定。"
            : "当前样本还不够集中，暂时更适合作为多元杂食者处理。";
    }

    private static bool IsPersonaTypeValid(string? personaType)
    {
        return !string.IsNullOrWhiteSpace(personaType)
            && PersonaTypes.Contains(personaType, StringComparer.Ordinal);
    }

    private static WatchProfileSnapshot CreateErrorSnapshot(WatchProfileInputSnapshot input, string errorMessage)
    {
        return new WatchProfileSnapshot
        {
            Meta = new WatchProfileMeta
            {
                GeneratedAtUtc = DateTime.UtcNow,
                SourceFingerprint = input.SourceFingerprint,
                ProfileSchemaVersion = CurrentProfileSchemaVersion,
                PromptVersion = CurrentPromptVersion,
                SignalMovieCount = input.SignalMovieCount,
                Confidence = 0,
                WarningMessages = [errorMessage]
            },
            LoadedFromCache = false,
            HasProfile = false,
            CanGenerateProfile = input.CanGenerateProfile,
            ErrorMessage = errorMessage,
            StatusMessage = errorMessage,
            WasAiCalled = false,
            IsCacheHit = false,
            IsUnchanged = false,
            WarningMessages = [errorMessage]
        };
    }

    private static string BuildSystemPrompt()
    {
        return """
        你是观影偏好画像分析助手。只能基于用户提供的结构化观影数据分析，不得编造没有数据支持的偏好。
        喜爱权重最高；想看代表未来兴趣；不想看代表负反馈；已看代表实际观看行为但不一定等于喜欢；WatchHistory 观看时长代表投入程度。
        自定义推荐偏好不在输入中，也不得假设。未识别、识别失败、无 TMDB 身份影片已被排除。
        画像是偏好总结，不是推荐结果。必须只返回 JSON 对象，不要输出 Markdown、解释文本或代码块。
        persona.type 必须从最终版固定 20 个观影人格类型中选择，不得自创；选择时必须参考类型边界说明，避免把相近人格混淆。
        口味象限必须由你基于输入数据输出二维坐标字段 xAxisScore/yAxisScore；服务层只做 -100 到 100 的范围校验，不会用本地分数覆盖。
        输出文案要像产品画像：具体、有判断力、少说空话。整份画像必须围绕一个核心判断展开：Summary 负责给出总判断，Persona 负责解释人格归因，DNA 负责拆解维度证据，WatchVsLike 负责解释行为差异。不要让各模块各说各的，也不要互相复述；DNA 描述要解释标签组合的偏好含义。
        """;
    }

    private static string BuildUserPrompt(WatchProfileInputSnapshot input)
    {
        var payload = new
        {
            dataRules = new
            {
                likedWeight = "highest",
                watchedMeaning = "actual behavior, not always preference",
                wantToWatchMeaning = "future interest",
                notInterestedMeaning = "negative signal",
                customRecommendationPreferencesIncluded = false,
                unidentifiedMoviesExcluded = true
            },
            personaTypes = PersonaTypes,
            personaTypeDefinitions = PersonaTypeDefinitions,
            personaSelectionRules = PersonaSelectionRules,
            requiredDnaGenes = DnaGenes,
            narrativeTags = NarrativeTags,
            quadrant = new
            {
                xAxis = "-100=熟悉安全，100=新鲜探索",
                yAxis = "-100=轻松消遣，100=情绪沉浸",
                scoringOwner = "AI must output xAxisScore and yAxisScore from the provided data; service only clamps range."
            },
            input.SignalMovieCount,
            input.BucketCount,
            input.TagCount,
            input.StatisticsSummary,
            samples = new
            {
                watched = input.WatchedSamples,
                favorite = input.FavoriteSamples,
                wantToWatch = input.WantToWatchSamples,
                notInterested = input.NotInterestedSamples,
                recentHistory = input.RecentHistorySamples
            }
        };

        return $$"""
        请根据下面 JSON 数据生成观影画像。只输出 JSON，字段固定为：
        {
          "meta":{"generatedAtUtc":"2026-01-01T00:00:00Z","sourceFingerprint":"","profileSchemaVersion":{{CurrentProfileSchemaVersion}},"promptVersion":"{{CurrentPromptVersion}}","signalMovieCount":0,"confidence":0,"warningMessages":[]},
          "summary":{"text":"","keywords":[]},
          "persona":{"type":"多元杂食者","title":"多元杂食者","description":"","confidence":0},
          "dna":[
            {"gene":"类型基因","label":"","tags":[],"score":0,"description":"","confidence":0},
            {"gene":"情绪基因","label":"","tags":[],"score":0,"description":"","confidence":0},
            {"gene":"场景基因","label":"","tags":[],"score":0,"description":"","confidence":0},
            {"gene":"叙事基因","label":"","tags":[],"score":0,"description":"","confidence":0},
            {"gene":"节奏基因","label":"","tags":[],"score":0,"description":"","confidence":0},
            {"gene":"探索基因","label":"","tags":[],"score":0,"description":"","confidence":0}
          ],
          "quadrant":{"xAxisScore":0,"yAxisScore":0,"quadrantName":"","description":""},
          "watchVsLike":{"oftenWatchedTypes":[],"oftenLikedTypes":[],"oftenWantedTypes":[],"conclusion":""},
          "likes":{"preferredGenres":[],"preferredEmotions":[],"preferredScenes":[],"preferredCountries":[],"preferredLanguages":[]},
          "dislikes":{"avoidGenres":[],"avoidEmotions":[],"avoidScenes":[],"negativeSummary":""},
          "futurePreference":{"likelyToEnjoy":[],"lessLikelyToEnjoy":[]},
          "caveats":[]
        }

        约束：
        1. persona.type 只能是以下集合之一：{{string.Join("、", PersonaTypes)}}。
        2. 必须参考输入中的 personaTypeDefinitions 和 personaSelectionRules 选择最强差异化人格，不要只按题材表面相似度选择。
        3. summary.text 写 2-4 句自然语言总结，可以比其他模块更完整；说明总体口味、选择动机和观看投入方式，但不要机械复述 6 个关键词，不要和 persona.description 大段重复。
        4. summary.keywords 最多 6 个，可由你基于画像总结生成，不要求必须来自影片标签；关键词应覆盖题材、情绪、观看方式、审美倾向、探索倾向等不同维度，不要使用语义高度重复的词。
        5. persona.description 要解释为什么归为该人格，必须结合观看、喜爱、想看或不想看等行为信号；不要简单罗列关键词，也不要复述 summary.text。
        6. DNA 必须包含六个基因：{{string.Join("、", DnaGenes)}}。
        7. 类型基因、情绪基因、场景基因、叙事基因的 tags 各输出 3 个标签；叙事基因 tags 只能从 narrativeTags 集合中选择，不要为每部影片生成叙事标签。
        8. 类型/情绪/场景/叙事基因的 description 必须解释标签组合背后的偏好，不要把 tags 改写成一句话，不要逐字重复 tags。
        9. 节奏基因用 score 表示 0=慢热、100=紧凑；description 必须与 score 方向一致：0-35 慢热，36-64 均衡，65-100 紧凑。
        10. 探索基因用 score 表示 0=稳定、100=新鲜；description 必须与 score 方向一致：0-35 稳定，36-64 平衡，65-100 新鲜。
        11. Summary、Persona、DNA 描述之间必须围绕同一个核心画像判断，但提供不同解释角度，不要完全复述。
        12. score、confidence 范围 0-100。
        13. xAxisScore、yAxisScore 必须由你输出，范围 -100 到 100；缺失或非数字会被视为画像生成错误。quadrant.description 必须解释分数依据。
        14. watchVsLike 的三个排行会由本地统计覆盖；你只需要给 conclusion 写一句行为差异解释，不要虚构排行。
        15. 不要输出推荐片单。
        16. 不要引用文件路径、URL、账号、token 或任何未提供内容。

        输入数据：
        {{JsonSerializer.Serialize(payload, JsonOptions)}}
        """;
    }

    private static void AddUnique(ICollection<string> list, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || list.Contains(value))
        {
            return;
        }

        list.Add(value);
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Clamp(value, min, max);
    }

    private static string BuildQuadrantName(int xAxisScore, int yAxisScore)
    {
        if (xAxisScore >= 0 && yAxisScore >= 0)
        {
            return "新鲜探索 × 情绪沉浸";
        }

        if (xAxisScore < 0 && yAxisScore >= 0)
        {
            return "熟悉安全 × 情绪沉浸";
        }

        return xAxisScore < 0 && yAxisScore < 0
            ? "熟悉安全 × 轻松消遣"
            : "新鲜探索 × 轻松消遣";
    }

    private static string ShortFingerprint(string fingerprint)
    {
        return string.IsNullOrWhiteSpace(fingerprint)
            ? "(none)"
            : fingerprint.Length <= 12 ? fingerprint : fingerprint[..12];
    }

    private static string FormatPromptVersion(WatchProfileSnapshot profile)
    {
        return string.IsNullOrWhiteSpace(profile.Meta.PromptVersion)
            ? $"schema:{profile.Meta.ProfileSchemaVersion};prompt:(none)"
            : $"schema:{profile.Meta.ProfileSchemaVersion};prompt:{profile.Meta.PromptVersion}";
    }

    private static void Log(string message)
    {
        Debug.WriteLine("[WATCH-PROFILE] " + message);
        AiPerfDiagnostics.WriteEvent("event=" + message);
    }
}
