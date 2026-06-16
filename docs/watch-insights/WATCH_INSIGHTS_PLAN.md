# Watch Insights Plan

## Stage Goal

Watch Insights（观影洞察）是播放器主线、Library Batch Ops 和 Recommendation Feedback 收口后的新阶段，目标是让用户基于真实本地观影数据理解自己的观影习惯、偏好画像和统计趋势。

第一版目标：
- 做真实数据功能版。
- 功能完整优先，UI 先够用，不追最终视觉。
- 页面最终包含两个 Tab：`画像分析`、`观影统计`。
- 首次进入默认打开 `画像分析`；同一应用会话内切换页面后，保留上次选中的 Tab 和两个 Tab 各自的滚动位置。
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
- The initial Tab is `画像分析`; later page activation restores the last selected Tab and each Tab's saved scroll offset.
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
- `persona.type` is restricted to the final fixed 23-type persona set.
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
12. 惊悚氛围控
13. 黑色幽默爱好者
14. 浪漫幻想派
15. 暗黑猎奇者
16. 史诗世界观派
17. 轻松娱乐派
18. 人性剖析者
19. 怀旧年代派
20. 小众寻宝者
21. 爆笑解压派
22. 动画叙事派
23. 纪录求真者

Persona boundary rules:
- The first 10 persona names remain unchanged.
- The final set removes `多元杂食者`; broad active exploration now falls back to `类型探索家`.
- The later types separate close meanings: horror / pressure maps to `惊悚氛围控`, weird cult darkness maps to `暗黑猎奇者`, comedy pressure release maps to `爆笑解压派`, broad low-pressure entertainment maps to `轻松娱乐派`, animation craft and story maps to `动画叙事派`, and factual / documentary viewing maps to `纪录求真者`.
- If AI returns a persona outside the fixed set, the service falls back to `类型探索家` and records a warning.
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
- Invalid persona fallback now regenerates or supplies a `类型探索家`-compatible description.
- Taste quadrant X/Y now comes from AI output and is only clamped by service validation.

Out of scope kept:
- No profile-driven recommendation.
- No recommendation-related database or prompt changes.
- No final UI redesign.
- No persona image assets.

### WI-6.2: Taste Combination Map Three-Column Graph

Status:
- Completed in this pass.

Scope completed:
- Replaced the old free-node/canvas taste combination map with a structured three-column relationship graph.
- Columns are `Type`, `Emotion`, and `Scene`.
- Relationships are displayed as direct lines between the three columns: `Type -> Emotion -> Scene`.
- Line thickness represents combination occurrence count.
- The visible module contains only the three-column line map and the Top10 combination list.
- The Top10 list remains visible and shows each combination as `Type -> Emotion -> Scene` with an occurrence count.
- Data still follows the WI-6.1 statistics range: current month or all time.
- Weighting uses combination occurrence count, not movie count, watched duration, or playback count.
- Node limits are Top6 per column. Link limits are Top12 for type-emotion and Top12 for emotion-scene. Top combinations remain Top10.
- Empty state remains component-level when the selected range has no usable type/emotion/scene combination.

Out of scope kept:
- No recommendation-system connection.
- No statistics口径 change beyond UI projection.
- No database field or migration.
- No final visual design pass.
- No animation or force-directed graph.

### WI-7: Verification and Closure

Status:
- Completed in this pass.

Scope completed:
- Verified DI, navigation, profile Tab, statistics Tab, range switching, calendar month switching, state history, caches, automatic-watched linkage, profile refresh semantics, empty/error states, and the WI-6.2 taste combination graph.
- Confirmed no new database field or migration is needed for WI-7.
- Confirmed statistics and profile refresh remain independent.
- Confirmed Watch Insights remains disconnected from profile-driven recommendation.
- Applied two closure-level fixes:
  - Statistics deduplicates watched movies by TMDB id, not just `MovieId`.
  - Profile service no longer overwrites rhythm/exploration DNA descriptions with local fixed text.
- Cleaned Known Issues so current limitations are separated from fixed historical issues.

