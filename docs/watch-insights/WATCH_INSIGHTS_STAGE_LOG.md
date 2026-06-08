# Watch Insights Stage Log

## Phase 7.7f Statistics Upper-Area Visual Closeout

Goal: close the Watch Statistics upper area against the Phase 7.7 UI redesign scope without changing statistics service semantics, profile generation, recommendation input, Movie-only boundaries, database schema, migration, database update, commit, or push.

Completed:

- Rebuilt the statistics upper area into the `洞察总览` and `观影日历` modules.
- Kept the overview status cards limited to `已看`, `喜爱`, `想看`, and `不想看`; the sketch-only `未看` card remains intentionally excluded.
- Kept month / all range switching, statistics refresh, calendar month switching, return-to-current-month, and calendar date navigation on existing ViewModel commands.
- Added App-layer status-card display projections for subtitle and visual kind; statistics service output and cache structure are unchanged.
- Resourceized calendar heat-level colors through existing theme resource keys instead of page-local hex colors.
- Reused existing Watch History target-date navigation for calendar date clicks.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77f\"` passed with 0 warnings and 0 errors after clearing prior temporary Codex build output directories from `%TEMP%`.

Not done:

- No statistics lower-area visual rebuild: preference graph, rankings, viewing rhythm, and taste combination map remain for 7.7g.
- No statistics service口径 change, profile prompt/model/cache change, recommendation change, TV Watch Insights, mixed Movie + TV profile, database schema change, migration, database update, commit, or push.

## Phase 4.18 Phase 4 Closeout Boundary Note

Goal: record the Phase 4 full-regression result for Movie Watch Insights after TV support stabilization.

Result:

- Movie Watch Insights remains Movie-only and continues to use Movie-side statistics, Movie watch history, Movie user collection/state/rating rows, and cached Movie Watch Profile data.
- `WatchHistory.EpisodeId`, `TvSeries`, `TvSeason`, `TvEpisode`, `UserTvSeasonCollectionItem`, and TV Season state history remain excluded from Movie Watch Statistics, Watch Profile input, source fingerprinting, persona, DNA, quadrant, watch-vs-like, and recommendation profile context.
- Movie playback history can still enter Movie Watch Insights through `WatchHistory.MovieId`; TV Episode playback history is available to Watch History UI but does not enter Movie Watch Insights.
- Metadata-only TV rows created by Discovery browsing remain TV metadata rows and do not create Movie watch-history or Movie profile inputs.
- Future TV Watch Insights must use a TV-only input model, TV-only fingerprint/cache namespace, and an independent acceptance matrix. The Movie profile is not a mixed Movie + TV profile.

Not done:

- No Watch Insights statistic scope change, TV Watch Insights, mixed profile, profile cache schema change, database change, migration, scan change, player change, recommendation change, TV Discovery change, database update, commit, or push.

## Phase 4.17 TV Exclusion Regression Note

Goal: close the TV Support Phase 4.17 regression review by confirming TV data still does not enter Movie Watch Insights, Watch Profile, persona, DNA, quadrant, watch-vs-like, or recommendation profile context.

Result:

- Movie Watch Insights remains bounded by Movie-side inputs: `Movie`, `MediaFile.MovieId`, `WatchHistory.MovieId`, `UserMovieCollectionItem`, Movie rating sources, and Movie state history.
- Watch Profile input and source fingerprinting still exclude unidentified / failed Movie rows and do not read `TvSeries`, `TvSeason`, `TvEpisode`, TV Season user states, or `WatchHistory.EpisodeId`.
- TV Episode playback history remains available to Watch History UI through `EpisodeId`, but is excluded from Movie Watch Statistics and Movie Watch Profile inputs.
- Recommendation profile context reads cached Movie Watch Profile data only and does not trigger profile AI generation.
- Future TV Watch Insights requires a TV-only input model, TV-only fingerprint/cache namespace, and independent acceptance matrix. The Movie profile should not be reused as a mixed Movie + TV profile.

Not done:

- No Watch Insights statistic口径 change.
- No profile prompt, profile cache schema, persona, DNA, quadrant, watch-vs-like, recommendation, database, migration, player, library, scan, or TV Discovery change.

## WI-0 Completed

Goal: complete a read-only audit and data-capability assessment before implementation.

Completed:
- Audited current data models for movies, media files, watch history, collection state, tags, rating sources, country, language, runtime, and identification state.
- Audited navigation and page-registration patterns.
- Audited playback history fields and current completion-signal limitations.
- Audited statistics and AI-profile feasibility.
- Confirmed that dedicated Watch Insights cache storage does not exist yet.

Implementation:
- None. WI-0 was read-only.

## WI-1 Started

Goal: build Watch Insights stage infrastructure without starting page, statistics, profile, AI, or watched-algorithm implementation.

Planned work:
- Create `docs/watch-insights`.
- Add Watch Insights plan, stage log, and known issues documents.
- Add `WatchInsightCacheEntry` EF entity and configuration.
- Add `WatchInsightCacheEntries` DbSet.
- Add cache snapshot read model.
- Add `IWatchInsightCacheService` and implementation.
- Register DI.
- Add and apply migration `AddWatchInsightCacheEntries`.
- Run `dotnet build MediaLibrary.sln`.

Out of scope:
- No Watch Insights page.
- No navigation entry.
- No statistics query engine.
- No statistics Tab.
- No profile analysis Tab.
- No AI call or AI profile generation.
- No automatic watched algorithm implementation.
- No player, recommendation, Library Batch Ops, or scan main-flow changes.
- No test data.

## WI-1 Completed

Completed:
- Created the Watch Insights documentation set.
- Added `WatchInsightCacheEntry` with JSON payload, source fingerprint, stale state, refresh timestamps, and error placeholder fields.
- Added EF configuration and indexes for cache lookup and stale/expiry queries.
- Added `WatchInsightCacheEntries` to `AppDbContext`.
- Added `WatchInsightCacheSnapshot`.
- Added `IWatchInsightCacheService`.
- Added `WatchInsightCacheService` with async get, upsert, mark-stale, and clear operations.
- Registered `IWatchInsightCacheService -> WatchInsightCacheService` in DI.
- Generated migration `20260509173300_AddWatchInsightCacheEntries`.
- Applied the migration to the local database.

Boundaries kept:
- No Watch Insights page or navigation entry.
- No statistics query engine.
- No statistics UI.
- No profile analysis UI.
- No AI calls.
- No automatic watched algorithm implementation.
- No `UserMovieCollectionItem` AI tag snapshot fields.
- No automatic watched setting field.
- No player, recommendation, Library Batch Ops, or scan main-flow changes.
- No test data.

Validation:
- `dotnet ef migrations add AddWatchInsightCacheEntries --project src/MediaLibrary.Core/MediaLibrary.Core.csproj --startup-project src/MediaLibrary.Core/MediaLibrary.Core.csproj`
- Result: migration generated successfully. Using the App project as startup was not used because it does not reference `Microsoft.EntityFrameworkCore.Design`.
- `dotnet ef database update --project src/MediaLibrary.Core/MediaLibrary.Core.csproj --startup-project src/MediaLibrary.Core/MediaLibrary.Core.csproj`
- Result: migration applied successfully.
- Temporary cache-service validation passed for DI resolution, upsert, get, mark stale, and clear. The temporary `wi-1-validation` cache row was cleared.
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-2 Started

Goal: build the Watch Statistics query engine and cache behavior without adding UI, navigation, AI calls, automatic watched writes, database fields, or migrations.

Planned work:
- Add `WatchStatisticsSnapshot` and supporting read models.
- Add `IWatchStatisticsService`.
- Add `WatchStatisticsService`.
- Compute real-data Watch Statistics modules from existing models.
- Use `WatchInsightCacheEntries` with `kind=statistics` and `scopeKey=global`.
- Add 12-hour cache expiry.
- Add source fingerprint invalidation.
- Register the statistics service in DI.
- Run `dotnet build MediaLibrary.sln`.

Out of scope:
- No Watch Insights page.
- No navigation entry.
- No profile analysis Tab.
- No AI call or AI profile generation.
- No automatic watched algorithm implementation.
- No `Movie.IsWatched` auto-write change.
- No database field or migration.
- No player, recommendation, Library Batch Ops, or scan main-flow changes.
- No persistent test data.

