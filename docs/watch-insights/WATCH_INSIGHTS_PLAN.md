# Watch Insights Plan

## Stage Goal

Watch Insights（观影洞察）是播放器主线、Library Batch Ops 和 Recommendation Feedback 收口后的新阶段，目标是让用户基于真实本地观影数据理解自己的观影习惯、偏好画像和统计趋势。

第一版目标：
- 做真实数据功能版。
- 功能完整优先，UI 先够用，不追最终视觉。
- 页面最终包含两个 Tab：`画像分析`、`观影统计`。
- 默认打开 `画像分析`。
- 可以显示组件级空状态；有数据时必须正常显示。
- 当前不接画像驱动推荐，画像驱动推荐后续单独接入。
- 观影历史页面尚未完成，日历日期点击跳转接口后续预留和接入。

## Sub-Stages

### WI-0: Read-Only Audit / Data Capability Assessment

Status:
- Completed before WI-1.

Result:
- Confirmed current models include `Movie`, `MediaFile`, `WatchHistory`, `UserMovieCollectionItem`, rating sources, TMDB identity, AI classification tags, country, language, runtime, and playback history fields.
- Confirmed most statistics can be computed from existing data.
- Confirmed dedicated statistics/profile cache storage is missing.
- Confirmed current navigation has no Watch Insights entry and no global collapse/expand implementation in source.
- Confirmed `WatchHistory.IsCompleted` cannot be the only completion source because current local data may contain watch history with no completed rows.

### WI-1: Stage Docs + Cache Infrastructure

Status:
- Completed in this pass.

Scope:
- Create independent Watch Insights documentation under `docs/watch-insights`.
- Add `WatchInsightCacheEntry` EF entity and configuration.
- Add `WatchInsightCacheEntries` DbSet.
- Add `AddWatchInsightCacheEntries` EF migration.
- Add `IWatchInsightCacheService` and `WatchInsightCacheService`.
- Register cache service in DI.
- Apply migration.
- Build with 0 warnings and 0 errors.

Out of scope:
- No Watch Insights page.
- No navigation entry.
- No statistics query engine.
- No statistics Tab.
- No profile analysis Tab.
- No AI calls.
- No AI profile generation.
- No automatic watched algorithm implementation.
- No player changes.
- No Library Batch Ops changes.
- No Recommendation Feedback or recommendation-system changes.
- No scan main-flow changes.
- No final UI refactor.
- No `UserMovieCollectionItem` AI tag snapshot fields.
- No automatic watched setting field.
- No test data.

### WI-2: Watch Statistics Query Engine

Status:
- Completed in this pass.

Scope completed:
- Added real-data statistics read models.
- Added `IWatchStatisticsService` and `WatchStatisticsService`.
- Computed Watch Statistics Tab data from existing `Movie`, `MediaFile`, `WatchHistory`, `UserMovieCollectionItem`, and `RatingSource` data.
- Added statistics source fingerprinting.
- Used `WatchInsightCacheEntries` with `kind=statistics` and `scopeKey=global`.
- Added 12-hour statistics cache expiry.
- Supported manual refresh through `forceRefresh=true`.
- Supported cache miss handling for missing, stale, expired, fingerprint-changed, and deserialize-failed cache states.

Out of scope kept:
- No Watch Insights page.
- No navigation entry.
- No profile analysis Tab.
- No AI call or AI profile generation.
- No automatic watched algorithm implementation.
- No database field or migration.
- No player, recommendation, Library Batch Ops, or scan main-flow changes.

### WI-3: Watch Insights Page Shell + Statistics Tab

Planned scope:
- Add navigation entry named `观影洞察`.
- Add page shell with title `观影洞察` and subtitle `让你更懂你`.
- Add two Tabs and default to `画像分析`.
- Implement statistics Tab using the WI-2 query service and cached data.

Status:
- Completed in this pass.