Functional mainline result:
- Watch Insights first-version real-data mainline is complete.
- Remaining work is explicitly deferred to later stages:
  - WI-8 persona poster/image assets.
  - WI-R profile-driven recommendation.
  - UI-5 watch-history page/date navigation.
  - Final UI visual polish.

### WI-8: Persona Poster and Common Frame Integration

Status:
- Completed in this pass.

Scope completed:
- Added persona poster resource structure under `Assets/WatchPersonas/{key}/`.
- Initially generated 20 persona key folders from the provided placeholder inputs `1男.png` and `1女.png`; WI-8.1 extends the final set to 23.
- Each persona folder contains `{key}_male.png` and `{key}_female.png`.
- Added the shared frame resource at `Assets/WatchPersonas/Frames/persona_card_frame_default.png`.
- Included `Assets/WatchPersonas/**/*` as WPF `Resource` items so images are packaged with the app.
- Added `Persona.Type -> key` mapping in `WatchInsightsViewModel`.
- Profile persona card now displays a 3:4 poster area with the persona body image and the shared transparent frame overlaid.
- Default display gender is `female`; real user preference remains deferred.
- Poster fallback is resource-based and does not use absolute paths:
  1. `{key}_{gender}`
  2. `{key}_female`
  3. `{key}_male`
  4. `default_{gender}`
  5. `default`
  6. UI text placeholder
- Frame fallback is: shared frame if present, otherwise body image only.

Out of scope kept:
- No profile-driven recommendation.
- No AI prompt/profile cache/statistics logic change.
- No user gender setting UI.
- No database field or migration.
- No final visual design pass.

### WI-8.1: Persona Taxonomy 23-Type Update

Status:
- Completed in this pass.

Scope completed:
- Updated fixed persona taxonomy from 20 to 23 types.
- Removed `多元杂食者` / `eclectic_omnivore` from the legal persona set.
- Replaced slot 12 with `惊悚氛围控` / `thriller_atmosphere_fan`.
- Added slot 21 `爆笑解压派` / `comedy_relief_fan`.
- Added slot 22 `动画叙事派` / `animation_narrative_fan`.
- Added slot 23 `纪录求真者` / `documentary_truth_seeker`.
- Updated `WatchProfileService.PersonaTypes`, persona definitions, selection rules, invalid-type fallback, and profile prompt version `wi-profile-persona-23-v6`.
- Invalid AI persona type now falls back to `类型探索家`; fallback title / description are type-explorer compatible.
- Added poster resource folders for the four new formal keys using the current `1男.png` / `1女.png` placeholders.
- Confirmed `eclectic_omnivore` is the redundant legacy folder; the current official poster set is 23 persona folders / 46 gendered poster PNGs, excluding root placeholders and `Frames`.
- Added legacy UI fallback from `童心奇想家` to `animation_narrative_fan`; `童心奇想家` is not a legal persona type.

Out of scope kept:
- No profile regeneration or AI call.
- No database field or migration.
- No recommendation logic change.
- No final visual design pass.

### WI-9 / WI-R: Profile-Driven Recommendation

Status:
- Completed in this pass.

Scope completed:
- Existing AI recommendation now reads the cached Watch Profile as a long-term soft preference context.
- Recommendation reads profile data through `IWatchProfileService.GetRecommendationContextAsync()`, which is cache-only and never triggers profile AI generation.
- No profile / insufficient profile / parse failure / service error falls back to the existing recommendation logic with a stable `profile:none` fingerprint part.
- Custom recommendation preference remains higher priority than profile context; the profile section is explicitly framed as background only.
- Not-interested remains local hard filtering and is not weakened by profile context.
- Recommendation prompt now includes a compact profile summary: persona, taste summary, DNA, quadrant, and watch-vs-like signals.
- Recommendation reasons may lightly mention profile fit, but must not expose internal scores, DNA fields, or system field names.
- Recommendation reason guidance now asks for longer 70-130 character reasons, less templated wording, and avoids repeatedly opening with watched / want-to-watch phrasing.
- The recommendation prompt JSON example uses the same 70-130 character reason range to avoid shortening conflicts.
- Recommendation fingerprint includes a stable profile fingerprint part based on cached profile payload hash, source fingerprint, schema/prompt version, generated time, and cache refresh time.
- Recommendation fingerprint also includes a recommendation prompt version so reason-writing prompt changes invalidate old exact cache / candidate-pool combinations.
- Reason cache reuse is retained within the same recommendation reason prompt version; the cache document version was bumped once to clear old short / templated reasons after the prompt update.
- Profile changes naturally make exact recommendation cache and candidate-pool combinations stale through the existing fingerprint mismatch protection.

