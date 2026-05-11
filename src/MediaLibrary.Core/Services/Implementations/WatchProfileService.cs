using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Diagnostics;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Models.Settings;
using MediaLibrary.Core.Services.Interfaces;

namespace MediaLibrary.Core.Services.Implementations;

public sealed class WatchProfileService : IWatchProfileService
{
    private const string ProfileKind = "profile";
    private const string GlobalScopeKey = "global";
    private const int CurrentProfileSchemaVersion = 2;
    private const string CurrentPromptVersion = "wi-profile-persona-23-parallel-v7";
    private const string FallbackPersonaType = "类型探索家";
    private const string FallbackPersonaTitle = "类型探索家";
    private const string FallbackPersonaDescription = "你的观影口味覆盖多个方向，更愿意主动尝试不同类型与风格，而不是被单一标签固定。";
    private const int MaxParallelProfileCardRequests = 5;

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
        "惊悚氛围控",
        "黑色幽默爱好者",
        "浪漫幻想派",
        "暗黑猎奇者",
        "史诗世界观派",
        "轻松娱乐派",
        "人性剖析者",
        "怀旧年代派",
        "小众寻宝者",
        "爆笑解压派",
        "动画叙事派",
        "纪录求真者"
    ];

    private static readonly string[] PersonaTypeDefinitions =
    [
        "1. 情绪沉浸者：容易被电影情绪带入，重视共情、氛围和情感余韵。",
        "2. 悬疑解谜者：喜欢推理、反转、线索、谜题和层层揭开的故事。",
        "3. 类型探索家：喜欢主动尝试不同类型，不满足于固定口味。",
        "4. 经典收藏家：偏爱经典老片、影史佳作、值得反复收藏的作品。",
        "5. 治愈陪伴型：喜欢温暖、柔软、安心、陪伴感强的电影。",
        "6. 高分严选派：看重评分、口碑、奖项和大众认可度，倾向筛选高质量作品。",
        "7. 作者导演迷：关注导演风格、作者表达、镜头语言和创作理念。",
        "8. 科幻幻想旅人：喜欢科幻、奇幻、异世界、未来感和想象力丰富的作品。",
        "9. 现实观察者：喜欢现实主义、社会议题、生活质感和真实人物处境。",
        "10. 动作爽片玩家：喜欢高能动作、追逐、打斗、爆炸、节奏刺激的爽片体验。",
        "11. 文艺审美家：重视画面美感、构图、色彩、音乐和整体艺术气质。",
        "12. 惊悚氛围控：喜欢恐怖片、惊悚片、压迫感、紧张感和黑夜试胆式观影体验。",
        "13. 黑色幽默爱好者：喜欢荒诞、反讽、冷幽默、尖锐但有趣的故事。",
        "14. 浪漫幻想派：喜欢爱情、幻想、梦境感、浪漫氛围和理想化情绪。",
        "15. 暗黑猎奇者：喜欢怪诞、邪典、边缘、奇异设定和暗黑审美，不等同于恐怖片爱好者。",
        "16. 史诗世界观派：喜欢宏大设定、复杂世界观、历史感、文明感和长篇叙事。",
        "17. 轻松娱乐派：看电影主要为了放松、解压、开心，不追求太沉重的表达。",
        "18. 人性剖析者：喜欢复杂人物、心理变化、道德困境和人性深处的冲突。",
        "19. 怀旧年代派：偏爱年代感、旧时光、复古影像和带有回忆滤镜的作品。",
        "20. 小众寻宝者：喜欢发现冷门佳作、小众电影、独立片和被低估的作品。",
        "21. 爆笑解压派：喜欢喜剧、无厘头、生活笑料、轻松爆笑和快乐续命型电影。",
        "22. 动画叙事派：喜欢动画电影独特的视觉语言、叙事、美术风格和情感内核，不把动画等同于低龄内容。",
        "23. 纪录求真者：喜欢纪录片、真实事件、人物档案、自然人文和知识型观影体验。"
    ];

    private static readonly string[] PersonaSelectionRules =
    [
        "如果解谜过程强于犯罪题材，选悬疑解谜者。",
        "如果复杂人物、心理变化、道德困境强于解谜过程，选人性剖析者。",
        "如果情绪冲击强于画面审美，选情绪沉浸者。",
        "如果影像氛围强于情绪冲击，选文艺审美家。",
        "如果主动探索陌生类型，选类型探索家。",
        "如果口味广泛且愿意主动尝试不同类型，选类型探索家。",
        "如果情绪修复和温暖陪伴强，选治愈陪伴型。",
        "如果泛轻松、低负担、放松观看强，选轻松娱乐派；如果明确偏喜剧、无厘头和爆笑解压，选爆笑解压派。",
        "如果世界观和设定强，选科幻幻想旅人。",
        "如果动画媒介、视觉语言、美术风格和情感内核强，选动画叙事派。",
        "如果历史感、文明感和宏大世界设定强，选史诗世界观派。",
        "如果影史地位和收藏价值强，选经典收藏家。",
        "如果恐怖片、惊悚片、压迫感和黑夜试胆式体验强，选惊悚氛围控；如果怪诞、邪典、边缘设定和暗黑审美强，选暗黑猎奇者。",
        "如果真实事件、人物档案、自然人文或知识型观看强，选纪录求真者；如果广义现实生活和社会议题强，选现实观察者。",
        "如果小众独立片、冷门佳作和被低估作品强，选小众寻宝者；如果只是尝试类型更多，选类型探索家。"
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
            Log(
                "watch-profile-ai-start "
                + $"sampleMovies={input.SignalMovieCount} mode=parallel cards=5 maxConcurrency={MaxParallelProfileCardRequests}");
            var profile = await GenerateProfileInParallelAsync(input, cancellationToken);
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

                AddUnique(profile.WarningMessages, "AI 返回非法人格类型，已回退为类型探索家。");
                AddUnique(profile.Meta.WarningMessages, "AI 返回非法人格类型，已回退为类型探索家。");
            }

            NormalizeProfile(profile, input, loadedFromCache: false);
            stopwatch.Stop();
            Log($"watch-profile-ai-complete elapsedMs={stopwatch.ElapsedMilliseconds} mode=parallel");

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

    public async Task<WatchProfileRecommendationContext> GetRecommendationContextAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cache = await _cacheService.GetAsync(ProfileKind, GlobalScopeKey, cancellationToken);
            if (cache is null)
            {
                Log("recommendation-profile-context-skipped reason=no-cache");
                return CreateSkippedRecommendationContext("no-cache");
            }

            if (cache.IsStale)
            {
                Log("recommendation-profile-context-skipped reason=stale");
                return CreateSkippedRecommendationContext("stale");
            }

            if (cache.ExpiresAtUtc.HasValue && cache.ExpiresAtUtc.Value <= DateTime.UtcNow)
            {
                Log("recommendation-profile-context-skipped reason=expired");
                return CreateSkippedRecommendationContext("expired");
            }

            if (!TryDeserializeProfile(cache.PayloadJson, out var profile) || profile is null)
            {
                Log("recommendation-profile-context-skipped reason=parse-failed");
                return CreateSkippedRecommendationContext("parse-failed");
            }

            if (!profile.HasProfile || !profile.CanGenerateProfile)
            {
                var reason = string.IsNullOrWhiteSpace(profile.InsufficientReason)
                    ? "insufficient"
                    : "insufficient";
                Log($"recommendation-profile-context-skipped reason={reason}");
                return CreateSkippedRecommendationContext(reason);
            }

            var context = BuildRecommendationContext(cache, profile);
            Log(
                "recommendation-profile-context-loaded "
                + $"hasProfile=true persona={AiPerfDiagnostics.FormatValue(context.PersonaType)}");
            Log($"recommendation-profile-fingerprint hash={ShortFingerprint(context.FingerprintPart)}");
            return context;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log($"recommendation-profile-context-skipped reason=service-error errorType={exception.GetType().Name}");
            return CreateSkippedRecommendationContext("service-error");
        }
    }

    private async Task<WatchProfileSnapshot> GenerateProfileInParallelAsync(
        WatchProfileInputSnapshot input,
        CancellationToken cancellationToken)
    {
        using var throttler = new SemaphoreSlim(MaxParallelProfileCardRequests, MaxParallelProfileCardRequests);
        var summaryTask = RunProfileCardRequestAsync(
            "summary",
            BuildCardSystemPrompt("观影口味总结"),
            BuildSummaryCardPrompt(input),
            ParseSummaryCard,
            throttler,
            cancellationToken);
        var personaTask = RunProfileCardRequestAsync(
            "persona",
            BuildCardSystemPrompt("观影人格"),
            BuildPersonaCardPrompt(input),
            ParsePersonaCard,
            throttler,
            cancellationToken);
        var dnaTask = RunProfileCardRequestAsync(
            "dna",
            BuildCardSystemPrompt("观影 DNA"),
            BuildDnaCardPrompt(input),
            ParseDnaCard,
            throttler,
            cancellationToken);
        var quadrantTask = RunProfileCardRequestAsync(
            "quadrant",
            BuildCardSystemPrompt("口味象限"),
            BuildQuadrantCardPrompt(input),
            ParseQuadrantCard,
            throttler,
            cancellationToken);
        var watchVsLikeTask = RunProfileCardRequestAsync(
            "watch-vs-like",
            BuildCardSystemPrompt("看得多 vs 真喜欢"),
            BuildWatchVsLikeCardPrompt(input),
            ParseWatchVsLikeCard,
            throttler,
            cancellationToken);

        await Task.WhenAll(summaryTask, personaTask, dnaTask, quadrantTask, watchVsLikeTask);
        return new WatchProfileSnapshot
        {
            Meta = new WatchProfileMeta
            {
                GeneratedAtUtc = DateTime.UtcNow,
                SourceFingerprint = input.SourceFingerprint,
                ProfileSchemaVersion = CurrentProfileSchemaVersion,
                PromptVersion = CurrentPromptVersion,
                SignalMovieCount = input.SignalMovieCount,
                Confidence = 60
            },
            Summary = await summaryTask,
            Persona = await personaTask,
            DNA = await dnaTask,
            Quadrant = await quadrantTask,
            WatchVsLike = await watchVsLikeTask,
            Likes = new WatchProfileLikes(),
            Dislikes = new WatchProfileDislikes(),
            FuturePreference = new WatchProfileFuturePreference()
        };
    }

    private async Task<T> RunProfileCardRequestAsync<T>(
        string cardName,
        string systemPrompt,
        string userPrompt,
        Func<string, T> parse,
        SemaphoreSlim throttler,
        CancellationToken cancellationToken)
    {
        await throttler.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            Log($"watch-profile-ai-card-start card={cardName}");
            var response = await _aiService.GenerateTextAsync(
                systemPrompt,
                userPrompt,
                AiRequestOptions.WatchProfile,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new InvalidOperationException($"AI profile card response was empty: {cardName}.");
            }

            var result = parse(response);
            stopwatch.Stop();
            Log($"watch-profile-ai-card-complete card={cardName} elapsedMs={stopwatch.ElapsedMilliseconds}");
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log($"watch-profile-ai-card-failed card={cardName} errorType={exception.GetType().Name}");
            throw;
        }
        finally
        {
            throttler.Release();
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

    private static WatchProfileRecommendationContext CreateSkippedRecommendationContext(string reason)
    {
        return new WatchProfileRecommendationContext
        {
            HasProfile = false,
            SkipReason = reason,
            FingerprintPart = "profile:none"
        };
    }

    private static WatchProfileRecommendationContext BuildRecommendationContext(
        WatchInsightCacheSnapshot cache,
        WatchProfileSnapshot profile)
    {
        profile.Meta ??= new WatchProfileMeta();
        profile.Summary ??= new WatchProfileSummary();
        profile.Persona ??= new WatchProfilePersona();
        profile.DNA ??= [];
        profile.Quadrant ??= new WatchProfileQuadrant();
        profile.WatchVsLike ??= new WatchProfileWatchVsLike();

        var lines = new List<string>
        {
            "用户长期观影画像（软偏好背景，不是硬过滤；自定义推荐偏好优先于画像）："
        };

        var persona = FormatProfileParts(
            profile.Persona.Type,
            profile.Persona.Title,
            profile.Persona.Description);
        if (!string.IsNullOrWhiteSpace(persona))
        {
            lines.Add($"- 观影人格：{persona}");
        }

        var summary = FormatProfileParts(
            profile.Summary.Text,
            FormatList(profile.Summary.Keywords, 6));
        if (!string.IsNullOrWhiteSpace(summary))
        {
            lines.Add($"- 口味总结：{summary}");
        }

        var dna = BuildRecommendationDnaSummary(profile.DNA);
        if (!string.IsNullOrWhiteSpace(dna))
        {
            lines.Add($"- 观影 DNA：{dna}");
        }

        if (!string.IsNullOrWhiteSpace(profile.Quadrant.QuadrantName)
            || !string.IsNullOrWhiteSpace(profile.Quadrant.Description))
        {
            lines.Add(
                "- 口味象限："
                + $"{profile.Quadrant.QuadrantName} "
                + $"(X={profile.Quadrant.XAxisScore}, Y={profile.Quadrant.YAxisScore})。"
                + profile.Quadrant.Description);
        }

        var watchVsLike = BuildRecommendationWatchVsLikeSummary(profile.WatchVsLike);
        if (!string.IsNullOrWhiteSpace(watchVsLike))
        {
            lines.Add($"- 看得多 vs 真喜欢：{watchVsLike}");
        }

        lines.Add("使用方式：画像只能帮助排序、选择相邻探索和解释匹配点；不要因为画像硬排除候选片，也不要机械重复画像标签。推荐理由可以轻度体现画像匹配，但不要提及内部字段名、X/Y 分数或 DNA 分数。");

        return new WatchProfileRecommendationContext
        {
            HasProfile = true,
            PersonaType = profile.Persona.Type,
            PromptSection = string.Join(Environment.NewLine, lines),
            FingerprintPart = BuildRecommendationProfileFingerprint(cache, profile)
        };
    }

    private static string BuildRecommendationDnaSummary(IReadOnlyList<WatchProfileDnaGene> dna)
    {
        if (dna.Count == 0)
        {
            return string.Empty;
        }

        var parts = dna
            .Where(x => !string.IsNullOrWhiteSpace(x.Gene))
            .Take(6)
            .Select(
                x =>
                {
                    var tags = FormatList(x.Tags, 3);
                    var label = FormatProfileParts(x.Label, tags);
                    var description = string.IsNullOrWhiteSpace(x.Description) ? string.Empty : $"：{x.Description}";
                    return string.IsNullOrWhiteSpace(label)
                        ? $"{x.Gene}{description}"
                        : $"{x.Gene}={label}{description}";
                })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return string.Join("；", parts);
    }

    private static string BuildRecommendationWatchVsLikeSummary(WatchProfileWatchVsLike watchVsLike)
    {
        var parts = new List<string>();
        var watched = FormatList(watchVsLike.OftenWatchedTypes, 3);
        if (!string.IsNullOrWhiteSpace(watched))
        {
            parts.Add($"经常观看 {watched}");
        }

        var liked = FormatList(watchVsLike.OftenLikedTypes, 3);
        if (!string.IsNullOrWhiteSpace(liked))
        {
            parts.Add($"经常喜爱 {liked}");
        }

        var wanted = FormatList(watchVsLike.OftenWantedTypes, 3);
        if (!string.IsNullOrWhiteSpace(wanted))
        {
            parts.Add($"经常想看 {wanted}");
        }

        if (!string.IsNullOrWhiteSpace(watchVsLike.Conclusion))
        {
            parts.Add(watchVsLike.Conclusion);
        }

        return string.Join("；", parts);
    }

    private static string BuildRecommendationProfileFingerprint(
        WatchInsightCacheSnapshot cache,
        WatchProfileSnapshot profile)
    {
        var payloadHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(cache.PayloadJson ?? string.Empty)))
            .ToLowerInvariant();
        var raw = string.Join(
            "|",
            "profile",
            cache.SourceFingerprint,
            cache.RefreshedAtUtc.Ticks,
            profile.Meta.SourceFingerprint,
            profile.Meta.ProfileSchemaVersion,
            profile.Meta.PromptVersion,
            profile.Meta.GeneratedAtUtc.Ticks,
            payloadHash);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
            .ToLowerInvariant();
        return $"profile:{hash}";
    }

    private static string FormatList(IEnumerable<string>? values, int maxCount)
    {
        if (values is null)
        {
            return string.Empty;
        }

        return string.Join(
            "、",
            values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(maxCount));
    }

    private static string FormatProfileParts(params string[] parts)
    {
        return string.Join(
            "；",
            parts
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
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
            profile.Persona.Type = FallbackPersonaType;
            warnings.Add("AI 返回了未知人格类型，已回退为类型探索家。");
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
                AiRequestOptions.WatchProfile,
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

            profile.Persona.Type = FallbackPersonaType;
            profile.Persona.Title = string.IsNullOrWhiteSpace(persona.Title) ? FallbackPersonaTitle : persona.Title.Trim();
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
        persona.type 已固定为“类型探索家”，不要改变类型。
        title 和 description 必须与“类型探索家”匹配，description 要基于给定摘要解释主动尝试不同类型与风格、不会被单一口味固定，不要编造推荐结果。
        """;
    }

    private static string BuildPersonaFallbackUserPrompt(
        WatchProfileSnapshot profile,
        WatchProfileInputSnapshot input)
    {
        var payload = new
        {
            fixedType = FallbackPersonaType,
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
        {"title":"类型探索家","description":"","confidence":0}

        输入数据：
        {{JsonSerializer.Serialize(payload, JsonOptions)}}
        """;
    }

    private static WatchProfileSummary ParseSummaryCard(string text)
    {
        var root = ParseCardRoot(text);
        var target = root.TryGetProperty("summary", out var summary) ? summary : root;
        return JsonSerializer.Deserialize<WatchProfileSummary>(target.GetRawText(), JsonOptions)
               ?? throw new JsonException("AI profile summary card could not be parsed.");
    }

    private static WatchProfilePersona ParsePersonaCard(string text)
    {
        var root = ParseCardRoot(text);
        var target = root.TryGetProperty("persona", out var persona) ? persona : root;
        return JsonSerializer.Deserialize<WatchProfilePersona>(target.GetRawText(), JsonOptions)
               ?? throw new JsonException("AI profile persona card could not be parsed.");
    }

    private static List<WatchProfileDnaGene> ParseDnaCard(string text)
    {
        var root = ParseCardRoot(text);
        if (!root.TryGetProperty("dna", out var dna) || dna.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("AI profile DNA card is missing dna array.");
        }

        return JsonSerializer.Deserialize<List<WatchProfileDnaGene>>(dna.GetRawText(), JsonOptions)
               ?? throw new JsonException("AI profile DNA card could not be parsed.");
    }

    private static WatchProfileQuadrant ParseQuadrantCard(string text)
    {
        var json = ExtractJsonObject(text);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("quadrant", out var quadrant))
        {
            Log("watch-profile-quadrant-missing error=missing-quadrant");
            throw new JsonException("AI profile quadrant card is missing quadrant.");
        }

        if (!TryReadQuadrantNumber(quadrant, "xAxisScore")
            || !TryReadQuadrantNumber(quadrant, "yAxisScore"))
        {
            Log("watch-profile-quadrant-missing error=missing-or-invalid-axis");
            throw new JsonException("AI profile quadrant card is missing valid x/y scores.");
        }

        return JsonSerializer.Deserialize<WatchProfileQuadrant>(quadrant.GetRawText(), JsonOptions)
               ?? throw new JsonException("AI profile quadrant card could not be parsed.");
    }

    private static WatchProfileWatchVsLike ParseWatchVsLikeCard(string text)
    {
        var root = ParseCardRoot(text);
        var target = root.TryGetProperty("watchVsLike", out var watchVsLike) ? watchVsLike : root;
        return JsonSerializer.Deserialize<WatchProfileWatchVsLike>(target.GetRawText(), JsonOptions)
               ?? throw new JsonException("AI profile watch-vs-like card could not be parsed.");
    }

    private static JsonElement ParseCardRoot(string text)
    {
        var json = ExtractJsonObject(text);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string BuildCardSystemPrompt(string cardName)
    {
        return $$$"""
        你是观影偏好画像分析助手，当前只负责生成“{{{cardName}}}”这一张画像卡片。
        只能基于用户提供的结构化观影数据分析，不得编造没有数据支持的偏好。
        喜爱权重最高；想看代表未来兴趣；不想看代表负反馈；已看代表实际观看行为但不一定等于喜欢；WatchHistory 观看时长代表投入程度。
        自定义推荐偏好不在输入中，也不得假设。未识别、识别失败、无 TMDB 身份影片已被排除。
        只返回指定 JSON 对象，不要输出 Markdown、解释文本、代码块、推荐片单、文件路径、URL、账号或 token。
        """;
    }

    private static string BuildSummaryCardPrompt(WatchProfileInputSnapshot input)
    {
        var payload = BuildProfileEvidencePayload(input);
        return $$$"""
        任务：只生成观影口味总结卡片。
        输出 JSON：
        {"summary":{"text":"","keywords":[]}}

        要求：
        1. summary.text 写 2-4 句自然语言总结，可以比其他模块更完整；说明总体口味、选择动机和观看投入方式。
        2. 不要机械复述关键词，不要连续堆砌标签。
        3. summary.keywords 最多 6 个，可基于画像总结生成，不要求必须来自影片标签。
        4. 关键词应覆盖题材、情绪、观看方式、审美倾向、探索倾向等不同维度，不要语义高度重复。

        输入数据：
        {{{JsonSerializer.Serialize(payload, JsonOptions)}}}
        """;
    }

    private static string BuildPersonaCardPrompt(WatchProfileInputSnapshot input)
    {
        var payload = new
        {
            data = BuildProfileEvidencePayload(input),
            personaTypes = PersonaTypes,
            personaTypeDefinitions = PersonaTypeDefinitions,
            personaSelectionRules = PersonaSelectionRules
        };
        return $$$"""
        任务：只生成观影人格卡片。
        输出 JSON：
        {"persona":{"type":"类型探索家","title":"类型探索家","description":"","confidence":0}}

        要求：
        1. persona.type 只能是以下集合之一：{{{string.Join("、", PersonaTypes)}}}。
        2. 必须参考 personaTypeDefinitions 和 personaSelectionRules 选择最强差异化人格，不要只按题材表面相似度选择。
        3. persona.description 要解释为什么归为该人格，必须结合观看、喜爱、想看或不想看等行为信号。
        4. 不要简单罗列关键词，也不要写成标签堆叠。
        5. confidence 范围 0-100。

        输入数据：
        {{{JsonSerializer.Serialize(payload, JsonOptions)}}}
        """;
    }

    private static string BuildDnaCardPrompt(WatchProfileInputSnapshot input)
    {
        var payload = new
        {
            data = BuildProfileEvidencePayload(input),
            requiredDnaGenes = DnaGenes,
            narrativeTags = NarrativeTags
        };
        return $$$"""
        任务：只生成观影 DNA 卡片。
        输出 JSON：
        {"dna":[
          {"gene":"类型基因","label":"","tags":[],"score":0,"description":"","confidence":0},
          {"gene":"情绪基因","label":"","tags":[],"score":0,"description":"","confidence":0},
          {"gene":"场景基因","label":"","tags":[],"score":0,"description":"","confidence":0},
          {"gene":"叙事基因","label":"","tags":[],"score":0,"description":"","confidence":0},
          {"gene":"节奏基因","label":"","tags":[],"score":0,"description":"","confidence":0},
          {"gene":"探索基因","label":"","tags":[],"score":0,"description":"","confidence":0}
        ]}

        要求：
        1. DNA 必须包含六个基因：{{{string.Join("、", DnaGenes)}}}。
        2. 类型基因、情绪基因、场景基因、叙事基因的 tags 各输出 3 个标签。
        3. 叙事基因 tags 只能从 narrativeTags 集合中选择，不要为每部影片生成叙事标签。
        4. 类型/情绪/场景/叙事基因的 description 必须解释标签组合背后的偏好，不要把 tags 改写成一句话。
        5. 节奏基因用 score 表示 0=慢热、100=紧凑；description 必须与 score 方向一致：0-35 慢热，36-64 均衡，65-100 紧凑。
        6. 探索基因用 score 表示 0=稳定、100=新鲜；description 必须与 score 方向一致：0-35 稳定，36-64 平衡，65-100 新鲜。
        7. score、confidence 范围 0-100。

        输入数据：
        {{{JsonSerializer.Serialize(payload, JsonOptions)}}}
        """;
    }

    private static string BuildQuadrantCardPrompt(WatchProfileInputSnapshot input)
    {
        var payload = new
        {
            data = BuildProfileEvidencePayload(input),
            quadrant = new
            {
                xAxis = "-100=熟悉安全，100=新鲜探索",
                yAxis = "-100=轻松消遣，100=情绪沉浸",
                scoringOwner = "AI must output xAxisScore and yAxisScore from the provided data; service only clamps range."
            }
        };
        return $$$"""
        任务：只生成口味象限卡片。
        输出 JSON：
        {"quadrant":{"xAxisScore":0,"yAxisScore":0,"quadrantName":"","description":""}}

        要求：
        1. xAxisScore、yAxisScore 必须由你输出，范围 -100 到 100；缺失或非数字会被视为画像生成错误。
        2. X 轴：-100=熟悉安全，100=新鲜探索。
        3. Y 轴：-100=轻松消遣，100=情绪沉浸。
        4. quadrantName 使用对应象限名称。
        5. quadrant.description 必须解释分数依据，不要提内部字段名。

        输入数据：
        {{{JsonSerializer.Serialize(payload, JsonOptions)}}}
        """;
    }

    private static string BuildWatchVsLikeCardPrompt(WatchProfileInputSnapshot input)
    {
        var payload = new
        {
            localRankings = new
            {
                oftenWatchedTypes = input.StatisticsSummary.OftenWatchedTypes,
                oftenLikedTypes = input.StatisticsSummary.OftenLikedTypes,
                oftenWantedTypes = input.StatisticsSummary.OftenWantedTypes
            },
            data = BuildProfileEvidencePayload(input)
        };
        return $$$"""
        任务：只生成“看得多 vs 真喜欢”卡片的结论文案。
        输出 JSON：
        {"watchVsLike":{"oftenWatchedTypes":[],"oftenLikedTypes":[],"oftenWantedTypes":[],"conclusion":""}}

        要求：
        1. oftenWatchedTypes、oftenLikedTypes、oftenWantedTypes 三个排行由本地统计提供，服务层会用 localRankings 覆盖，不要虚构排行。
        2. 你只需要写 conclusion，一句话解释“经常观看 / 经常喜爱 / 经常想看”之间的行为差异。
        3. conclusion 不要超过 120 字，不要输出推荐片单。

        输入数据：
        {{{JsonSerializer.Serialize(payload, JsonOptions)}}}
        """;
    }

    private static object BuildProfileEvidencePayload(WatchProfileInputSnapshot input)
    {
        return new
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
    }

    private static void ApplyFixedPersonaFallback(WatchProfileSnapshot profile)
    {
        profile.Persona ??= new WatchProfilePersona();
        profile.Persona.Type = FallbackPersonaType;
        profile.Persona.Title = FallbackPersonaTitle;
        profile.Persona.Description = FallbackPersonaDescription;
        profile.Persona.Confidence = Clamp(profile.Persona.Confidence == 0 ? 50 : profile.Persona.Confidence, 0, 100);
    }

    private static string BuildFixedPersonaFallbackDescription(WatchProfileInputSnapshot input)
    {
        return input.SignalMovieCount >= 8
            ? FallbackPersonaDescription
            : "当前样本还不够集中，暂时更适合作为类型探索家处理。";
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