## WI-2 Completed

Completed:
- Added `WatchStatisticsSnapshot` and supporting read models for overview cards, monthly tags, calendar heatmap, monthly activity cards, distributions, rhythm, taste combinations, and watch-more-vs-like data.
- Added `IWatchStatisticsService`.
- Added `WatchStatisticsService`.
- Added statistics source fingerprinting across movies, media files, watch histories, collection items, and rating sources.
- Added cache reuse through `WatchInsightCacheEntries` using `kind=statistics` and `scopeKey=global`.
- Added cache miss handling for missing, stale, expired, fingerprint-changed, and deserialize-failed states.
- Added `forceRefresh=true` manual refresh behavior.
- Added 12-hour automatic cache validity.
- Registered `IWatchStatisticsService -> WatchStatisticsService` in DI.

Statistics scope implemented:
- Status counts: watched, unwatched, favorite, want-to-watch, and not-interested.
- Total watch seconds from valid `WatchHistory.DurationWatchedSeconds`.
- Monthly watch count from valid watch histories in the current natural month.
- Monthly frequent tags weighted by watch seconds.
- Current-month calendar heatmap with all month dates.
- Monthly watch days, continuous watch days, and most active date.
- Type, emotion, scene, year, country, language, and weighted rating distributions.
- Monthly type, emotion, and scene Top 3 tag rankings.
- Viewing time buckets, weekday/weekend stats, and duration buckets.
- Taste combination map data for type x emotion x scene.
- Watch-more-vs-like Top 3 data by type tags.

Boundaries kept:
- No Watch Insights page or navigation entry.
- No statistics UI.
- No profile analysis UI.
- No AI calls.
- No automatic watched algorithm implementation.
- No database field or migration.
- No player, recommendation, Library Batch Ops, or scan main-flow changes.

Validation:
- Temporary service validation resolved `IWatchStatisticsService` through `AppServiceProvider`.
- `forceRefresh=true` computed a fresh snapshot.
- A following `forceRefresh=false` call loaded from the `statistics/global` cache.
- Validation result included `calendarDays=31`, `monthlyWatchCount=12`, `totalWatchSeconds=7949`, and a 64-character fingerprint on the current local database.
- Temporary validation project was removed.
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-3 Started

Goal: add the Watch Insights page shell, navigation entry, two Tabs, and a real-data Watch Statistics Tab while keeping AI profile generation and automatic watched implementation out of scope.

Planned work:
- Add navigation entry `观影洞察`.
- Add `WatchInsightsPage`.
- Add `WatchInsightsViewModel`.
- Register the page DataTemplate and ViewModel DI.
- Default to the `画像分析` Tab.
- Show profile-analysis component empty states without AI calls.
- Bind the `观影统计` Tab to WI-2 `IWatchStatisticsService` data.
- Add manual statistics refresh, loading state, warning display, and component-level empty states.
- Run `dotnet build MediaLibrary.sln`.

Out of scope:
- No AI call or AI profile generation.
- No profile cache refresh.
- No profile-driven recommendation.
- No automatic watched algorithm implementation.
- No WatchHistory write-chain change.
- No database field or migration.
- No player, recommendation, Library Batch Ops, or scan main-flow changes.
- No watch history page or real calendar date navigation.

## WI-3 Completed

Completed:
- Added `NavigationPageKey.WatchInsights`.
- Added the left navigation item `观影洞察` near recommendations and favorites.
- Added `WatchInsightsViewModel`.
- Added `WatchInsightsPage`.
- Added `WatchInsightsPage.xaml.cs`.
- Added `WatchInsightsViewModel` to DI.
- Added `WatchInsightsViewModel -> WatchInsightsPage` DataTemplate in `App.xaml`.
- Set page title to `观影洞察` and subtitle to `让你更懂你`.
- Added `画像分析` and `观影统计` Tabs.
- Reset the default active Tab to `画像分析` on page activation.
- Loaded real statistics through `IWatchStatisticsService.GetStatisticsAsync(forceRefresh:false)` on activation.
- Added manual refresh through `GetStatisticsAsync(forceRefresh:true)`.
- Added loading button text and refresh-button disable behavior through `AsyncRelayCommand`.
- Added warning, error, empty-state, and component-level fallback UI.

Statistics Tab modules implemented:
- Insight overview cards: watched, unwatched, favorite, want-to-watch, not-interested.
- Total watch time, monthly watch count, and monthly frequent tags.
- Current-month calendar heatmap with all dates from WI-2 `CalendarDays`.
- Monthly watch days, continuous watch days, and most active date.
- Preference graph using emotion and scene tags.
- Monthly Top3 ranking for type, emotion, and scene tags.
- Viewing rhythm: time buckets, weekday/weekend stats, and duration distribution.
- Taste combination map: simplified node/edge canvas plus Top10 ranking.

Profile Tab state:
- `观影口味总结`, `观影 DNA`, `你的观影人格`, and `口味象限` show component-level deferred/empty states.
- `看得多 vs 真喜欢` uses WI-2 local Top3 statistics when available and does not call AI.

Boundaries kept:
- No AI calls.
- No AI profile generation.
- No profile cache refresh.
- No automatic watched algorithm implementation.
- No database field or migration.
- No WatchHistory write-chain change.
- No player, recommendation, Library Batch Ops, or scan main-flow changes.

Deferred:
- Calendar previous/next month buttons are present but disabled until month-scoped statistics are introduced.
- Calendar day click only shows a placeholder notice; the watch history page is not implemented.
- Final visual polish remains deferred.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-8.2 Persona Poster Asset Replacement And Color Frame Mapping

Status:
- Completed in this pass.

Scope completed:
- Imported the real persona poster assets from `C:\Users\32184\Desktop\人格海报`.
- Replaced the 23 formal persona key folders with real `male` / `female` poster images.
- Kept the original desktop assets and legacy repo resources such as `1男.png`, `1女.png`, and `eclectic_omnivore` untouched.
- Added four color frame resources:
  - `persona_card_frame_blue.png`
  - `persona_card_frame_gold.png`
  - `persona_card_frame_pink.png`
  - `persona_card_frame_green.png`
- Updated the persona card frame selection logic to use the user-provided number-to-color matching rule.
- Kept the existing `persona_card_frame_default.png` as a fallback when a color-specific frame is unavailable.
- No XAML layout change was needed; the existing overlay image layer now receives the persona-specific frame URI.

Color mapping:
- Blue: 1, 2, 3, 7, 8, 9, 12, 13, 15, 22.
- Gold: 4, 6, 16, 17, 19, 20, 23.
- Pink: 5, 14, 18, 21.
- Green: 10, 11.

Boundaries kept:
- No AI/profile generation logic change.
- No recommendation logic change.
- No database field or migration.
- No runtime image cache.

## WI-8.1 Completed

Goal: update the Watch Profile persona taxonomy from 20 to 23 formal persona types and keep profile validation, prompt rules, poster mapping, and docs aligned.

Completed:
- Replaced the old slot 12 `多元杂食者` with `惊悚氛围控`.
- Removed `多元杂食者` / `eclectic_omnivore` from the legal persona set and fallback target.
- Added `爆笑解压派`, `动画叙事派`, and `纪录求真者` as slots 21-23.
- Updated `WatchProfileService.PersonaTypes`, persona definitions, persona selection rules, invalid persona fallback, and profile prompt version to `wi-profile-persona-23-v6`.
- Invalid AI `persona.type` now falls back to `类型探索家`; fallback title / description also match `类型探索家`.
- Updated `WatchInsightsViewModel` persona poster mapping to the 23 formal keys.
- Added legacy UI compatibility so old cached `童心奇想家` can use the `animation_narrative_fan` poster fallback without becoming a legal persona type.
- Created four new poster resource folders from the current placeholder images:
  - `thriller_atmosphere_fan`
  - `comedy_relief_fan`
  - `animation_narrative_fan`
  - `documentary_truth_seeker`
- Kept legacy `eclectic_omnivore` resources in place and did not delete user assets.

Boundaries kept:
- No profile regeneration.
- No AI call.
- No recommendation logic change.
- No database field or migration.
- No final UI visual redesign.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-9 / WI-R Completed