Out of scope kept:
- No recommendation UI change.
- No home page or discovery page change.
- No profile AI generation inside recommendation requests.
- No profile switch or settings entry.
- No database field or migration.

### WI-8.2: Persona Poster Asset Replacement And Color Frames

Status:
- Completed in this pass.

Scope completed:
- Replaced the placeholder persona posters with real assets from `C:\Users\32184\Desktop\人格海报`.
- Preserved the formal resource naming convention:
  - `Assets/WatchPersonas/{key}/{key}_male.png`
  - `Assets/WatchPersonas/{key}/{key}_female.png`
- Added four color frame resources under `Assets/WatchPersonas/Frames/`.
- Persona card frame selection now follows the user-provided matching rule instead of always using the single default frame.
- Existing default frame remains as fallback.
- Watch Insights loading backdrop now uses the permanent persona palette resource through a cache-only preflight before page activation, so it no longer depends on runtime poster decoding for the loading color.
- Watch Insights profile and statistics visuals are now split into four and five deferred module batches, allowing the loading shell to render before heavy cards, charts, bubbles, and triptych visuals are materialized.

Frame matching:
- Blue: 1, 2, 3, 7, 8, 9, 12, 13, 15, 22.
- Gold: 4, 6, 16, 17, 19, 20, 23.
- Pink: 5, 14, 18, 21.
- Green: 10, 11.

Out of scope kept:
- No profile AI prompt/cache change.
- No recommendation logic change.
- No database field or migration.
- No runtime image cache.

### WI-8.3: Persona Lead And Painted Label Refinement

Status:
- Completed in this pass.

Scope completed:
- Increased the AI persona lead limit from 33 to 40 characters in both the main prompt and the type-explorer repair prompt.
- Kept local persona lead normalization aligned to the same 40-character ceiling and bumped the profile prompt version so stale profile cache is not treated as current.
- Enlarged the lead corner brackets and moved the opening bracket upward and the closing bracket downward.
- Continued positioning both brackets from the actual first and last visible characters, with separate single-line and two-line hosts for collapsed and expanded navigation layouts.
- Added a dynamic painted background behind the persona label text.
- The painted background selects the highest-chroma color from the current fixed persona poster palette, then creates a restrained pale light-theme variant or a deeper dark-theme variant.
- The background follows the measured text width with 26-pixel side padding and uses five overlapping, round-ended, irregular strokes with a subtle right-up brush direction.

Out of scope kept:
- No persona taxonomy, recommendation logic, database field, migration, database update, commit, or push.
- No bitmap brush asset or runtime poster decoding was added.

### WI-8.4: Quadrant, Calendar, And Preference Wave Refinement

Status:
- Completed in this pass.

Scope completed:
- Expanded the taste-quadrant explanation area toward the center while keeping its left edge fixed.
- Moved the full quadrant coordinate view upward and increased the quadrant AI explanation target from 110-170 to 150-220 Chinese characters across 3-4 sentences.
- Bumped the Watch Profile prompt version so refreshed profiles use the longer quadrant explanation contract.
- Shifted the calendar navigation row right, then shifted its legend an additional amount to the right.
- Shifted the three calendar metric cards slightly right and upward without moving the calendar grid again.
- Added an alternating full-height preference-graph wave that starts from the left and right in turn approximately every eight seconds.
- The wave crosses the full graph in 4.2 seconds, gradually fades, and applies a stronger directional force than the pointer ripple while remaining inside the existing physics loop.
- Rounded the pointer ripple by reducing its final horizontal-to-vertical expansion ratio.