Scope completed:
- Added left navigation entry named `观影洞察`.
- Added `WatchInsightsPage` and `WatchInsightsViewModel`.
- Added page title `观影洞察` and subtitle `让你更懂你`.
- Added two Tabs: `画像分析` and `观影统计`.
- Default Tab is `画像分析` whenever the page is activated.
- Profile analysis Tab shows component-level empty/deferred states and does not call AI.
- Statistics Tab binds to real WI-2 `IWatchStatisticsService` data.
- Added manual statistics refresh, loading state, warning display, and component-level empty states.

Out of scope kept:
- No AI call or AI profile generation.
- No profile cache refresh.
- No automatic watched algorithm implementation.
- No database field or migration.
- No WatchHistory write-chain change.
- No watch history page.
- Calendar date click is a placeholder notice only.
- Previous/next calendar month buttons are present but disabled until month-scoped statistics are introduced.

### WI-3 Polish: Statistics/Profile Refresh Split

Status:
- Completed in this pass.

Scope completed:
- Clarified that Watch Statistics refresh and Profile refresh are independent.
- Moved the manual refresh action into the Watch Statistics area so it only means `刷新统计`.
- Manual statistics refresh calls `IWatchStatisticsService.GetStatisticsAsync(forceRefresh:true)` only.
- Statistics refresh does not trigger profile refresh, profile cache refresh, AI generation, or recommendation changes.
- `WatchInsightsViewModel` listens to local data changes through `IDataRefreshService`.
- Library, metadata, collection, scan, and playback-history changes can refresh statistics after a short debounce.
- If the page is inactive, statistics refresh is deferred until the next activation.
- Concurrent refreshes are merged so multiple statistics refresh tasks are not started at once.

Profile refresh rule reserved:
- Profile analysis remains a separate later-stage workflow.
- Future automatic profile refresh requires source data changes and at least 1 day since the last automatic profile refresh.
- Future manual profile refresh is not constrained by the 1-day automatic refresh interval.
- WI-3 polish does not add a profile refresh button and does not call AI.

### WI-4: Automatic Watched Algorithm

Status:
- Completed in this pass.

Scope completed:
- Implement automatic watched detection based on playback progress, watch duration, end tolerance, and multi-session merge rules.
- Keep automatic marking conservative and user-state preserving.
- Single-session completion requires near-end progress, either 90% playback progress or within 300 seconds of the end, plus minimum real watched duration.
- Minimum real watched duration is `min(20 minutes, 25% runtime)`.
- Multi-session completion can mark the movie watched when effective history totals at least 80% runtime and max progress reaches at least 70% runtime.
- `WatchHistory.IsCompleted` is set for the current run only when the current run itself completes or an external completed signal is true.
- Multi-session completion can set `Movie.IsWatched=true`, but does not label a short current run as a completed watch.
- Automatic watched only writes `false -> true`; it never automatically writes `true -> false`.
- External player `isCompleted=true` is not allowed to bypass the watched-duration threshold. It is treated as a signal for diagnostics/aggregation, and completion still requires evaluator validation.
- Aggregate max progress is calculated only from effective watch runs, so a pure seek to the ending cannot contribute the 70% aggregate position requirement.

Out of scope kept:
- No AI call or AI profile generation.
- No profile-driven recommendation.
- No watch history page.
- No database field or migration.
- No full-history backfill.
- No automatic watched settings UI.
- No player core playback changes.

### WI-4.1: Automatic Watched Baseline + Reset Recognition Boundary

Status:
- Completed and manually revalidated through logs.