Goal: connect the cached Watch Profile to the existing AI recommendation system as a soft long-term taste background.

Completed:
- Added a cache-only recommendation context method to `IWatchProfileService`.
- The recommendation context reads `WatchInsightCacheEntries` with `kind=profile`, `scopeKey=global`.
- Recommendation context does not call `GetProfileAsync()` and does not trigger profile AI generation.
- Recommendation prompt receives a compact profile section containing persona, summary keywords, DNA, quadrant, and watch-vs-like signals.
- Prompt wording keeps the priority order: local hard filters and scope rules first, custom recommendation preference second, profile context third as soft background.
- Recommendation reason guidance was adjusted to allow fuller 70-130 character reasons, reduce fixed watched / want-to-watch phrasing, and naturally mention profile fit when useful.
- The AI JSON output example was aligned to the same 70-130 character reason range so it no longer contradicts the main reason guidance.
- Recommendation fingerprint now includes a profile fingerprint part.
- Recommendation fingerprint also includes a recommendation prompt version, so prompt wording changes can stale old candidate pools and exact cache entries.
- Recommendation cache document version was bumped once for the reason-prompt update, clearing old cached recommendation reasons while preserving same-version reason reuse afterwards.
- When no usable profile cache exists, fingerprint uses `profile:none` and recommendations follow the previous logic.
- Profile changes invalidate exact recommendation cache and candidate-pool combinations through the existing fingerprint mismatch path.
- Added privacy-safe diagnostics for profile context load/skip, profile fingerprint, and prompt application.

Boundaries kept:
- No recommendation UI change.
- No home page or discovery page change.
- No recommendation-triggered profile AI call.
- No database field or migration.
- No Watch Insights statistics, profile generation prompt, persona posters, player, library, or scan changes.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-9 Polish: Profile AI Model Tiering And Parallel Card Prompts

Status:
- Completed in this pass.

Completed:
- Split Watch Profile generation from one large AI prompt into five card-level AI requests:
  - Taste summary.
  - Persona.
  - Watch DNA.
  - Taste quadrant.
  - Watch-more-vs-like conclusion.
- The five profile card requests run concurrently with a guarded max concurrency of 5.
- Watch-more-vs-like rankings remain local statistics; AI only generates the conclusion sentence.
- The cached payload remains the same `WatchProfileSnapshot` shape.
- Profile prompt version was bumped to `wi-profile-persona-23-parallel-v7` so old one-shot profile output is not reused as current profile output.
- Added AI request tiers:
  - Watch Profile uses DeepSeek `deepseek-v4-pro` with thinking enabled, `reasoning_effort=high`, and 180-second timeout when the configured endpoint is DeepSeek.
  - Recommendation uses DeepSeek `deepseek-v4-flash` and 90-second timeout when the configured endpoint is DeepSeek.
  - Classification and other existing calls continue using the global configured model; old DeepSeek compatibility names map to `deepseek-v4-flash`.
- Non-DeepSeek OpenAI-compatible endpoints continue using the user-configured model, avoiding hard-coded DeepSeek names on other providers.

Boundaries kept:
- No UI change.
- No recommendation UI change.
- No database field or migration.
- No profile cache table/schema change.
- No classification prompt semantic change.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-7: Verification and Closure

Status:
- Completed in this pass.

Scope completed:
- Verified Watch Insights navigation/DI wiring:
  - `WatchInsightsViewModel` is registered in DI.
  - `IWatchStatisticsService`, `IWatchProfileInputService`, and `IWatchProfileService` are registered.
  - `NavigationPageKey.WatchInsights` and the `WatchInsightsViewModel -> WatchInsightsPage` DataTemplate are present.
- Verified Statistics Tab behavior:
  - Default statistics range is `本月`.
  - Range selector supports `本月 / 全部`.
  - Range-dependent modules use the selected range: watch time, watched movie count, frequent tags, preference graph, tag ranking, rhythm, duration distribution, and taste combination map.
  - Calendar month is independent from statistics range.
- Verified Status Overview behavior:
  - Only `已看`, `喜爱`, `想看`, and `不想看` are displayed.
  - `全部` shows current status totals and hides comparison text.
  - `本月` uses `UserMovieStateChangeHistories` for current-month additions and `本月比上月`.
  - `UpdatedAt` is not used as state-change time.
- Verified Calendar behavior:
  - Calendar defaults to current month.
  - Previous/next month navigation is bounded by first valid watch-history month and current month.
  - `回到当前月` is shown only outside the current month.
  - Heat levels use fixed thresholds.
- Verified Profile Analysis behavior:
  - Profile remains a long-term/global profile.
  - Statistics range and calendar month do not affect profile cache or AI refresh.
  - Manual profile refresh skips AI when source fingerprint and prompt/schema version are unchanged.
  - Data-insufficient and AI-failure states keep component-level fallback behavior.
- Verified cache behavior:
  - Statistics cache scope is range/calendar aware.
  - Profile cache remains `kind=profile`, `scopeKey=global`.
  - Statistics refresh and profile refresh stay separated.
- Verified WI-6.2 taste combination map:
  - The old free-node map is no longer used.
  - UI uses a three-column `类型 / 情绪 / 场景` graph.
  - Lines connect `类型 -> 情绪` and `情绪 -> 场景`.
  - Line thickness reflects combination occurrence count.
  - Top10 remains the only companion list.
- Minimal closure fixes:
  - Distinct watched movie enumeration now deduplicates by TMDB id, preventing duplicate Movie rows for the same TMDB from double-counting watched movies, tags, and taste combinations.
  - Rhythm/exploration DNA descriptions are no longer overwritten by local fixed text. AI descriptions are preserved; missing progress-gene descriptions are recorded as warnings and left empty instead of being fabricated.
  - The UI projection no longer inserts a generic DNA description when the profile service intentionally leaves it empty.
- Cleaned `WATCH_INSIGHTS_KNOWN_ISSUES.md` so fixed items are no longer mixed into current Known Issues.

Out of scope kept:
- No new DB field or migration.
- No recommendation-system or profile-driven recommendation connection.
- No persona poster/image resources.
- No final UI redesign.
- No player, resource library, Library Batch Ops, scan, or settings changes.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Closure recommendation:
- Watch Insights functional mainline is complete.
- Suggested next stages:
  - `WI-8`: persona poster/image asset integration.
  - `WI-R`: profile-driven recommendation integration.
  - `UI-5`: watch-history page and calendar-date navigation.

## WI-8: Persona Poster and Common Frame Integration

Status:
- Completed in this pass.

Input resource audit:
- Found male placeholder: `Assets/WatchPersonas/1男.png`.
- Found female placeholder: `Assets/WatchPersonas/1女.png`.
- Found shared transparent frame: `Assets/WatchPersonas/Frames/persona_card_frame_default.png`.
- No numeric `1-20` legacy folders were present.

Resource generation:
- Created 20 persona key folders:
  - `emotion_immersive`
  - `mystery_solver`
  - `genre_explorer`
  - `classic_collector`
  - `healing_companion`
  - `rating_curator`
  - `auteur_follower`
  - `sci_fantasy_traveler`
  - `realism_observer`
  - `action_player`
  - `arthouse_aesthete`
  - `eclectic_omnivore`
  - `dark_humorist`
  - `romantic_dreamer`
  - `dark_curiosity_seeker`
  - `epic_worldbuilder`
  - `easy_entertainment_fan`
  - `human_nature_analyst`
  - `nostalgia_time_traveler`
  - `niche_treasure_hunter`
- Copied 20 male placeholder posters and 20 female placeholder posters.
- Existing target posters would be skipped instead of overwritten; this run skipped 0 existing target posters.
- Original `1男.png` / `1女.png` were preserved.

Code changes:
- Added WPF `Resource` inclusion for `Assets/WatchPersonas/**/*` in `MediaLibrary.App.csproj`.
- Added stable `Persona.Type -> key` mapping in `WatchInsightsViewModel`.
- Added poster URI fallback resolution without absolute paths.
- Added shared-frame URI resolution with safe fallback when the frame is missing.
- Updated the Profile Analysis persona card to show:
  - poster body image,
  - shared transparent frame overlay,
  - persona type/title/description.
- Default poster gender is `female`.