Out of scope kept:
- No statistics data semantics, preference tag generation, database field, migration, database update, commit, or push.
- No distant or repeating concentric ripple field was added.

### WI-8.5: Flat Watch-vs-Like Focus Cards

Status:
- Completed in this pass.

Scope completed:
- Replaced the watch-versus-like pseudo-perspective presentation with three ordinary flat WPF cards.
- Removed card skew transforms, folded shade overlays, and all hover-time angle changes.
- Kept a fixed 24-pixel overlap between adjacent cards to express stacking without perspective.
- Made the middle `经常喜爱` card the default primary card with ZIndex 3, 1.04 scale, full opacity, stronger shadow, and a 10-pixel upward offset.
- Made the default side cards use 0.94 scale, 0.68 opacity, weaker shadows, and an 8-pixel downward offset.
- Added explicit left-primary and right-primary stacking orders, with the middle card remaining between the primary and far card.
- Animated scale, opacity, shadow opacity, and translation over 220 milliseconds with restrained cubic easing.
- Restored the middle card as primary when the pointer leaves the complete three-card area.

Out of scope kept:
- No watch/like/want ranking data, conclusion prompt, database field, migration, database update, commit, or push.
- No rotation, perspective camera, trapezoid, skew, or simulated perspective effect remains in this module.

### WI-8.6: Taste Combination Graph And Top5 Refinement

Status:
- Completed in this pass.

Scope completed:
- Removed the lower-left taste graph legend.
- Normalized every visible graph link against the current visible minimum and maximum occurrence counts.
- Set inactive link opacity to `0.18 + 0.45 * normalizedCount` and thickness to `2 * (1 + 1.5 * sqrt(normalizedCount))`, clamped to 2-5 pixels.
- Set links connected to the hovered or selected node to 0.9 opacity.
- Added click-to-select and click-again-to-clear behavior, with hover temporarily taking focus over the selected node.
- Replaced graph node cards with shadow-free 74-pixel circular labels containing only the tag text and moved the count into a tooltip.
- Increased vertical spacing between graph labels from 10 to 20 pixels while preserving each column center.
- Added theme-aware mist-blue type, rose-pink emotion, and mint-green scene label resources with slightly darker borders.
- Changed the right ranking to Top5, inserted explicit `x` separators between rounded-rectangle labels, and shortened the occurrence text to `x次`.

Out of scope kept:
- No taste-combination calculation, statistics persistence, database field, migration, database update, commit, or push.

### WI-8.7: Quadrant Calendar And Local Preference Waves

Status:
- Completed in this pass.

Scope completed:
- Raised the taste-quadrant axis title/body group, with an extra upward offset while the sidebar is expanded.
- Moved the taste-quadrant coordinate canvas further upward and aligned the `A 标签 x B 标签` line to the body text readable width.
- Shortened the quadrant prompt target to 130-190 Chinese characters across 2-3 sentences.
- Bumped the Watch Profile prompt version to `wi-profile-persona-23-parallel-v17-quadrant-brief`.
- Centered the viewing-calendar previous/month/next navigation over the calendar grid and widened the spacing between the three controls.
- Moved the viewing-calendar heat legend below the grid and centered it on the calendar width, with the return-to-current-month action retained on that row.
- Raised the three calendar metric cards as a group.
- Replaced the full-height alternating preference-bubble sweep with randomized local wave emitters that expand from different zones and curve more strongly near edges/corners.

Out of scope kept:
- No statistics calculation, watch-history semantics, recommendation behavior, database field, migration, database update, commit, or push.

### WI-8.8: Watch-vs-Like Triptych Size And Shadow Safe Area

Status:
- Completed in this pass.