Scope completed:
- Added `Movie.AutoWatchedBaselineAtUtc` as the persistent baseline for automatic watched aggregation.
- Manual library watched=false now sets `AutoWatchedBaselineAtUtc` to current UTC time.
- Linked collection watched=false can set the same movie baseline when the collection item has a `MovieId`.
- Automatic watched aggregate checks only use `WatchHistory.StartedAt > AutoWatchedBaselineAtUtc` when the baseline exists.
- Old `WatchHistory` rows are retained and still available to statistics, profile analysis, and future watch history pages.
- Resetting a media file to unidentified now moves only the `MediaFile` to a new unidentified placeholder and does not move old `WatchHistory`.
- The reset placeholder does not inherit old movie watched/favorite state or auto-watched baseline.
- Manual log validation confirmed baseline application after manual unwatched, reset-to-unidentified with `movedWatchHistory=false`, reset resume clearing, and Batch-2 AI assisted identification success for the reset placeholder path.

Out of scope kept:
- No WatchHistory deletion.
- No full-history backfill.
- No UI or settings screen.
- No AI call or profile generation.
- No recommendation, player-core, scan-main-flow, or statistics口径 change.

### WI-5: AI Profile Cache and Generation

Status:
- Completed in this pass.

Scope completed:
- Added profile input aggregation from watched, liked, want-to-watch, not-interested, valid watch history, labels, rating sources, and WI-2 local statistics.
- Added data sufficiency checks before any AI call.
- Added structured AI profile output read models.
- Added `IWatchProfileInputService` / `WatchProfileInputService`.
- Added `IWatchProfileService` / `WatchProfileService`.
- Stored profile cache in `WatchInsightCacheEntries` using `kind=profile` and `scopeKey=global`.
- Added profile source fingerprinting.
- Added manual refresh through `GetProfileAsync(forceRefresh:true)`.
- Added automatic refresh through `GetProfileAsync(forceRefresh:false)` when source fingerprint changes and the last automatic refresh is at least 1 day old.
- Kept manual refresh independent from the 1-day automatic refresh interval.
- Added AI failure fallback that preserves old cached profile data when available.

Profile rules:
- Data is insufficient when unique signal movies are fewer than 8, effective signal buckets are fewer than 2, or no usable tags exist.
- The AI output must be JSON and is parsed into `WatchProfileSnapshot`.
- `persona.type` is restricted to the final fixed 20-type persona set.
- Watch DNA supports six genes: type, emotion, scene, rhythm, narrative, and exploration.
- Taste quadrant uses a local two-axis score: familiar-safe to fresh-exploratory, and easy-casual to emotional-immersive.
- RF-2 custom recommendation preferences are not included in profile input.

Final persona set:
1. 情绪沉浸者
2. 悬疑解谜者
3. 类型探索家
4. 经典收藏家
5. 治愈陪伴型
6. 高分严选派
7. 作者导演迷
8. 科幻幻想旅人
9. 现实观察者
10. 动作爽片玩家
11. 文艺审美家
12. 多元杂食者
13. 黑色幽默爱好者
14. 浪漫幻想派
15. 暗黑猎奇者
16. 家庭温情派
17. 历史史诗控
18. 动画想象派
19. 犯罪人性派
20. 轻松下饭派

Persona boundary rules:
- The first 10 persona names remain unchanged.
- The later 10 types have explicit boundaries to reduce overlap: broad taste maps to `多元杂食者`, active novelty-seeking maps to `类型探索家`, dark/abnormal atmosphere maps to `暗黑猎奇者`, moral/crime human nature maps to `犯罪人性派`, and low-cost casual viewing maps to `轻松下饭派`.
- If AI returns a persona outside the fixed set, the service falls back to `多元杂食者` and records a warning.
- Persona image/poster assets are not part of WI-5 and remain a later UI asset stage.

Out of scope kept:
- No profile-driven recommendation.
- No recommendation prompt or fingerprint change.
- No page/navigation/UI change.
- No database field or migration.
- No player, delete, Library Batch Ops, scan, or automatic-watched change.

### WI-6: Profile Analysis Tab

Status:
- Completed in this pass.

