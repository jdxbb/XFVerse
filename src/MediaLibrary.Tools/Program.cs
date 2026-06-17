using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediaLibrary.Core.Data;
using MediaLibrary.Core.Models.Entities;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;
using MediaLibrary.Core.Services.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Tools;

internal static class Program
{
    private const string PackageSeedSource = "PackageSeed";
    private const string ProfileKind = "profile";
    private const string ProfileScopeKey = "global";
    private const int CurrentProfileSchemaVersion = 2;
    private const string CurrentPromptVersion = "wi-profile-persona-23-parallel-v18-persona-description-longer";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            WriteUsage();
            return 0;
        }

        try
        {
            var command = args[0].Trim();
            var options = ParseOptions(args.Skip(1));
            return command switch
            {
                "package-test-data" => await PackageTestDataAsync(options),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"tool-error type={exception.GetType().Name} message={exception.Message}");
            return 1;
        }
    }

    private static async Task<int> PackageTestDataAsync(IReadOnlyDictionary<string, string> options)
    {
        var sourceDatabase = RequireOption(options, "source-db");
        var targetDatabase = RequireOption(options, "target-db");
        var reportPath = GetOption(options, "report");
        var preserveProfileCache = HasOption(options, "preserve-profile-cache");

        if (!File.Exists(sourceDatabase))
        {
            throw new FileNotFoundException("Source database was not found.", sourceDatabase);
        }

        var targetDirectory = Path.GetDirectoryName(Path.GetFullPath(targetDatabase));
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("Target database directory could not be resolved.");
        }

        Directory.CreateDirectory(targetDirectory);
        if (File.Exists(targetDatabase))
        {
            File.Delete(targetDatabase);
        }

        BackupSqliteDatabase(sourceDatabase, targetDatabase);
        var report = await SeedPackageDataAsync(targetDatabase, preserveProfileCache);

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            await File.WriteAllTextAsync(
                reportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    WriteIndented = true
                }));
        }

        Console.WriteLine(
            "package-test-data-complete "
            + $"playableMovies={report.PlayableMovieCount} "
            + $"watchHistoriesAdded={report.WatchHistoryRowsAdded} "
            + $"collectionItemsUpserted={report.CollectionItemsUpserted} "
            + $"profileSignalMovies={report.ProfileSignalMovieCount} "
            + $"profileCanGenerate={report.ProfileCanGenerate.ToString().ToLowerInvariant()} "
            + $"profileCachePreserved={report.ProfileCachePreserved.ToString().ToLowerInvariant()}");
        return 0;
    }

    private static void BackupSqliteDatabase(string sourceDatabase, string targetDatabase)
    {
        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourceDatabase,
            Mode = SqliteOpenMode.ReadOnly
        };
        var targetBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = targetDatabase,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        using var source = new SqliteConnection(sourceBuilder.ToString());
        using var target = new SqliteConnection(targetBuilder.ToString());
        source.Open();
        target.Open();
        source.BackupDatabase(target);
    }

    private static async Task<PackageSeedReport> SeedPackageDataAsync(string targetDatabase, bool preserveProfileCache)
    {
        var options = AppDbContextOptionsFactory.Create(targetDatabase);
        await using var dbContext = new AppDbContext(options);

        var nowUtc = DateTime.UtcNow;
        var allMovies = await dbContext.Movies
            .Include(x => x.MediaFiles)
            .Where(x => x.TmdbId.HasValue
                && x.TmdbId.Value > 0
                && !string.IsNullOrWhiteSpace(x.Title)
                && (x.IdentificationStatus == IdentificationStatus.Matched
                    || x.IdentificationStatus == IdentificationStatus.ManualConfirmed))
            .ToListAsync();

        var playableMovies = SelectPlayableEpicMovies(allMovies);
        var canonicalItems = UpsertCanonicalEpicCollectionItems(dbContext, allMovies, nowUtc);
        var watchHistoryRowsAdded = AddWatchHistories(dbContext, playableMovies, nowUtc);
        ApplyMovieState(dbContext, playableMovies, nowUtc);
        if (!preserveProfileCache)
        {
            MarkInsightCachesStale(dbContext, nowUtc);
        }
        await dbContext.SaveChangesAsync();

        WatchProfileInputSnapshot? input = null;
        if (!preserveProfileCache)
        {
            var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(targetDatabase));
            if (!string.IsNullOrWhiteSpace(dataDirectory))
            {
                Environment.SetEnvironmentVariable("XFVERSE_APPDATA_DIR", dataDirectory);
            }

            var cacheService = new WatchInsightCacheService();
            var statisticsService = new WatchStatisticsService(cacheService);
            var profileInputService = new WatchProfileInputService(statisticsService);
            input = await profileInputService.BuildProfileInputAsync();
            await UpsertEpicProfileCacheAsync(targetDatabase, input, nowUtc);
        }

        return new PackageSeedReport
        {
            PlayableMovieCount = playableMovies.Count,
            PlayableMovieTitles = playableMovies.Select(x => x.Movie.Title).ToList(),
            CanonicalCollectionTitles = canonicalItems.Select(x => x.Title).ToList(),
            WatchHistoryRowsAdded = watchHistoryRowsAdded,
            CollectionItemsUpserted = canonicalItems.Count,
            ProfileSignalMovieCount = input?.SignalMovieCount ?? 0,
            ProfileCanGenerate = input?.CanGenerateProfile ?? false,
            ProfileInsufficientReason = input?.InsufficientReason ?? "preserved-current-cache",
            ProfileCachePreserved = preserveProfileCache
        };
    }

    private static List<PlayableMovieSeed> SelectPlayableEpicMovies(IEnumerable<Movie> movies)
    {
        var playable = movies
            .Select(movie => new PlayableMovieSeed(movie, SelectPlayableMediaFile(movie), ScoreEpicMovie(movie)))
            .Where(x => x.MediaFile is not null)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Movie.RuntimeMinutes ?? 0)
            .ThenBy(x => x.Movie.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = playable
            .Where(x => x.Score > 0)
            .Take(8)
            .ToList();

        if (selected.Count < 8)
        {
            var selectedIds = selected.Select(x => x.Movie.Id).ToHashSet();
            selected.AddRange(playable.Where(x => !selectedIds.Contains(x.Movie.Id)).Take(8 - selected.Count));
        }

        return selected;
    }

    private static MediaFile? SelectPlayableMediaFile(Movie movie)
    {
        var candidates = movie.MediaFiles
            .Where(x => !x.IsDeleted && x.MediaType == MediaType.Video)
            .OrderByDescending(x => x.Id == movie.DefaultMediaFileId)
            .ThenByDescending(x => x.DurationSeconds ?? 0)
            .ThenBy(x => x.Id)
            .ToList();

        return candidates.FirstOrDefault();
    }

    private static int ScoreEpicMovie(Movie movie)
    {
        var text = string.Join(
            " ",
            movie.Title,
            movie.OriginalTitle,
            movie.GenresText,
            movie.AiTagsText,
            movie.EmotionTagsText,
            movie.SceneTagsText,
            movie.Overview);

        var score = 0;
        score += CountMatches(text, ["指环王", "魔戒", "lord of the rings", "霍比特", "hobbit", "中土", "middle-earth"]) * 100;
        score += CountMatches(text, ["沙丘", "dune", "纳尼亚", "narnia", "阿凡达", "avatar"]) * 40;
        score += CountMatches(text, ["史诗", "奇幻", "冒险", "世界观", "神话", "远征", "文明", "战争", "历史"]) * 20;
        score += movie.RuntimeMinutes is >= 150 ? 6 : 0;
        return score;
    }

    private static int CountMatches(string text, IEnumerable<string> terms)
    {
        return terms.Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static List<UserMovieCollectionItem> UpsertCanonicalEpicCollectionItems(
        AppDbContext dbContext,
        IReadOnlyCollection<Movie> movies,
        DateTime nowUtc)
    {
        var canonical = new[]
        {
            new CanonicalEpicMovie(120, "tt0120737", "指环王：护戒使者", "The Lord of the Rings: The Fellowship of the Ring", 2001, 178, true),
            new CanonicalEpicMovie(121, "tt0167261", "指环王：双塔奇兵", "The Lord of the Rings: The Two Towers", 2002, 179, true),
            new CanonicalEpicMovie(122, "tt0167260", "指环王：王者无敌", "The Lord of the Rings: The Return of the King", 2003, 201, true),
            new CanonicalEpicMovie(49051, "tt0903624", "霍比特人：意外之旅", "The Hobbit: An Unexpected Journey", 2012, 169, true),
            new CanonicalEpicMovie(57158, "tt1170358", "霍比特人：史矛革之战", "The Hobbit: The Desolation of Smaug", 2013, 161, true),
            new CanonicalEpicMovie(122917, "tt2310332", "霍比特人：五军之战", "The Hobbit: The Battle of the Five Armies", 2014, 144, true),
            new CanonicalEpicMovie(438631, "tt1160419", "沙丘", "Dune", 2021, 155, false),
            new CanonicalEpicMovie(693134, "tt15239678", "沙丘2", "Dune: Part Two", 2024, 166, false)
        };

        var seededItems = new List<UserMovieCollectionItem>();
        for (var index = 0; index < canonical.Length; index++)
        {
            var item = canonical[index];
            var changedAt = ToUtc(2026, index < 4 ? 4 : 5, index < 4 ? 6 + index * 6 : 4 + (index - 4) * 6, 21, 10);
            var existingMovie = movies.FirstOrDefault(x => x.TmdbId == item.TmdbId);
            var collectionItem = dbContext.UserMovieCollectionItems
                .FirstOrDefault(x => x.TmdbId == item.TmdbId || (existingMovie != null && x.MovieId == existingMovie.Id));

            if (collectionItem is null)
            {
                collectionItem = new UserMovieCollectionItem
                {
                    MovieId = existingMovie?.Id,
                    TmdbId = item.TmdbId,
                    Title = existingMovie?.Title ?? item.Title,
                    OriginalTitle = existingMovie?.OriginalTitle ?? item.OriginalTitle,
                    ReleaseYear = existingMovie?.ReleaseYear ?? item.ReleaseYear,
                    ReleaseDate = existingMovie?.ReleaseDate,
                    Overview = existingMovie?.Overview ?? string.Empty,
                    PosterRemoteUrl = existingMovie?.PosterRemoteUrl ?? string.Empty,
                    GenresText = MergeTags(existingMovie?.GenresText, "奇幻", "冒险", "史诗"),
                    Country = existingMovie?.Country ?? "新西兰、美国",
                    Language = existingMovie?.Language ?? "英语",
                    RuntimeMinutes = existingMovie?.RuntimeMinutes ?? item.RuntimeMinutes,
                    ImdbId = existingMovie?.ImdbId ?? item.ImdbId,
                    IsInLibrary = existingMovie is not null,
                    LibraryVisibilityState = existingMovie is not null ? LibraryVisibilityState.Visible : LibraryVisibilityState.Auto,
                    CreatedAt = changedAt
                };
                dbContext.UserMovieCollectionItems.Add(collectionItem);
            }

            var oldWatched = collectionItem.IsWatched;
            var oldFavorite = collectionItem.IsFavorite;
            collectionItem.MovieId ??= existingMovie?.Id;
            collectionItem.TmdbId = item.TmdbId;
            collectionItem.IsWantToWatch = false;
            collectionItem.IsWatched = true;
            collectionItem.IsFavorite = item.Favorite;
            collectionItem.IsNotInterested = false;
            collectionItem.UpdatedAt = changedAt;

            dbContext.UserMovieStateChangeHistories.Add(CreateStateHistory(
                item.TmdbId,
                existingMovie?.Id,
                collectionItem.Id == 0 ? null : collectionItem.Id,
                collectionItem.Title,
                "Watched",
                oldWatched,
                true,
                changedAt));

            if (item.Favorite)
            {
                dbContext.UserMovieStateChangeHistories.Add(CreateStateHistory(
                    item.TmdbId,
                    existingMovie?.Id,
                    collectionItem.Id == 0 ? null : collectionItem.Id,
                    collectionItem.Title,
                    "Favorite",
                    oldFavorite,
                    true,
                    changedAt.AddMinutes(2)));
            }

            seededItems.Add(collectionItem);
        }

        return seededItems;
    }

    private static int AddWatchHistories(
        AppDbContext dbContext,
        IReadOnlyList<PlayableMovieSeed> movies,
        DateTime nowUtc)
    {
        if (movies.Count == 0)
        {
            return 0;
        }

        var starts = new[]
        {
            ToUtc(2026, 4, 4, 20, 15),
            ToUtc(2026, 4, 11, 19, 55),
            ToUtc(2026, 4, 18, 20, 30),
            ToUtc(2026, 4, 26, 18, 40),
            ToUtc(2026, 5, 3, 20, 5),
            ToUtc(2026, 5, 10, 19, 45),
            ToUtc(2026, 5, 18, 21, 0),
            ToUtc(2026, 5, 24, 20, 20),
            ToUtc(2026, 5, 30, 21, 10)
        };

        var added = 0;
        for (var index = 0; index < starts.Length; index++)
        {
            var seed = movies[index % movies.Count];
            if (seed.MediaFile is null)
            {
                continue;
            }

            var startedAt = starts[index];
            if (startedAt >= nowUtc)
            {
                continue;
            }

            var durationSeconds = ResolveDurationSeconds(seed.Movie, seed.MediaFile);
            var watchedSeconds = Math.Clamp((int)Math.Round(durationSeconds * (index % 3 == 0 ? 0.92 : 0.98)), 600, durationSeconds);
            var endedAt = startedAt.AddSeconds(watchedSeconds);
            if (endedAt >= nowUtc)
            {
                endedAt = nowUtc.AddMinutes(-15);
                watchedSeconds = Math.Max(600, (int)Math.Round((endedAt - startedAt).TotalSeconds));
            }

            var exists = dbContext.WatchHistories.Any(x =>
                x.MovieId == seed.Movie.Id
                && x.MediaFileId == seed.MediaFile.Id
                && x.StartedAt == startedAt);
            if (exists)
            {
                continue;
            }

            dbContext.WatchHistories.Add(new WatchHistory
            {
                MovieId = seed.Movie.Id,
                EpisodeId = null,
                MediaFileId = seed.MediaFile.Id,
                StartedAt = startedAt,
                EndedAt = endedAt,
                LastPlayPositionSeconds = Math.Min(durationSeconds, watchedSeconds),
                DurationWatchedSeconds = watchedSeconds,
                IsCompleted = true,
                CreatedAt = startedAt
            });
            added++;
        }

        return added;
    }

    private static void ApplyMovieState(
        AppDbContext dbContext,
        IReadOnlyList<PlayableMovieSeed> movies,
        DateTime nowUtc)
    {
        for (var index = 0; index < movies.Count; index++)
        {
            var seed = movies[index];
            var movie = seed.Movie;
            var oldWatched = movie.IsWatched;
            var oldFavorite = movie.IsFavorite;
            var changedAt = ToUtc(2026, index < 4 ? 4 : 5, index < 4 ? 7 + index * 5 : 6 + (index - 4) * 5, 22, 20);

            movie.IsWatched = true;
            movie.IsFavorite = index < 5 || seed.Score >= 80;
            movie.UserRating = Math.Max(movie.UserRating ?? 0d, Math.Round(9.4d - (index * 0.12d), 1));
            movie.LastPlayedAt = dbContext.WatchHistories
                .Where(x => x.MovieId == movie.Id)
                .Select(x => x.EndedAt ?? x.StartedAt)
                .OrderByDescending(x => x)
                .FirstOrDefault();
            movie.AutoWatchedBaselineAtUtc = movie.AutoWatchedBaselineAtUtc ?? changedAt;
            movie.AiTagsText = MergeTags(movie.AiTagsText, "史诗", "奇幻", "冒险", "世界观");
            movie.EmotionTagsText = MergeTags(movie.EmotionTagsText, "震撼", "热血", "沉浸");
            movie.SceneTagsText = MergeTags(movie.SceneTagsText, "宏大世界", "远征", "神话文明");
            movie.UpdatedAt = changedAt;

            if (movie.TmdbId is > 0)
            {
                dbContext.UserMovieStateChangeHistories.Add(CreateStateHistory(
                    movie.TmdbId.Value,
                    movie.Id,
                    null,
                    movie.Title,
                    "Watched",
                    oldWatched,
                    true,
                    changedAt));

                if (movie.IsFavorite)
                {
                    dbContext.UserMovieStateChangeHistories.Add(CreateStateHistory(
                        movie.TmdbId.Value,
                        movie.Id,
                        null,
                        movie.Title,
                        "Favorite",
                        oldFavorite,
                        true,
                        changedAt.AddMinutes(3)));
                }
            }
        }
    }

    private static UserMovieStateChangeHistory CreateStateHistory(
        int tmdbId,
        int? movieId,
        int? collectionItemId,
        string? title,
        string stateType,
        bool oldValue,
        bool newValue,
        DateTime changedAtUtc)
    {
        return new UserMovieStateChangeHistory
        {
            TmdbId = tmdbId,
            MovieId = movieId,
            UserMovieCollectionItemId = collectionItemId,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            StateType = stateType,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAtUtc = changedAtUtc,
            Source = PackageSeedSource,
            CreatedAtUtc = changedAtUtc
        };
    }

    private static void MarkInsightCachesStale(AppDbContext dbContext, DateTime nowUtc)
    {
        foreach (var entry in dbContext.WatchInsightCacheEntries)
        {
            entry.IsStale = true;
            entry.UpdatedAtUtc = nowUtc;
        }
    }

    private static async Task UpsertEpicProfileCacheAsync(
        string targetDatabase,
        WatchProfileInputSnapshot input,
        DateTime nowUtc)
    {
        await using var dbContext = new AppDbContext(AppDbContextOptionsFactory.Create(targetDatabase));
        var profile = CreateEpicProfile(input, nowUtc);
        var payloadJson = JsonSerializer.Serialize(profile, JsonOptions);
        var entry = await dbContext.WatchInsightCacheEntries
            .FirstOrDefaultAsync(x => x.Kind == ProfileKind && x.ScopeKey == ProfileScopeKey);

        if (entry is null)
        {
            entry = new WatchInsightCacheEntry
            {
                Kind = ProfileKind,
                ScopeKey = ProfileScopeKey,
                CreatedAtUtc = nowUtc
            };
            dbContext.WatchInsightCacheEntries.Add(entry);
        }

        entry.PayloadJson = payloadJson;
        entry.SourceFingerprint = input.SourceFingerprint;
        entry.RefreshedAtUtc = nowUtc;
        entry.ExpiresAtUtc = null;
        entry.IsStale = false;
        entry.LastError = null;
        entry.LastAutoRefreshAtUtc = nowUtc;
        entry.UpdatedAtUtc = nowUtc;
        await dbContext.SaveChangesAsync();
    }

    private static WatchProfileSnapshot CreateEpicProfile(WatchProfileInputSnapshot input, DateTime nowUtc)
    {
        return new WatchProfileSnapshot
        {
            Meta = new WatchProfileMeta
            {
                GeneratedAtUtc = nowUtc,
                SourceFingerprint = input.SourceFingerprint,
                ProfileSchemaVersion = CurrentProfileSchemaVersion,
                PromptVersion = CurrentPromptVersion,
                SignalMovieCount = input.SignalMovieCount,
                Confidence = 88
            },
            LoadedFromCache = false,
            HasProfile = true,
            CanGenerateProfile = true,
            WasAiCalled = false,
            IsCacheHit = false,
            StatusMessage = "测试包内置画像：偏好宏大世界观、史诗叙事和长线冒险。",
            Summary = new WatchProfileSummary
            {
                Text = "你的观影轨迹明显偏向宏大世界观、长篇冒险和文明史诗。你愿意把时间交给设定扎实、人物群像清晰、情绪回响强的影片，尤其容易被远征、神话、战争与命运抉择交织的故事吸引。",
                Keywords = ["史诗", "奇幻", "冒险", "世界观", "远征", "群像"],
                KeywordScores =
                [
                    new WatchProfileKeyword { Label = "史诗", Score = 95 },
                    new WatchProfileKeyword { Label = "奇幻", Score = 92 },
                    new WatchProfileKeyword { Label = "冒险", Score = 88 },
                    new WatchProfileKeyword { Label = "世界观", Score = 87 },
                    new WatchProfileKeyword { Label = "群像", Score = 79 }
                ]
            },
            Persona = new WatchProfilePersona
            {
                Type = "史诗世界观派",
                Title = "史诗世界观派",
                Lead = "你更容易被一个完整世界的重量和命运感带入。",
                Description = "你偏爱设定宏大、历史感清晰、角色共同承担使命的电影。相比短平快的情节刺激，你更享受世界逐步展开、阵营关系成形、人物在旅途中完成选择的过程。",
                Confidence = 90
            },
            DNA =
            [
                new WatchProfileDnaGene { Gene = "类型基因", Label = "奇幻史诗", Tags = ["奇幻", "冒险", "史诗"], Score = 94, Confidence = 90, Description = "高频信号集中在远征、神话文明与大型系列叙事。" },
                new WatchProfileDnaGene { Gene = "情绪基因", Label = "震撼沉浸", Tags = ["震撼", "热血", "沉浸"], Score = 88, Confidence = 86, Description = "更看重情绪余波和命运感，而不是单场戏的刺激。" },
                new WatchProfileDnaGene { Gene = "场景基因", Label = "宏大世界", Tags = ["宏大世界", "战争", "神话文明"], Score = 91, Confidence = 88, Description = "偏爱有地理、种族、文明和阵营层次的故事空间。" },
                new WatchProfileDnaGene { Gene = "叙事基因", Label = "远征群像", Tags = ["远征", "群像叙事", "史诗叙事"], Score = 89, Confidence = 86, Description = "长线任务、伙伴关系和牺牲选择是核心吸引点。" },
                new WatchProfileDnaGene { Gene = "节奏基因", Label = "长线铺陈", Tags = ["长片", "系列", "章节叙事"], Score = 82, Confidence = 80, Description = "可以接受慢热铺垫，只要世界观和角色回报足够强。" },
                new WatchProfileDnaGene { Gene = "探索基因", Label = "神话文明", Tags = ["异世界", "文明感", "设定控"], Score = 85, Confidence = 83, Description = "对规则自洽、文化细节和历史纵深更敏感。" }
            ],
            Quadrant = new WatchProfileQuadrant
            {
                XAxisScore = Math.Max(input.LocalXAxisScore, 66),
                YAxisScore = Math.Max(input.LocalYAxisScore, 70),
                QuadrantName = "新鲜探索 × 情绪沉浸",
                Description = "你倾向进入陌生但完整的世界，并通过角色命运获得情绪沉浸。"
            },
            WatchVsLike = new WatchProfileWatchVsLike
            {
                OftenWatchedTypes = ["奇幻", "冒险", "史诗"],
                OftenLikedTypes = ["史诗", "世界观", "群像叙事"],
                OftenWantedTypes = ["科幻幻想", "历史战争", "大型系列"],
                Conclusion = "看得多和真正喜欢的方向高度重合，说明史诗感不是偶然偏好，而是稳定口味。"
            },
            Likes = new WatchProfileLikes
            {
                PreferredGenres = ["奇幻", "冒险", "史诗", "科幻幻想"],
                PreferredEmotions = ["震撼", "热血", "沉浸", "悲壮"],
                PreferredScenes = ["宏大世界", "远征", "战争", "神话文明"],
                PreferredCountries = ["新西兰", "美国", "英国"],
                PreferredLanguages = ["英语"]
            },
            Dislikes = new WatchProfileDislikes
            {
                AvoidGenres = ["低设定密度"],
                AvoidEmotions = ["无余韵"],
                AvoidScenes = ["单薄世界观"],
                NegativeSummary = "过于碎片化、缺少世界观支撑或人物使命感不足的作品，通常不如史诗冒险稳定命中。"
            },
            FuturePreference = new WatchProfileFuturePreference
            {
                LikelyToEnjoy = ["宏大奇幻系列", "文明冲突题材", "长线冒险叙事", "神话史诗"],
                LessLikelyToEnjoy = ["纯即时爽感但缺少设定回报的影片", "角色关系单薄的轻体量故事"]
            },
            Caveats = ["这是测试安装包预置画像，用于验证观影洞察和推荐链路。"]
        };
    }

    private static int ResolveDurationSeconds(Movie movie, MediaFile mediaFile)
    {
        if (mediaFile.DurationSeconds is > 0)
        {
            return mediaFile.DurationSeconds.Value;
        }

        if (movie.RuntimeMinutes is > 0)
        {
            return movie.RuntimeMinutes.Value * 60;
        }

        return 8_400;
    }

    private static DateTime ToUtc(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(8)).UtcDateTime;
    }

    private static string MergeTags(string? existing, params string[] tags)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            values.AddRange(existing.Split(['、', ',', '，', ';', '；', '|', '/'], StringSplitOptions.RemoveEmptyEntries));
        }

        values.AddRange(tags);
        return string.Join(
            "、",
            values
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20));
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentKey = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                currentKey = arg[2..];
                options[currentKey] = string.Empty;
                continue;
            }

            if (currentKey is null)
            {
                throw new ArgumentException($"Unexpected argument: {arg}");
            }

            options[currentKey] = arg;
            currentKey = null;
        }

        return options;
    }

    private static string RequireOption(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required option --{name}.");
        }

        return value;
    }

    private static string? GetOption(IReadOnlyDictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    private static bool HasOption(IReadOnlyDictionary<string, string> options, string name)
    {
        return options.ContainsKey(name);
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        WriteUsage();
        return 1;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("MediaLibrary.Tools commands:");
        Console.WriteLine("  package-test-data --source-db <path> --target-db <path> [--report <path>] [--preserve-profile-cache]");
    }

    private sealed record PlayableMovieSeed(Movie Movie, MediaFile? MediaFile, int Score);

    private sealed record CanonicalEpicMovie(
        int TmdbId,
        string ImdbId,
        string Title,
        string OriginalTitle,
        int ReleaseYear,
        int RuntimeMinutes,
        bool Favorite);

    private sealed class PackageSeedReport
    {
        public int PlayableMovieCount { get; set; }

        public List<string> PlayableMovieTitles { get; set; } = [];

        public List<string> CanonicalCollectionTitles { get; set; } = [];

        public int WatchHistoryRowsAdded { get; set; }

        public int CollectionItemsUpserted { get; set; }

        public int ProfileSignalMovieCount { get; set; }

        public bool ProfileCanGenerate { get; set; }

        public string ProfileInsufficientReason { get; set; } = string.Empty;

        public bool ProfileCachePreserved { get; set; }
    }
}