Boundaries kept:
- No AI profile generation or prompt change.
- No profile-driven recommendation.
- No statistics service change.
- No database field or migration.
- No runtime image cache.
- No final visual polish.

Validation:
- Confirmed 20 persona folders, 40 generated persona images, and shared frame exist.
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6.2 Completed

Goal: make the Watch Statistics taste combination map read as a clear three-column relationship graph instead of a free-node map.

Audit result:
- `WatchStatisticsService` already used the WI-6.1口径: selected statistics range, valid watched histories, identified movies, distinct movies, type x emotion x scene combinations, and occurrence-count sorting.
- The previous UI still projected `TasteMapNodes` / `TasteMapEdges` into a Canvas-style free-node graph.
- The previous Top10 list existed, but the relationship between type, emotion, and scene was not explicit enough.

Completed:
- Added ViewModel projections for positioned graph nodes, direct graph lines, and Top10 combination rows.
- Replaced the old Canvas/free-node display with:
  - three columns: type / emotion / scene
  - direct lines between type -> emotion and emotion -> scene
  - Top10 rows showing type -> emotion -> scene and occurrence count
- Line thickness is based on combination occurrence count.
- The visible module contains only the three-column line map and the Top10 combination list; no extra relationship cards are shown.
- Limited nodes to Top6 per column, links to Top12 per side, and combinations to Top10.
- Added fallback projection from Top10 combinations so non-empty combination data does not render as an empty graph if edge filtering removes too much.
- Kept the selected statistics time range as the source of truth; no profile, AI, recommendation, calendar, or database behavior was changed.

Boundaries kept:
- No recommendation-system connection.
- No database field or migration.
- No final visual redesign.
- No animation or force-directed graph.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6.1 Status Identity Follow-up

Completed:
- Confirmed the auto-watched "not counted" report was caused by an unidentified movie: state-history writes intentionally skip rows without a stable TMDB identity.
- Corrected the identification-state semantic: reset / unidentified placeholder / re-identification paths do not create status-history activation rows.
- Removed the identification-time activation behavior. There is no `Identification` state-history source for watched, favorite, want-to-watch, or not-interested state.
- Successful identification uses the target `Movie`'s existing state. If the target movie was already marked before, that remains existing state and is not a current-month addition.
- Same-TMDB duplicate merges may preserve state because they represent the same movie identity. Different-TMDB reassignment does not transfer watched, favorite, user rating, baseline, or collection status.
- Manual match and Batch-2 apply/merge paths both use the same no-status-activation behavior.
- Collection rows linked to unidentified or identification-failed movies remain excluded from Watch Statistics unless they already match a stable identified target identity.
- Reset / unidentified placeholder / re-identification never turns an old placeholder state into a new monthly status addition.
- Moving a library record out and scanning the same file back in does not create a status-history row. The scan path reuses the existing `MediaFile` by path and only clears `IsDeleted`, so old user states keep their original state-history timing.
- Re-identifying an already identified, previously marked movie does not create a new status activation row.

Behavior:
- `全部` still uses the current state snapshot for identified movies.
- `本月` uses only real state-history rows written by user actions, batch operations, recommendation actions, collection actions, or automatic watched.
- Repeated identification does not create a new activation row.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6.1 Statistics Range and Profile Semantics Fix Completed

Goal: correct Watch Statistics time ranges and profile-generation semantics without adding DB fields, migrations, recommendation integration, or final UI redesign.

Audit result:
- Watch Statistics used one `statistics/global` cache payload and mixed all-time status data, all-time watch duration, current-month watch count, and current-month calendar data.
- Status overview still displayed `未看`, even though the current product overview should show only positive/explicit states.
- Monthly watch count was based on valid `WatchHistory` rows, so repeat plays of the same movie inflated the count.
- Frequent tags, monthly rankings, and taste combinations were weighted by watch duration or mixed score, while the corrected product口径 requires distinct watched movies and tag/combination occurrence counts.
- Calendar month switching was still disabled and heat levels were relative to the most active date in the shown month.
- Profile persona fallback previously used `多元杂食者` and could retain an incompatible AI description; WI-8.1 supersedes the fallback target with `类型探索家`.
- Profile quadrant X/Y was overwritten by local scores; WI-6.1 requires AI-provided X/Y with service-side range validation only.

Completed:
- Added `WatchStatisticsTimeRange` with `Month` and `All`.
- Added range-aware `IWatchStatisticsService.GetStatisticsAsync(...)` overload.
- Statistics cache scope is now separated by range and calendar month, for example `range:month:yyyyMM:calendar:yyyyMM` and `range:all:calendar:yyyyMM`.
- Statistics fingerprint now includes the statistics scope key so month/all/calendar payloads cannot pollute each other.
- Statistics Tab now has a `本月 / 全部` range selector, defaulting to `本月`.
- Status overview shows four cards only: `已看`, `喜爱`, `想看`, `不想看`.
- Status overview now follows the selected statistics range. The current month range uses each status source row's `UpdatedAt`; the all-time range counts all current states.
- The `本月 / 全部` range buttons now show a selected state.
- Playback-dependent modules follow the selected range: watch duration, watched movie count, frequent tags, preference graph, tag ranking, rhythm, duration distribution, and taste combination map.
- Watch count now means distinct TMDB movies with valid watch history in the selected range.
- Frequent tags, tag rankings, preference graph, and taste combinations are counted by distinct watched movies in the selected range, not by watch duration or repeat play count.
- Calendar month is independent from the statistics range and supports previous/next month plus `回到当前月`.
- Calendar bounds are constrained between the first valid watch-history month and the current month.
- Calendar heat levels now use fixed thresholds: none, <30 minutes, 30-60 minutes, 1-2 hours, and 2+ hours.
- Calendar monthly cards now follow the displayed calendar month and use longest continuous watch streak within that month.
- Profile input now asks Watch Statistics for the all-time range so profile remains a long-term profile.
- Invalid AI persona type now triggers a small second AI request to generate matching fallback title/description; WI-8.1 changes the fallback target to `类型探索家`.
- Profile quadrant X/Y now comes from AI output; service only clamps to -100..100 and rejects newly generated profile JSON with missing or invalid X/Y.
- Prompt version was bumped to `wi-profile-range-quadrant-v5`.
- Removed the Profile Analysis `口味线索` card; the profile page keeps summary, persona, DNA, quadrant, and watch-more-vs-like as the primary modules.

Boundaries kept:
- No database field or migration.
- No recommendation-system connection.
- No player, Library Batch Ops, delete, scan, or automatic-watched changes.
- No profile time range; Profile Analysis remains a global long-term profile.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6.1 Delete-Record State History Follow-up

Completed:
- Fixed a status-history ownership bug where deleting a movie record could leave its `UserMovieStateChangeHistories` rows behind, causing `本月状态新增` to keep counting a deleted movie.
- `DeleteMovieRecordAsync` now removes state-history rows owned by the deleted movie or by deleted collection items.
- `DeleteCollectionRecordAsync` now removes state-history rows owned by the deleted collection item.
- Watch Statistics now ignores orphaned state-history rows whose `MovieId` or `UserMovieCollectionItemId` no longer belongs to a current identified movie / collection item.
- Statistics fingerprint includes a logic version so existing cached statistics are invalidated after this ownership-filter change.

Behavior:
- `删除影片记录` removes the movie from resource library, collection state, Watch Insights statistics, and future profile input.
- Existing orphaned state-history rows from earlier builds no longer affect the current-month status cards.
- `移出资源库` is unchanged: it still preserves state/history and should not remove status-history rows.
- Profile input fingerprint no longer includes visibility/order-only timestamps from preserved collection state, so moving a marked movie out of the library does not by itself force profile regeneration.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6.1 Status Overview History Correction Completed

Goal: correct status overview wording and state-change timing so `本月` does not use `Movie.UpdatedAt` or `UserMovieCollectionItem.UpdatedAt` as a proxy for user status changes.

Problem:
- `UpdatedAt` can be changed by metadata, poster, rating, tag, scan, merge, or visibility updates.
- Using `UpdatedAt` made a movie look like it was newly marked watched/favorite/want/not-interested in the current month even when only non-status data changed.