Scope completed:
- Connected the Profile Analysis Tab to `IWatchProfileService`.
- Page activation starts profile loading through `GetProfileAsync(forceRefresh:false)` without blocking the statistics refresh path.
- Added a profile-only manual refresh button that calls `GetProfileAsync(forceRefresh:true)`.
- Kept profile refresh and statistics refresh separate.
- Rendered real profile modules from `WatchProfileSnapshot`: taste summary, keywords, persona, Watch DNA, two-axis quadrant, and watch-more-vs-like.
- Added UI states for loading, insufficient data, error without cache, and old-cache-with-warning.

WI-6 polish:
- Profile UI is narrowed to the product-approved module set.
- Taste summary displays at most 6 AI-generated keywords.
- Watch DNA display no longer shows confidence. Type, emotion, scene, and narrative genes show up to 3 tags plus one description.
- Narrative tags are generated only in the profile result and are not written to `Movie` or any database field.
- Narrative gene tags are constrained to the fixed profile-level set: `线性叙事`, `多线叙事`, `反转叙事`, `开放结局`, `成长叙事`, `公路叙事`, `群像叙事`, `心理叙事`, `悬念推进`, `章节叙事`, `回忆叙事`, `非线性叙事`, `命运交织`, `日常切片`, `史诗叙事`, `寓言叙事`, `黑色幽默`, `现实观察`, `情绪流动`, `高概念设定`.
- Rhythm DNA displays a progress bar from `慢热` to `紧凑`.
- Exploration DNA displays a progress bar from `稳定` to `新鲜`.
- Persona confidence and DNA confidence remain in the service model but are not shown in the UI because they are AI/self-estimated or normalized confidence values, not rigorous metrics.
- The independent cards for liked directions, disliked directions, future likely directions, future less-likely directions, and caveats are not shown in WI-6 UI. Warnings are limited to a short top status row.
- Watch-more-vs-like labels are `经常观看`, `经常喜爱`, and `经常想看`.
- Manual profile refresh now behaves as "check and update": if the profile input fingerprint has not changed and a valid profile cache exists, the service returns the cache, does not call AI, and reports `画像数据没有变化，已显示最新画像。`.
- Manual refresh still regenerates immediately when the fingerprint changes, cache is missing, or cache cannot be parsed; it remains independent from the 1-day automatic refresh gate.
- A future "force regenerate anyway" advanced action is deferred.

WI-6 DNA/text polish:
- DNA type, emotion, scene, and narrative genes are rendered as up to 3 rounded tag capsules plus one concise description.
- Rhythm and exploration genes are rendered as progress bars plus one concise description.
- DNA descriptions are normalized so they explain the preference behind a tag combination instead of simply repeating the tag labels.
- Rhythm description is kept consistent with the score: low means `慢热`, middle means `适中`, high means `紧凑`.
- Exploration description is kept consistent with the score: low means `稳定`, middle means `平衡`, high means `新鲜`.
- Summary, persona, and DNA prompt instructions now explicitly require non-repetitive product copy and different explanation angles.
- Taste summary text may be longer than other profile modules: 2-4 natural sentences, capped in service projection at a larger limit than persona/DNA text.
- Summary keywords are deduplicated and still capped at 6.
- Watch-more-vs-like rankings are deterministic local statistics from WI-2. AI may write the conclusion, but the three ranking lists are overwritten from local statistics during profile normalization.
- Prompt version `wi-profile-unified-copy-v4` asks Summary, Persona, DNA, and WatchVsLike to share one core profile story while each module explains a different angle.
- Watch DNA is presented as a 3x2 six-cell grid instead of a vertical field list.
- Taste quadrant is presented as a four-area `口味定位图` and no longer exposes raw X/Y score readouts in the UI.
- Profile input excludes unidentified and identification-failed movies from both samples and source fingerprint, including linked collection status rows.
- Profile input fingerprint is semantic rather than row-lifecycle-based: moving a movie out of the library must not change the profile when watched/favorite/want/not-interested/history/rating/tag signals are unchanged.
- Profile type/emotion/scene distributions are built from profile signal samples only, not from every identified movie in the library.
- `WatchProfileMeta.ProfileSchemaVersion` and `WatchProfileMeta.PromptVersion` are stored in profile cache JSON, without adding database fields.
- Cache reuse now requires both matching source fingerprint and matching prompt/schema version.
- If fingerprint is unchanged but prompt/schema version is old, page load returns the old cache with a warning, while manual refresh regenerates once using the new prompt.
- If fingerprint and prompt/schema version are both unchanged, manual refresh still skips AI to prevent content drift.