Scope completed:
- Enlarged the three flat watch-vs-like cards from 320x252 to 338x264 and expanded the triptych stage from 280/960 to 326/1000.
- Reduced the default adjacent overlap from 24px to 18px and pushed both side cards slightly outward so the primary middle card covers the right card less in the default state.
- Kept the primary middle card as the default focus, but lowered the default lift from 10px to 6px and lowered side/intermediate/far card animation targets so the larger cards grow downward.
- Enlarged only the `经常想看` star badge viewbox from 30px to 33px while keeping the badge host center unchanged.
- Reused the previous shadow-safe layout approach for this local triptych by giving the stage real vertical drawing room, disabling stage/Viewbox clipping, and keeping the cached-shadow card path unchanged.
- Reduced the module minimum height from 548px to 532px so the conclusion block sits slightly higher after the larger card stage.

Out of scope kept:
- No watch/like/want ranking data, profile conclusion text, AI prompt, statistics calculation, database field, migration, database update, commit, or push.

### WI-8.9: Taste Combination Graph Layout And Bezier Links

Status:
- Completed in this pass.

Scope completed:
- Increased the taste-combination graph canvas height and vertical node spacing so the left-side tag rows breathe more.
- Centered the `类型` / `情绪` / `场景` column labels on their node columns and removed the subtitle period.
- Switched graph labels and Top5 chips to the same type / emotion / scene color resources used by the preference-bubble graph.
- Replaced straight `Line` links with cubic Bezier `Path` links that start at source node right edges and end at target node left edges, with restrained horizontal control points.
- Added theme-aware gradient link colors from source-kind border color to target-kind border color.
- Lowered the maximum inactive link opacity and added a lightweight selected-link glow layer without reintroducing live drop-shadow effects.
- Expanded selected-node highlighting so a selected tag highlights both pair links for any Top10 `A x B x C` combination containing that tag.
- Added richer node tooltips: first line remains `标签：次数`, followed by up to three highest-count combinations in `组合：次数` format, with a faster graph-local tooltip delay.
- Reworked the right Top5 panel so it sizes to content, is vertically centered against the graph, has larger row gaps, shifts combination/progress content right, and vertically centers `x次` on the progress bar row.

Out of scope kept:
- No taste-combination calculation, statistics query, profile AI, recommendation behavior, database field, migration, database update, commit, or push.

### WI-8.10: Chrome Text Outline And Overview Delta Arrows

Status:
- Completed in this pass.

Scope completed:
- Split Watch Insights tab-highlight text outline into a dedicated `WatchInsightsTabChromeTextEffect`.
- Light theme tab-highlight outline now uses black; dark theme tab-highlight outline now uses white.
- Increased the shared Watch Insights chrome text outline blur used by the collapsed status subtitle and the tab-right status text, preserving light-theme white outline and dark-theme black outline.
- Changed zero month-over-month overview delta text to `较上月无变化`.
- Added up/down arrow text immediately to the right of the current metric value when the month-over-month delta is non-zero.
- Applied the arrow behavior to the four overview status cards, total watch time, and watch-day count.

Out of scope kept:
- No statistics calculation, watch-history semantics, profile AI, recommendation behavior, database field, migration, database update, commit, or push.

### WI-8.11: Persona Lead Brackets And Acrylic Paint Label

Status:
- Completed in this pass.

Scope completed:
- Kept persona lead brackets as inline `Run` text before and after the lead copy, so the lead reads as one complete sentence.
- Changed the lead brackets to inherit the lead text font, size, and weight while using the same muted quote color/opacity as the taste-summary quotation marks.
- Increased the persona label render safe area so the paint layer has room above and below the text.
- Removed the old persona-label projected text shadow path; this pass changes only the paint backdrop, not the label text rendering.
- Replaced separated highlight-style strokes with an opaque acrylic/impasto-like paint body and four connected back-and-forth brush passes.
- Removed the old edge-stroke path that could read as a thin line inside the painted label.
- Added clipped thick/dry-brush texture marks at a restrained right-up to left-down 30-degree angle.

Out of scope kept:
- No persona text, AI prompt, profile data, statistics data, recommendation behavior, database field, migration, database update, commit, or push.

### WI-8.12: Watch DNA Icon And Overflow Height Polish

Status:
- Completed in this pass.