Completed:
- Added `UserMovieStateChangeHistory` entity and EF configuration.
- Added `UserMovieStateChangeHistories` to `AppDbContext`.
- Generated and applied migration `20260510163406_AddUserMovieStateChangeHistories`.
- Added centralized state-change recording for actual boolean transitions only.
- Recorded `Watched`, `Favorite`, `WantToWatch`, and `NotInterested` changes with `OldValue`, `NewValue`, `ChangedAtUtc`, `TmdbId`, optional `MovieId`, optional collection item id, and source.
- Connected history writes to:
  - manual library watched/favorite changes through `MovieManagementService`;
  - external/collection watched, want-to-watch, and not-interested changes through `UserCollectionService`;
  - automatic watched changes through `WatchHistoryService` with `Source=AutoWatched`;
  - batch watched/unwatched operations with `Source=Batch`;
  - recommendation want-to-watch/not-interested operations with `Source=Recommendation`.
- Reworked Watch Statistics status overview:
  - `全部` uses current entity state snapshot, unified by TMDB id, and shows no comparison text.
  - `本月` uses `UserMovieStateChangeHistories` and counts distinct TMDB ids whose latest state change in the current natural month is `NewValue=true`.
  - If a movie is marked true and then canceled in the same month, it no longer counts as a current-month addition.
  - `本月` comparison text is `较上月 +N`, `较上月 -N`, `与上月持平`, or `暂无上月记录`.
- Updated statistics fingerprint to include state-history count and max `ChangedAtUtc`.
- Updated status overview UI title/subtitle:
  - `全部`: `当前状态总览`.
  - `本月`: `本月状态新增`.
- Kept `未看` removed from the status overview.

Boundaries kept:
- No recommendation-system changes.
- No player core changes beyond recording automatic-watched state history in the existing auto-watched write path.
- No resource-library delete semantic changes.
- No fake state-history backfill.

Compatibility:
- Existing true states before the new table are still counted in `全部`.
- Existing true states before the new table are not counted as `本月新增`.
- Historical state changes before this migration cannot be accurately reconstructed.

Validation:
- `dotnet ef migrations add AddUserMovieStateChangeHistories --project src/MediaLibrary.Core/MediaLibrary.Core.csproj --startup-project src/MediaLibrary.Core/MediaLibrary.Core.csproj`
- Result: migration generated successfully.
- `dotnet ef database update --project src/MediaLibrary.Core/MediaLibrary.Core.csproj --startup-project src/MediaLibrary.Core/MediaLibrary.Core.csproj`
- Result: migration applied successfully.
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6 Profile Semantic Fingerprint Fix

Problem:
- Removing a movie from the library could create or update a `UserMovieCollectionItem` only to preserve library/collection visibility.
- Profile input fingerprinting used raw row identifiers and `UpdatedAt` values, so this visibility-only operation could be treated as a profile input change.
- Profile prompt context also reused global Watch Statistics type/emotion/scene distributions, which included identified movies without profile signals.

Completed:
- Changed profile fingerprinting to use normalized semantic profile signals instead of raw `Movie` / `UserMovieCollectionItem` row timestamps.
- Profile fingerprint now ignores library visibility-only changes such as moving a movie out of the library when watched/favorite/want/not-interested/history signals are unchanged.
- Profile fingerprint still changes when real profile signals change: watched, favorite, want-to-watch, not-interested, user rating, valid watch history, ratings, tags, or deletion of a movie record that carried those signals.
- Profile type/emotion/scene distributions are now built from profile signal samples only, not all identified movies.
- Collection-row `UpdatedAt` only affects profile sample ordering when the collection item adds a new semantic state to the sample.

Boundaries kept:
- No database field or migration.
- No UI contract change.
- No recommendation-system connection.
- No Library Delete semantic change.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6 DNA/Text Consistency Polish Completed

Goal: make DNA presentation read like a product profile instead of a field dump, reduce repeated wording, and keep rhythm/exploration descriptions consistent with their scores.

Audit result:
- DNA tag genes already had a `Tags` projection and WPF `ItemsControl` with `Border`/`TextBlock` chips, but old caches without `tags[]` could still fall back to label parsing.
- DNA descriptions came from AI unless missing, so they could repeat the visible tags.
- Rhythm and exploration scores came from AI output and were only clamped; descriptions could contradict the score direction.
- Summary text, keywords, and persona description were AI output with no prompt-level anti-repetition rule.
- The profile cache did not have a prompt/schema version, so changing the prompt could not be distinguished from unchanged user input.

Completed:
- Added `ProfileSchemaVersion` and `PromptVersion` to `WatchProfileMeta` inside cached profile JSON only; no database field or migration was added.
- Current prompt version is `wi-profile-summary-depth-v3`.
- Cache reuse now requires:
  - Same source fingerprint.
  - Same profile schema version.
  - Same prompt version.
  - Parseable cache payload.
- `forceRefresh=false` with old prompt/schema returns old cache and warns that manual refresh can generate the newer profile.
- `forceRefresh=true` with old prompt/schema regenerates even if source fingerprint is unchanged.
- `forceRefresh=true` with same fingerprint and same prompt/schema skips AI and returns `画像数据没有变化，已显示最新画像。`.
- Tightened prompt rules:
  - Summary text is 2-4 natural sentences and may be more complete than other profile modules.
  - Summary keywords are capped at 6 and should cover different dimensions.
  - Persona description must explain the persona through behavior signals.
  - DNA descriptions must explain the preference behind tags, not repeat tags.
  - Rhythm/exploration descriptions must match score direction.
- Added service-side keyword deduplication for summary keywords.
- Added service-side DNA description normalization:
  - Tag genes get safe explanatory fallback when AI description is missing or just repeats tags.
  - Rhythm description is derived from score ranges: 0-35 slow-burn, 36-64 balanced, 65-100 tight.
  - Exploration description is derived from score ranges: 0-35 stable, 36-64 balanced, 65-100 fresh.
- Follow-up adjustment:
  - Taste summary text can now be longer: 2-4 natural sentences, with a larger service-side length cap than persona/DNA text.
  - Watch-more-vs-like ranking lists are now always overwritten from WI-2 local statistics; AI only supplies the optional conclusion text.
- Initial UI/copy polish:
  - Added a lightweight `观影画像概览` lead area with a primary profile judgment and quadrant summary.
  - Shortened explanatory copy so the top section reads less like implementation notes.
  - Renamed the visible section headings to `口味总结` and `观影人格`.
  - Tightened the profile prompt tone toward concrete product-profile copy.
- Follow-up UI polish:
  - Updated prompt version to `wi-profile-unified-copy-v4`.
  - Strengthened prompt rules so Summary, Persona, DNA, and WatchVsLike share one core profile story while each explains a different angle.
  - Changed Watch DNA from a vertical list into a 3x2 six-cell grid.
  - Changed the taste quadrant from a coordinate/debug view into a four-area `口味定位图`.
  - Removed visible X/Y score readouts from the quadrant card; the UI now shows only the current positioning and axis meanings.
- Added UI log events for DNA tag projection per gene.

Boundaries kept:
- No recommendation-system connection.
- No persona image resources.
- No database field or migration.
- No statistics Tab change.
- No player, library delete, scan, or automatic-watched changes.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6 Profile Input Filter Fix

Issue:
- Profile generation could appear to be affected by marks on unidentified movies.

Root cause:
- Profile samples already filtered identified movies, but linked `UserMovieCollectionItem` rows could still enter if they had a `TmdbId` while their linked `Movie` was currently unidentified or identification-failed.
- Profile fingerprint also reused the WI-2 statistics fingerprint. That statistics fingerprint intentionally covered all collection rows, so an unidentified collection/status change could change the profile fingerprint even when the prompt samples should not change.

Fix:
- `WatchProfileInputService.LoadIdentifiedCollectionItemsAsync` now includes a linked collection item only when:
  - it has a positive TMDB id and title; and
  - it is either external with no `MovieId`, or its linked `Movie` is identified (`Matched` or `ManualConfirmed`) with the same TMDB id.
- Profile source fingerprint no longer embeds `WatchStatisticsSnapshot.SourceFingerprint`.
- Profile fingerprint now hashes only profile-eligible movies, profile-eligible collection items, rating sources, and valid watch histories.