Out of scope kept:
- No profile-driven recommendation.
- No recommendation prompt/fingerprint change.
- No persona poster/image resources.
- No database field or migration.
- No final visual polish.

### WI-6.1: Statistics Range and Profile Semantics Fix

Status:
- Completed in this pass.

Scope completed:
- Added Watch Statistics time range selector: `本月` and `全部`, defaulting to `本月`.
- Added `UserMovieStateChangeHistories` to record actual user state changes for `已看`, `喜爱`, `想看`, and `不想看`.
- Status overview now displays only `已看`, `喜爱`, `想看`, and `不想看`, and follows the selected `本月 / 全部` statistics range.
- `全部` range shows current state totals and hides comparison text.
- `本月` range shows current-month new state additions from state history, not from `UpdatedAt`.
- A status only counts as a current-month addition when the latest same-month change for that TMDB/state is still `true`; same-month cancellation removes it from the addition count.
- Reset / unidentified placeholder / re-identification paths do not inherit the old `Movie` watched, favorite, want-to-watch, or not-interested state into the newly identified identity.
- Collection status rows linked to an unidentified or identification-failed `Movie` are excluded from Watch Statistics unless they already match a stable identified target identity.
- Moving a marked movie out of the library and scanning it back in does not change status-history timing; scan restores library visibility but does not create a new status addition.
- Deleting a movie record removes its software-owned state history, and Watch Statistics ignores orphaned state-history rows whose movie or collection owner no longer exists.
- Moving a marked movie out of the library preserves its state/history and must not change the long-term profile input fingerprint unless an actual state, history, tag, rating, or metadata signal changed.
- Successful identification uses the target `Movie`'s existing state. If the target was already marked before, that is existing state and is not recorded as a current-month status addition.
- Re-identification of an already identified marked movie does not create a status activation row. Same-TMDB duplicate merges may preserve state as the same movie identity, but different-TMDB reassignment does not transfer status.
- The status comparison text now means `本月比上月`; it no longer uses `本周比上周`.
- New status history before this stage is not backfilled, so old pre-existing true states are not counted as this month's additions.
- Playback-dependent statistics follow the selected range, while the calendar has its own independent displayed month.
- Watch count is distinct watched movies in the selected range, not play-record count.
- Tags, preference graph, tag ranking, and taste combination map count distinct watched movies by tag/combination occurrence.
- Calendar supports previous/next month and `回到当前月`, bounded by first valid watch history month and current month.
- Calendar heat levels use fixed thresholds and include a legend.
- Profile Analysis remains a long-term profile and is not affected by statistics range or calendar month.
- Profile Analysis no longer shows the extra `口味线索` card.
- Invalid persona fallback now regenerates or supplies a `多元杂食者`-compatible description.
- Taste quadrant X/Y now comes from AI output and is only clamped by service validation.

Out of scope kept:
- No profile-driven recommendation.
- No recommendation-related database or prompt changes.
- No final UI redesign.
- No persona image assets.

### WI-7: Verification and Closure

Planned scope:
- Verify statistics, cache behavior, stale behavior, empty states, and profile refresh behavior.
- Update stage documents.
- Confirm no test data remains.

## Stage Acceptance Criteria

- Watch Insights remains independent from player, Library Batch Ops, and Recommendation Feedback stages.
- First version uses real local data.
- Statistics and profile cache do not use `ApplicationSetting`.
- AI profile generation is cached and controlled by explicit refresh rules in later stages.
- Image/profile-driven recommendation is not introduced until a separate stage.