Scope completed:
- Replaced the text placeholder icons in the Watch DNA cards with pure vector icons: movie ticket, heart, sofa, route branching, waveform, and compass.
- Kept the icon slot size and position unchanged by rendering each icon inside the existing 48x48 icon area.
- Removed the icon slot background so the icons read as standalone no-background line icons.
- Matched type, emotion, scene, and narrative icon colors to their corresponding chip colors; added dedicated cyan-blue and purple icon brushes for rhythm and exploration.
- Added a page-level Watch DNA overflow state that turns on when any DNA description ScrollViewer needs internal scrolling.
- When any DNA text needs scrolling, all six DNA cards become slightly taller.
- In non-progress DNA cards, the chip row and description move down together; in progress DNA cards, only the description moves down while the progress bar and labels stay fixed.

Out of scope kept:
- No DNA text generation, AI prompt, profile data, statistics data, recommendation behavior, database field, migration, database update, commit, or push.

### WI-8.13: Quadrant Rhythm Calendar Waves And Watch-Like Polish

Status:
- Completed in this pass.

Scope completed:
- Split the taste-quadrant `A x B` title into separate X-axis label, separator, and Y-axis label so each axis label can use its corresponding quadrant color.
- Moved the taste-quadrant coordinate canvas slightly left.
- Raised the taste-quadrant title/body block a little more when the sidebar is expanded.
- Updated the viewing-time spline chart to use height-based colors: blue at the x-axis, purple at low values, pink-orange at middle values, and red at high values.
- Applied the same color ramp to the viewing-time chart area fill while preserving the previous top-to-bottom opacity falloff.
- Added a dedicated deeper blue calendar heat color for `<30min` days and its legend swatch.
- Centered the previous/current/next calendar navigation group against the calendar grid.
- Changed preference-bubble wave emitters to start from random positions on the content-area edge only, with longer duration, farther reach, and slower visual fade.
- Raised the Watch-vs-Like triptych stage and conclusion panel slightly, and moved the wanted star badge up by 0.5px.

Out of scope kept:
- No profile/statistics calculation, AI prompt, recommendation behavior, database field, migration, database update, commit, or push.

### WI-8.14: Tab Gray Outline And Delta Arrow Placement Correction

Status:
- Completed in this pass.

Scope completed:
- Changed the selected/hovered Watch Insights tab text outline to light gray in the light theme and dark gray in the dark theme.
- Kept the separate status-subtitle and tab-right status text outline resource unchanged.
- Moved non-zero overview delta arrows from the current metric value row to the right side of the `compared with last month` delta text row.
- Applied the arrow placement correction to the four overview status cards, total watch time, and watch-day count.

Out of scope kept:
- No statistics calculation, month-over-month delta semantics, AI prompt, recommendation behavior, database field, migration, database update, commit, or push.

### WI-8.15: Taste Combination Top5 And Triple-Only Graph Polish

Status:
- Completed in this pass.

Scope completed:
- Shifted the taste-combination Top5 card left and slightly downward.
- Added a taste-combination-only rank badge style with a theme-specific border darker than the badge fill.
- Removed the Top5 progress bars so each row is rank, combination, and occurrence count aligned on one vertical center line.
- Moved the occurrence count column left by reducing the right column width and left-aligning the count text.
- Changed the selected-link glow from a hard wide path to a blurred, lower-opacity glow layer.
- Rebuilt graph node selection from complete type x emotion x scene combinations only, with at most four visible labels per column.
- Changed graph link generation to use only visible complete three-category combinations, not the old aggregate pair-edge list.
- Kept the four-slot column height stable while distributing fewer than four labels evenly across the same vertical range.

Out of scope kept:
- No taste-combination source statistics, AI prompt, recommendation behavior, database field, migration, database update, commit, or push.

## Stage Acceptance Criteria

- Watch Insights remains independent from player, Library Batch Ops, and Recommendation Feedback stages.
- First version uses real local data.
- Statistics and profile cache do not use `ApplicationSetting`.
- AI profile generation is cached and controlled by explicit refresh rules in later stages.
- Profile-driven recommendation is connected as a soft preference in WI-9 / WI-R; it remains disconnected from hard filters and does not trigger profile AI generation.