Boundaries kept:
- No database field or migration.
- No statistics Tab change.
- No recommendation-system connection.

Validation:
- `dotnet build src\MediaLibrary.App\MediaLibrary.App.csproj -p:OutDir=...\ .tmp\verify-build\app\`
- Result: 0 warnings, 0 errors.

## WI-6 Polish Completed

Goal: narrow the Profile Analysis Tab to the product-approved display structure and prevent repeated AI calls when profile input has not changed.

Audit result:
- The WI-6 UI was showing more fields than the product design requires: liked directions, disliked directions, future likely directions, future less-likely directions, and a standalone caveats/limits card.
- Persona and DNA confidence were displayed. The confidence values come from AI output and service-layer clamping/defaults, so they are not suitable as rigorous UI metrics.
- `GetProfileAsync(forceRefresh:true)` bypassed the normal cache-hit path and could call AI even when `SourceFingerprint` was unchanged, causing profile content drift for identical input.

Completed:
- Taste summary keywords are limited to the first 6 entries.
- Added profile DNA `tags[]` support while keeping old cache compatibility through `label` fallback.
- Updated the AI prompt schema so type/emotion/scene/narrative genes can output 3 tags plus a description.
- Added a fixed profile-level narrative tag set; narrative tags are not persisted to `Movie` and do not require a database field.
- Filtered narrative gene tags to the fixed narrative tag set at service normalization time and record a warning when AI returns out-of-set tags.
- Updated DNA UI:
  - Type, emotion, scene, and narrative genes display up to 3 tags plus one description.
  - Rhythm displays a progress bar from `慢热` to `紧凑`.
  - Exploration displays a progress bar from `稳定` to `新鲜`.
  - DNA confidence and score text are no longer displayed for tag genes.
- Removed persona confidence from the UI.
- Removed the five standalone UI cards for liked directions, disliked directions, future likely directions, future less-likely directions, and caveats/limits.
- Kept warnings as a compact top status row only.
- Changed watch-more-vs-like UI labels to `经常观看`, `经常喜爱`, and `经常想看`.
- Changed manual profile refresh semantics to "check and update":
  - If a valid cache exists and `SourceFingerprint` is unchanged, return the cached profile.
  - Do not call AI in the unchanged case.
  - Add the status message `画像数据没有变化，已显示最新画像。`.
  - Do not update `LastManualRefreshAtUtc` in the unchanged check path because no regeneration occurred.
  - Regenerate immediately when fingerprint changed, cache is missing, cache is stale, or cache cannot be parsed.
- Kept the automatic refresh rule unchanged: fingerprint changed plus at least 1 day since last automatic refresh.

Boundaries kept:
- No recommendation-system connection.
- No force-regenerate UI.
- No persona image resources.
- No database field or migration.
- No player, resource library, Library Batch Ops, delete, scan, or automatic-watched changes.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-4.1 Started

Goal: tighten two WI-4 boundaries without deleting watch history or changing statistics口径.

Planned work:
- Add a persistent automatic-watched baseline on `Movie`.
- Set the baseline when the user manually marks a movie unwatched.
- Keep old `WatchHistory` for statistics, profile analysis, and future watch history pages.
- Filter automatic watched aggregate checks by the baseline.
- Audit reset-recognition behavior and prevent reset placeholders from inheriting old movie watch history/progress.
- Add and apply migration `AddMovieAutoWatchedBaseline`.
- Run `dotnet build MediaLibrary.sln`.

Out of scope:
- No WatchHistory deletion.
- No backfill.
- No UI or setting screen.
- No AI call or profile generation.
- No recommendation-system, player-core, Library Batch Ops, scan-main-flow, or statistics口径 change.

## WI-4.1 Completed

Audit result:
- Manual library watched state changes go through `MovieManagementService.SetWatchedAsync`.
- Movie detail watched/unwatched and Library batch watched/unwatched both reuse that service for in-library movies.
- External collection watched changes use `UserCollectionService.SetWatchedAsync`; only entries linked to a `MovieId` can update a movie baseline.
- Automatic watched aggregate checks are done in `WatchHistoryService` by loading same-movie `WatchHistory` rows and passing them to `WatchCompletionEvaluator`.
- `WatchHistory.StartedAt` is available on every history row and is suitable as the aggregate-baseline boundary.
- Reset recognition previously moved the reset media file's `WatchHistory` to the new unidentified placeholder.
- Playback resume uses current `movieId` plus current media-file ids, so once old histories stop moving to the placeholder, the reset placeholder does not inherit old movie resume progress.
- Watch statistics load only identified movies and exclude unidentified/no-TMDB placeholders.

Completed:
- Added `Movie.AutoWatchedBaselineAtUtc`.
- Generated migration `20260509210831_AddMovieAutoWatchedBaseline`.
- Applied the migration to the local database.
- `MovieManagementService.SetWatchedAsync(movieId, false)` now sets `AutoWatchedBaselineAtUtc=UtcNow`.
- `UserCollectionService.SetWatchedAsync(..., false)` sets the baseline when the collection item is linked to a real `MovieId`.
- Automatic watched aggregate history loading now ignores rows with `StartedAt <= AutoWatchedBaselineAtUtc`.
- The current run participates in aggregate only when its `StartedAt` is after the baseline.
- Single-run completion still works independently of aggregate history.
- Reset recognition no longer moves old `WatchHistory` to the unidentified placeholder.
- Reset recognition logs `movedWatchHistory=false` and records that resume is cleared for the reset source.

Boundaries kept:
- Old `WatchHistory` rows remain stored.
- Old `WatchHistory` rows remain available to statistics, AI profile inputs, and future watch history pages.
- No identified movie statistics were changed to source-level statistics.
- No UI was added.
- No AI was called.
- No recommendation, player-core, Library Batch Ops, or scan-main-flow change was made.

Known edge:
- If a playback run started before the manual-unwatched baseline and continues after it, that run is excluded from aggregate history, but can still complete through the single-run rule if it has enough real watched duration and reaches the ending condition during the save.

Validation:
- `dotnet ef migrations add AddMovieAutoWatchedBaseline --project src/MediaLibrary.Core/MediaLibrary.Core.csproj --startup-project src/MediaLibrary.Core/MediaLibrary.Core.csproj`
- Result: migration generated successfully.
- `dotnet ef database update --project src/MediaLibrary.Core/MediaLibrary.Core.csproj --startup-project src/MediaLibrary.Core/MediaLibrary.Core.csproj`
- Result: migration applied successfully.
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-4.1 Batch-2 Compatibility Audit Completed

Goal: confirm the WI-4.1 reset-to-unidentified boundary does not break Library Batch Ops Batch-2 AI assisted identification.

Audit result:
- Batch-2 entry is `LibraryViewModel.BatchAutoIdentifyAsync`.
- Batch-2 calls `IMovieIdentificationService.AutoIdentifyWithFirstResultAsync`.
- Batch-2 does not call `ResetMediaFileToUnidentifiedAsync`.
- No-result, AI-suggestion failed, TMDB-search no-result, TMDB-detail failure, cancellation, and apply failure paths return without moving `MediaFile.MovieId`, moving `WatchHistory`, changing `Movie.IsWatched`, or changing `AutoWatchedBaselineAtUtc`.
- Batch-2 success loads the current movie and applies the first TMDB candidate through `ApplyManualMatchCoreAsync`.
- When the candidate matches the same/current movie, metadata is updated in place and existing playback history/resume state stays with that movie.
- When the candidate merges into an existing same-TMDB movie, existing product semantics still move media files, watch histories, and collection items from the source movie to the target movie.
- This merge behavior is intentionally distinct from reset-to-unidentified. The WI-4.1 "do not inherit old history" rule applies only to reset-to-unidentified placeholders.

Compatibility fix:
- Added baseline merge handling in `ApplyManualMatchCoreAsync`.
- When merging source movie into an existing target movie, `AutoWatchedBaselineAtUtc` now uses the newer of source and target baselines.
- The source movie baseline is cleared after transfer to avoid stale state on a movie that may be cleaned up.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-4.1 Manual Log Revalidation Completed

Goal: confirm the automatic watched baseline, reset-recognition ownership boundary, and Batch-2 AI assisted identification behavior after manual testing.

Log evidence reviewed:
- `logs/watch-completion.log`
- `logs/ai-perf-debug.log`
- `logs/mpv-playback.log`

Automatic watched baseline result:
- Manual unwatched set a new baseline for `movieId=40` at `2026-05-09T21:54:50.8465192Z`.
- Later short playback saves for `movieId=40`, `mediaFileId=13` showed `baselineSet=true` and `watch-completion-baseline-applied`.
- The aggregate check after the baseline kept `totalWatched=0`, `maxPosition=0`, and `validRunCount=0`.
- The completion result stayed `single=false`, `aggregate=false`, `historyCompleted=false`, `movieAutoWatched=false`, with reason `watched-too-short`.
- This confirms old watch history was not immediately reused to auto-mark the movie watched after manual unwatched.

Reset-recognition boundary result:
- Reset log showed `media-identification-reset-boundary mediaFileId=10 oldMovieId=29 newMovieId=41 movedWatchHistory=false`.
- Reset log also showed `media-identification-reset-resume-cleared mediaFileId=10 reason=reset-to-unidentified`.
- This confirms reset-to-unidentified moved only the media file to a new placeholder and did not migrate old watch history into that placeholder.

Batch-2 AI assisted identification result:
- Recent Batch-2 run started with `count=2`.
- The reset placeholder `movieId=41` completed suggestion, TMDB search, TMDB detail, apply DB, commit, and apply steps with `status=success`.
- The same run also completed the second selected item successfully.
- Batch completed with `success=2 noResult=0 failed=0 cancelled=0`.
- The log showed `recommendation-refresh-deferred reason=avoid-immediate-ai-refresh`, so Batch-2 did not trigger an immediate recommendation AI refresh.

Conclusion:
- Manual log validation found no blocker in WI-4.1 automatic watched baseline behavior.
- Manual log validation found no blocker in reset-to-unidentified history/progress ownership boundaries.
- Manual log validation found no blocker in Batch-2 AI assisted identification compatibility.
- WI-4.1 is acceptable for manual acceptance based on the reviewed logs.

## WI-4 Misclassification Fix Completed

Goal: diagnose and minimally fix an automatic watched false positive where a long video could be marked watched after seeking near the ending without enough real watch duration.

Diagnosis:
- Persistent `logs/mpv-playback.log` contained recent `mpv-watch-history-save` rows with `completed=true`, including near-ending positions.
- Existing `watch-completion-*` diagnostics were written through `Debug.WriteLine` only, so there was no durable completion-evaluator log for the misclassified run.
- Code evidence showed `WatchHistoryService.SaveProgressAsync` still used `history.IsCompleted || isCompleted || completionResult.IsSingleWatchCompleted`.
- That meant the player-side legacy `isCompleted=true` signal, based only on `PositionSeconds / DurationSeconds >= 0.9`, could bypass the evaluator's minimum watched-duration rule.
- The aggregate rule also used the current high seek position when calculating `maxPositionSeconds`, even if the current run had too little watched duration to be an effective watch run.

Fix:
- Removed unconditional trust in external `isCompleted=true`.
- Current `WatchHistory.IsCompleted` is now set only when it was already completed or the evaluator's single-run rule passes.
- `Movie.IsWatched` is now auto-marked only when the evaluator reports single-run or aggregate completion.
- Aggregate `maxPositionSeconds` now comes only from effective watch runs with `DurationWatchedSeconds > 60`, preventing a pure seek from satisfying the 70% position requirement.
- Added durable completion diagnostics to `logs/watch-completion.log`.

Diagnostics added:
- `watch-completion-input`
- `watch-completion-single-check`
- `watch-completion-aggregate-check`
- `watch-completion-result`
- `watch-completion-single-pass`
- `watch-completion-aggregate-pass`
- `watch-completion-auto-mark-watched`
- `watch-completion-skip reason=external-completed-rejected`

Revalidation recommendations:
- Long video with little/no history: play a few seconds, seek to 90% or the ending, stop. Expected: not auto-watched; log should show `external-completed-rejected` or `watched-too-short`.
- Long video: actually watch at least `min(20 minutes, 25% runtime)`, then reach 90% or the ending. Expected: auto-watched.
- 3-minute video: actually watch at least 45 seconds, then reach 90%. Expected: auto-watched.
- 3-minute video: play only a few seconds and seek to the ending. Expected: not auto-watched.
- Multi-session: cumulative effective watch time reaches 80% and effective max position reaches 70%. Expected: `Movie.IsWatched=true`; the current short run is not necessarily `WatchHistory.IsCompleted=true`.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-3 Polish Completed

Goal: split Watch Statistics refresh semantics from future Profile refresh semantics, and let Watch Statistics refresh automatically when local data changes.

Completed:
- Audited the WI-3 refresh button behavior.
- Confirmed the old refresh button was visible in the page Tab header area and called `IWatchStatisticsService.GetStatisticsAsync(forceRefresh:true)`.
- Moved the manual refresh action into the Watch Statistics overview area.
- Kept the button text/semantics as statistics-only refresh.
- Added `PageViewModelBase.Deactivate()` and called it when the main shell changes pages.
- Added active/inactive tracking to `WatchInsightsViewModel`.
- Subscribed `WatchInsightsViewModel` to `IDataRefreshService.DataChanged`.
- Automatic statistics refresh now listens for library, metadata, collection, scan, and playback-history changes.
- Added 600 ms debounce for data-change statistics refresh.
- Added concurrency protection: if a refresh is already running, later refresh requests are merged and run once afterward.
- If the Watch Insights page is inactive, data changes mark statistics refresh pending and the next activation forces a statistics refresh.
- Automatic refresh failure preserves the previous statistics snapshot and adds a warning.

Boundaries kept:
- No AI calls.
- No AI profile generation.
- No profile refresh button.
- No automatic watched algorithm implementation.
- No database field or migration.
- No player, recommendation, Library Batch Ops, or scan main-flow changes.

Refresh semantics:
- Watch Statistics refresh is fast local data refresh and may run on local data changes.
- Profile refresh is reserved for WI-5/WI-6 and remains independent.
- Future profile automatic refresh should use the 1-day minimum interval rule.
- Watch Statistics refresh must not trigger profile AI generation.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-4 Started

Goal: implement conservative automatic watched detection from playback progress and watch history without adding database fields, migrations, AI calls, or page changes.

Planned work:
- Audit `WatchHistoryService.StartAsync`, `SaveProgressAsync`, player progress persistence, and manual watched state paths.
- Add a concentrated completion evaluator for single-session and multi-session rules.
- Set `WatchHistory.IsCompleted` from the external completed signal or the current-run completion rule.
- Automatically set `Movie.IsWatched=true` when a not-yet-watched movie is completed by a current run or by aggregate history.
- Preserve manual state semantics by only allowing automatic `false -> true` watched writes.
- Run `dotnet build MediaLibrary.sln`.

Out of scope:
- No AI call or AI profile generation.
- No profile-driven recommendation.
- No watch history page.
- No statistics UI change.
- No database field or migration.
- No full-history backfill.
- No automatic watched settings UI.
- No player core playback change.
- No recommendation, Library Batch Ops, or scan main-flow change.

## WI-4 Completed

Completed:
- Added `WatchCompletionEvaluator`, `WatchCompletionOptions`, `WatchCompletionResult`, and `WatchCompletionHistoryItem`.
- Added single-session completion detection:
  - Progress is at least 90%, or position is within 300 seconds of the media end.
  - Real watched duration is at least `min(20 minutes, 25% runtime)`.
  - Media duration must be known and greater than 60 seconds.
- Added multi-session completion detection:
  - Effective histories require `DurationWatchedSeconds > 60`.
  - Aggregate watched duration must reach at least 80% of runtime.
  - Max historical position must reach at least 70% of runtime.
- Updated `WatchHistoryService.SaveProgressAsync` to evaluate completion every time progress is saved.
- Respected external `isCompleted=true` while allowing local completion rules to upgrade external `false` to completed.
- Set `WatchHistory.IsCompleted=true` only for current-run or external completed watches.
- Set `Movie.IsWatched=true` automatically when a not-yet-watched movie completes through current-run or aggregate rules.
- Kept automatic watched writes one-way: automatic logic never writes watched back to false.
- Updated matching `UserMovieCollectionItem` rows in the same database context when auto-marking watched, clearing `IsWantToWatch` and setting collection `IsWatched=true`.
- Changed watch-progress persistence to report whether automatic watched state changed.
- Player progress persistence now sends the existing collection-change refresh event when automatic watched state changes, so Watch Insights statistics can refresh through the WI-3 polish data-change path.
- Added lightweight debug logs for single-pass completion, aggregate-pass completion, auto-mark watched, and no-duration skips.

Boundaries kept:
- No old `WatchHistory` records were backfilled.
- No database field or migration was added.
- No AI call or AI profile generation was added.
- No profile-driven recommendation was added.
- No player core playback logic was changed.
- No recommendation, Library Batch Ops, or scan main-flow change was made.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-5 Started

Goal: implement AI profile input aggregation, profile generation, and profile cache behavior without connecting profile-driven recommendation or adding UI.

Prerequisite documentation closeout:
- WI-4 automatic watched algorithm is complete.
- External player `isCompleted=true` no longer bypasses the anti-seek watched-duration rule.
- After manual unwatched, old `WatchHistory` is retained but excluded from automatic watched aggregation by `Movie.AutoWatchedBaselineAtUtc`.
- Reset-to-unidentified creates a clean placeholder that does not inherit old movie state, resume progress, watch history, or auto-watched baseline.
- Batch-2 AI assisted identification is an apply/merge workflow and does not use the reset-to-unidentified clean-placeholder rule.
- Scan failure placeholders keep the existing behavior because they may represent movies that TMDB cannot identify.
- Library Delete UX-1 split delete semantics into soft `移出资源库` and software-record `删除影片记录`.
- `移出资源库` preserves history/state and keeps stateful movies discoverable in the resource library or Favorites.
- Favorites shows all want-to-watch and favorite movies without a library/external split.
- `删除影片记录` is the operation that removes a movie from the resource library, Favorites, Watch Insights statistics, and future profile inputs.
- Physical local files and WebDAV files are not deleted by either delete operation. Ignore-list behavior remains deferred.

Planned work:
- Add `IWatchProfileInputService`.
- Add `IWatchProfileService`.
- Build profile input from identified movies, collection state, watch history, rating sources, and WI-2 local statistics.
- Add data-insufficient guardrails.
- Generate structured JSON profile through existing `IAiService`.
- Cache profile results in `WatchInsightCacheEntries`.
- Support manual refresh and the 1-day automatic refresh rule.

Out of scope:
- No profile-driven recommendation.
- No recommendation prompt/fingerprint change.
- No Watch Insights UI change.
- No database field or migration.
- No player, Library Batch Ops, delete, scan, or automatic watched change.

## WI-5 Completed

Completed:
- Added `WatchProfileInputSnapshot` and supporting profile-input read models.
- Added `WatchProfileSnapshot` and supporting structured profile read models.
- Added `IWatchProfileInputService` / `WatchProfileInputService`.
- Added `IWatchProfileService` / `WatchProfileService`.
- Registered both services in DI.
- Profile input includes identified movies, linked and external collection state, valid watch history, rating sources, and WI-2 local statistics.
- Profile input excludes unidentified, identification-failed, and no-TMDB movies.
- Profile input does not include RF-2 custom recommendation preferences.
- Added source fingerprinting for profile-related movie state, collection state, watch histories, rating sources, and the WI-2 statistics fingerprint.
- Added data-insufficient rules: fewer than 8 signal movies, fewer than 2 signal buckets, or no usable tags means no AI call.
- Added AI JSON prompt and parser for `WatchProfileSnapshot`.
- Enforced the then-current persona-type set. WI-8.1 supersedes this with 23 persona types and fallback to `类型探索家`.
- Enforced six Watch DNA genes and score/confidence clamping.
- Made taste quadrant scores local-statistics-first and clamped to -100 to 100.
- Cached successful profile output under `kind=profile`, `scopeKey=global`.
- Manual refresh uses `GetProfileAsync(forceRefresh:true)` and bypasses the 1-day automatic interval.
- Automatic refresh uses `GetProfileAsync(forceRefresh:false)` and refreshes only when fingerprint changes and the last automatic refresh is at least 1 day old.
- AI failure preserves the previous cached profile when available.
- Added lightweight privacy-safe profile logs without full prompts, full responses, paths, URLs, or tokens.

Boundaries kept:
- No database field or migration.
- No Watch Insights page/navigation/UI change.
- No profile-driven recommendation or recommendation prompt/fingerprint change.
- No player, Library Batch Ops, delete, scan, or automatic-watched logic change.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-5 Persona Set Finalization

Goal: update the Watch Profile persona taxonomy to the fixed persona set without changing UI, cache schema, recommendation logic, or database schema. This section is superseded by WI-8.1, which updates the final set to 23 types.

Audit result:
- The persona type list is defined once in `WatchProfileService.PersonaTypes`.
- `WatchProfileService` validates `persona.type`; WI-8.1 changes invalid fallback to `类型探索家`.
- `WatchProfileSnapshot` stores persona fields but does not hard-code persona names.
- `WatchProfileInputService` does not define persona types.
- The WI-5 prompt already injected the persona list, but did not yet include the final boundary definitions and selection rules.

Completed:
- Confirmed the then-current persona names were represented in `WatchProfileService.PersonaTypes`; WI-8.1 supersedes this with 23 formal persona types.
- Added final persona boundary definitions to the profile prompt.
- Added conflict-resolution selection rules so AI picks the strongest differentiating feature.
- WI-8.1 updates invalid persona warning to `AI 返回了未知人格类型，已回退为类型探索家。`.
- Documented the final persona set and boundary policy.

Superseded persona set:
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

Boundaries kept:
- No UI change.
- No persona image assets.
- No profile cache table/schema change.
- No database field or migration.
- No recommendation prompt or profile-driven recommendation change.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

## WI-6 Started

Goal: connect the Profile Analysis Tab to the real WI-5 profile service while keeping profile refresh independent from statistics refresh and recommendation generation.

Planned work:
- Inject `IWatchProfileService` into `WatchInsightsViewModel`.
- Load profile state on page activation with `GetProfileAsync(forceRefresh:false)`.
- Add a profile-only manual refresh command with `GetProfileAsync(forceRefresh:true)`.
- Render taste summary, keywords, persona, Watch DNA, quadrant, watch-more-vs-like, likes, dislikes, future preference, warnings, and caveats.
- Preserve data-insufficient and AI-failure states.

Out of scope:
- No profile-driven recommendation.
- No recommendation prompt/fingerprint change.
- No persona poster/image resources.
- No database field or migration.
- No player, Library Batch Ops, delete, scan, or automatic-watched change.

## WI-6 Completed

Completed:
- `WatchInsightsViewModel` now injects `IWatchProfileService`.
- Page activation starts profile loading with `GetProfileAsync(forceRefresh:false)`.
- Statistics loading remains independent and does not wait for profile generation.
- Added `RefreshProfileCommand` for manual profile refresh through `GetProfileAsync(forceRefresh:true)`.
- Manual profile refresh disables its own button and does not refresh statistics.
- Statistics refresh still calls only `IWatchStatisticsService` and does not trigger profile AI.
- Data-change events continue to refresh only Watch Statistics; they do not call profile AI.
- Profile Analysis Tab now renders:
  - Taste summary and keywords.
  - Persona type, title, description, and confidence.
  - Six Watch DNA genes with score, confidence, and descriptions.
  - Two-axis taste quadrant with a plotted point.
  - Watch-more-vs-like groups, with local statistics fallback when profile output omits the field.
  - Preferred genres, emotions, scenes, countries, and languages.
  - Avoid genres, emotions, scenes, and negative summary.
  - Future likely-to-enjoy and less-likely-to-enjoy directions.
  - Caveats and warning messages.
- Data-insufficient and error-without-cache states show component-level empty messaging instead of fake profile data.
- Old-cache-with-warning is displayed as real cached profile data plus warning chips.
- Added lightweight page logs for profile load/refresh start, completion, skip, and failure.

Boundaries kept:
- No recommendation-system connection.
- No persona image resources.
- No database field or migration.
- No final visual polish.
- No player, Library Batch Ops, delete, scan, or automatic-watched changes.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.
