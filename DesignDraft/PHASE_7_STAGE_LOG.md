# Phase 7 UI Rebuild Stage Log

Last updated: 2026-06-10

This is the living Phase 7 handoff log. Keep entries concise and stage-oriented. Do not turn this into a full code diff.

## Path Context

- Historical initial UI rebuild docs remain in `docs/ui-redesign/UI_REDESIGN_PLAN.md`, `UI_REDESIGN_STAGE_LOG.md` and `UI_REDESIGN_KNOWN_ISSUES.md`.
- Phase 7 is a later formal UI rebuild stage, so ongoing Phase 7 maintenance lives in `DesignDraft/PHASE_7_PLAN.md`, this log and `DesignDraft/PHASE_7_KNOWN_ISSUES.md`.
- Design source docs live in `DesignDraft`, especially `PHASE_6_COVERAGE_MATRIX.md` and `page-spec`.

## Phase 7.7 - History / Favorites / Watch Insights

### 7.7a - Audit And Shared UI Baseline

Completed:

- Read the 7.7 detailed plan, Phase 7 rules, Movie Discovery tab alignment notes, existing global styles, `MovieDiscoveryPage` tab template and `SettingsPage` tab template.
- Added `src/MediaLibrary.App/Resources/Styles/PageTabs.xaml` as the shared 7.7 top-tab baseline: manual tab button, last-tab button, hidden-header tab item fallback, divider and hidden-header tab control styles.
- Merged `PageTabs.xaml` in `App.xaml` so later 7.7 pages can reference the shared style keys.
- Kept accepted Movie Discovery and Settings local tab templates untouched. A full shared `TabControl` template would hard-code page-specific visible buttons and commands, so 7.7a only extracts the safe shared style primitives.
- Recorded the 7.7a execution result in `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77a\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no WatchHistoryPage, FavoritesPage or WatchInsightsPage visual rebuild;
- no Movie Discovery or Settings retargeting to the new shared styles;
- no history card, favorite card, insight card, calendar or chart component implementation;
- no watch-statistics口径, watch-profile, collection service, Core service, database schema, migration, database update, commit or push change.

Business logic changes:

- None.

Non-stage page changes:

- `App.xaml` now merges a new ResourceDictionary, but no current page references the new keys; no existing page layout or behavior changes.

Suggested commit message:

- `Phase 7.7a shared top tab baseline`

### 7.7b - Watch History Visual Alignment And Date Targeting

Completed:

- Rebuilt `WatchHistoryPage.xaml` from the old page-card row list into a fixed date-filter toolbar plus scrollable date-group poster grid.
- Replaced the date `ComboBox` with explicit `全部 / 今天 / 本周 / 本月 / 指定日期` filter buttons and kept the existing refresh command.
- Copied the Media Library poster-card visual direction for watch-history items: 180x270 poster card, rounded clip, placeholder, cached poster image, palette shadow host, top chips, bottom gradient, title, metadata/tag line and progress bar.
- Kept Movie / Episode history display and existing detail navigation semantics; no player history-write path or Watch Insights statistics scope was changed.
- Added WatchHistoryViewModel presentation projections and one-way WPF bindings needed by the poster card template.
- Recorded the 7.7b execution result in `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln --no-restore` passed with 0 warnings and 0 errors.
- Ran the desktop app and navigated to Watch History. The final run stayed open after navigation, and UI Automation found `观影历史`, all date filter buttons and `刷新`.
- Fixed runtime XAML binding issues found during app probing: `Run.Text` and `ProgressBar.Value` needed explicit one-way binding to read-only card projection properties.

Explicit non-goals:

- no delete-history, search, sort, batch selection, continue-play button or player history-write change;
- no FavoritesPage or WatchInsightsPage visual work;
- no Core service, database schema, migration, database update, commit or push change.

Business logic changes:

- None. Changes are App-layer UI commands/projections and XAML presentation only.

Non-stage page changes:

- None. Shell, Movie Discovery, Media Library, Favorites, Watch Insights, Player and Core services were not edited in 7.7b.

Suggested commit message:

- `Phase 7.7b align watch history UI`

### 7.7c - Favorites Visual Alignment And Remove State

Completed:

- Rebuilt `FavoritesPage.xaml` from the old visible-header `TabControl` and page-card shell into a root `Grid > TabControl` with a Movie Discovery-style manual top tab strip for `喜爱` / `想看`.
- Used the 7.7a `PageTabs.xaml` primitives for fixed-width manual tab buttons, selected underline and divider placement while keeping Movie Discovery and Settings local accepted templates untouched.
- Rebuilt Favorites content as a scrollable 180x270 poster-card grid for Movie / Season collection items, with poster placeholder, cached poster image, type chip, rating badge, collection chip, title, date / season auxiliary text and source / watch-state summary.
- Added FavoritesViewModel presentation state for default `喜爱` tab selection, tab count headers, loading/error/empty states, card display fields and item-level remove action state.
- Kept existing collection service semantics: Movie favorite removal uses movie management, Movie want-to-watch removal uses user collection service, Season favorite / want-to-watch removal uses TV season collection service.
- Added item-level `取消中...`, failure text and real disabled reason projection. Missing stable `MovieId` for an external favorite is not faked as removable.
- Recorded the 7.7c execution result in `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\xfverse-build-7-7c\"` passed with 0 warnings and 0 errors.
- Normal build to the default output folder was blocked by a running `MediaLibrary.App` process locking the App binary / DLL; the temp-output build confirms the code and XAML compile.

Explicit non-goals:

- no search, sorting, filtering, batch selection, delete-record, scan-entry or continue-play feature on Favorites;
- no Watch Insights, watch-statistics, watch-profile, recommendation, Movie-only boundary or Core service change;
- no database schema, migration, database update, commit or push change.

Business logic changes:

- None in Core services. Changes are App-layer UI state/projections and XAML presentation only.

Non-stage page changes:

- None. Media Library, Movie Discovery, Watch History, Watch Insights, detail pages, Player, Shell and collection services were not edited in 7.7c.

Suggested commit message:

- `Phase 7.7c align favorites UI`

### 7.7d - Watch Insights Shell Tabs And State Baseline

Completed:

- Rebuilt `WatchInsightsPage.xaml` around a root `Grid > TabControl` with hidden native headers and a Movie Discovery-style manual top tab strip for `画像分析` / `观影统计`.
- Used the 7.7a `PageTabs.xaml` primitives for fixed-width manual tab buttons, selected underline and divider placement while keeping Movie Discovery, Settings and Favorites accepted templates untouched.
- Removed the old rounded in-page tab button group and kept the page title/subtitle owned by Shell.
- Gave Profile Analysis and Watch Statistics one main vertical scroll surface each, with `ScrollBarAutoRevealBehavior`, disabled horizontal page scrolling and page-local modern scrollbar styling.
- Added `SelectedTabIndex` / `SelectTabCommand` as the TabControl binding projection while preserving existing profile/statistics selected-state properties.
- Added App-layer `InsightModuleState` projection for loading, empty, error, data-insufficient, config-missing, generation-failed and cached-fallback states.
- Sanitized Watch Insights UI status messages so paths, URLs and secret-like values are not shown in module-state text.
- Recorded the 7.7d execution result in `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77d\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no final visual rebuild of the five Profile Analysis modules;
- no final visual rebuild of Watch Statistics overview, calendar or charts;
- no AI prompt, watch-profile service, watch-statistics service, recommendation, player, scan, collection service, Core business rule, Movie-only boundary, database schema, migration, database update, commit or push change.

Business logic changes:

- None in Core services. Changes are App-layer UI state/projections, XAML shell layout and UI status sanitization only.

Non-stage page changes:

- None. Movie Discovery, Settings, Favorites, Watch History, Media Library, detail pages, Player and Shell were not edited in 7.7d.

Suggested commit message:

- `Phase 7.7d align watch insights shell`

### 7.7e - Watch Insights Profile Analysis Visual Closeout

Completed:

- Rebuilt the Profile Analysis tab into five sketch-aligned modules: taste summary, watch DNA, persona, taste quadrant and watch-vs-like comparison.
- Reworked the summary area as a readable summary/status/refresh/keyword layout while preserving the 7.7d module-state projection for loading, empty, insufficient-data, config-missing, generation-failed and cached-fallback states.
- Added App-layer display projections for profile DNA icon text, subtitle and progress values without changing watch-profile service output, cache shape or AI generation semantics.
- Kept persona rendering on the existing WatchPersona asset mapping and adjusted only the visual frame, image sizing and text hierarchy.
- Reframed the taste quadrant as explanation plus a compact coordinate panel using existing X/Y scores and UI-only coordinate scaling.
- Rebuilt watch-vs-like as three Top3 groups with progress bars and a wrapped conclusion area.
- Removed old hidden profile placeholder / fallback visual blocks from the page.
- Recorded the 7.7e execution result in `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77e\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no AI prompt, AI model selection, profile input aggregation, profile cache fingerprint, recommendation profile, watch-statistics service, player, scan, collection service, Core business rule, Movie-only boundary, database schema, migration, database update, commit or push change.
- no rebuild of Watch Statistics range, overview, calendar or chart visuals; those remain for 7.7f / 7.7g.

Business logic changes:

- None in Core services. Changes are App-layer profile display projections and XAML visual layout only.

Non-stage page changes:

- None. Movie Discovery, Favorites, Watch History, Settings, Media Library, detail pages, Player and Shell were not edited in 7.7e.

Suggested commit message:

- `Phase 7.7e polish watch insights profile`

### 7.7f - Watch Insights Statistics Upper Area

Completed:

- Rebuilt the Watch Statistics upper area into the sketch-aligned overview and calendar modules.
- Kept the overview to the four product-approved status cards: watched, favorite, want-to-watch and not-interested. The sketch-only unwatched card was not restored.
- Moved month / all range switching into the overview header as segmented buttons bound to the existing statistics range commands.
- Kept the statistics refresh action and last refresh text in the overview header; refresh still calls only the statistics service and does not trigger profile generation or recommendations.
- Added App-layer overview display projections for status-card subtitle and visual kind without changing statistics service output.
- Added `StatisticsOverviewCardTemplate`, range button styles, metric panel style and calendar legend swatch style in `WatchInsightsPage.xaml`.
- Moved total watch time, current-range watch count and high-frequency tags into the overview lower metrics row.
- Rebuilt the calendar module as month controls, heat legend, weekday row, calendar grid and three right-side monthly metric cards.
- Replaced calendar heat-level hex colors with existing theme resource keys and kept calendar date clicks routed through the existing Watch History target-date navigation.
- Recorded the 7.7f execution result in `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77f\"` passed with 0 warnings and 0 errors after clearing prior temporary Codex build output directories from `%TEMP%`.

Explicit non-goals:

- no rebuild of the Watch Statistics lower area: preference graph, tag rankings, viewing rhythm or taste combination map remain for 7.7g.
- no watch-statistics service semantics, automatic watched algorithm, profile, recommendation, player, scan, collection service, Core business rule, Movie-only boundary, database schema, migration, database update, commit or push change.

Business logic changes:

- None in Core services. Changes are App-layer statistics display projections and XAML visual layout only.

Non-stage page changes:

- None. Watch History target-date navigation was reused as-is; Movie Discovery, Favorites, Watch History, Settings, Media Library, detail pages, Player and Shell were not edited in 7.7f.

Suggested commit message:

- `Phase 7.7f polish watch statistics overview`

### 7.7g - Watch Insights Statistics Lower Area

Completed:

- Rebuilt the Watch Statistics lower area into the sketch-aligned preference graph, tag ranking, viewing rhythm and taste combination modules.
- Replaced the old preference bubble wrap layout with a scalable fixed-canvas bubble graph using real type / emotion distribution data, resource-driven colors and a legend.
- Rebuilt tag rankings as three Top3 cards for type, emotion and scene tags, keeping rank, label, count and relative-progress bindings from the existing statistics snapshot.
- Reframed viewing rhythm as a time-bucket bar chart plus weekday/weekend comparison and duration distribution panels.
- Rebuilt the taste-combination map as a scalable node/link canvas plus Top10 combination list; graph nodes and links still come from real snapshot data, with the existing Top10 fallback when graph data is sparse.
- Added App-layer display-only projections for bubble positions, rhythm summary text, dominant duration text, node labels and graph sizing.
- Recorded the 7.7g execution result in `docs/ui-redesign/PHASE_7_7_HISTORY_FAVORITES_INSIGHTS_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77g\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no watch-statistics service semantics, tag aggregation, AI classification, scan recognition, cache fingerprint, profile, recommendation, player, collection service, Core business rule, Movie-only boundary, database schema, migration, database update, commit or push change.
- no 7.7 full regression closeout or 7.8 global Light / Dark polish.

Business logic changes:

- None in Core services. Changes are App-layer statistics display projections and XAML visual layout only.

Non-stage page changes:

- None. Movie Discovery, Favorites, Watch History, Settings, Media Library, detail pages, Player and Shell were not edited in 7.7g.

Suggested commit message:

- `Phase 7.7g polish watch statistics charts`

#### 7.7g Follow-up - Watch Insights Tab Alignment And Modern Surfaces

Completed:

- Matched the Watch Insights top Tab strip placement to Movie Discovery's accepted `TabControl` coordinates: negative top offset, top content padding, divider / underline alignment and selected-content margin source.
- Replaced Watch Insights main module card surfaces with `GlassPageCardStyle`.
- Retargeted Watch Insights inner panels to the shared glass inline-panel baseline.
- Changed the local Watch Insights scrollbar to the same resource-driven 6px modern scrollbar direction used by Discovery and Settings.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuildInsightsPolish\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no Watch Insights service semantics, profile generation, recommendation, Core business rule, Movie-only boundary, database schema, migration, database update, commit or push change.

#### 7.7g Follow-up - Watch Insights Profile Summary Layout

Completed:

- Removed the Profile Analysis top module-state card so the first visible card is the taste-summary card.
- Moved `刷新画像` into the taste-summary card header right action area with compact status and last-refresh text.
- Removed the three lower-left summary chips for status, refresh time and sample count.
- Fixed the taste-summary card height and aligned the summary panel bottom with the right keyword panel.
- Reduced the keyword panel height and changed core keywords to a two-row, three-column `UniformGrid`.
- Replaced keyword pill chips with low-radius rounded-rectangle keyword labels.

Validation:

- Initial build retry hit temporary build output disk-space exhaustion; after cleaning Codex temp build outputs, `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildWatchInsightsProfileLayoutFinal\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no Watch Insights service semantics, profile generation, recommendation, Core business rule, Movie-only boundary, database schema, migration, database update, commit or push change.

#### 7.7g Follow-up - Watch Insights Profile Refresh And Quote Summary

Completed:

- Moved `刷新画像` from the taste-summary card to the top Tab divider action area.
- The refresh action is visible only on the `画像分析` Tab and appears as a transparent MDL2 refresh icon button with `刷新画像` tooltip.
- Moved the last profile refresh time label to the left of the refresh icon.
- Explicitly restored arrow cursor behavior for the Profile and Statistics content surfaces.
- Switched large Watch Insights module cards to a page-local no-rectangular-shadow card style with larger bottom spacing.
- Added top / bottom scroll-content padding to protect card edges from clipping.
- Removed the cached/status pill from the taste-summary card.
- Tightened the taste-summary subtitle-to-content gap.
- Reworked the taste-summary text area as a quote-style layout with muted opening and closing quote marks.
- Empty summary content now shows centered empty-state text in the same content area without an extra border or background.
- App-layer summary formatting now preserves AI-returned natural paragraphs and only prepends two full-width spaces to each paragraph.
- Updated the profile summary prompt so future AI output asks for two natural paragraphs separated by a newline.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildWatchInsightsRefreshTabLayout\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no statistics service semantics, recommendation, player, scan, collection, Movie-only boundary, database schema, migration, database update, commit or push change.

#### 7.7g Follow-up - Preliminary Global Palette Alignment

Completed:

- Replaced the temporary beige / orange Light palette and teal Dark palette with the gray-pink, soft-pink accent and neutral-first direction from `DesignDraft/DESIGN.md`.
- Retuned shared glass gradients, selected states, focus, hover / pressed, empty, warning, success, danger and info resources while keeping the existing resource keys stable.
- Retuned the fixed dark player resource dictionary so player progress, focus ring, menu / popup surfaces and status colors no longer use the old teal temporary scheme.
- Repointed obvious page-local poster-card and collection-state brushes in Home, Library, Movie Discovery, Recommendations, Favorites and Watch History to the new shared palette, then toned this back where the result felt too pink.
- Restored poster-card top chips, rating-badge backgrounds, poster overlay text and poster-card progress tracks to the previous blue-gray / white-on-poster treatment after user feedback that these should not use the pink palette.
- Kept pink primarily for primary actions, selected states and limited accents.
- Lightened the dark theme away from dirty gray-pink mass surfaces by moving navigation, cards and glass surfaces toward cleaner neutral charcoal / warm-gray values.
- Repointed the shared progress bar fill to `BrushInfo`, but restored the progress track background to the previous light gray.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildGlobalPalette77\"` passed with 0 warnings and 0 errors.
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildPaletteLessPink77\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- This is still Phase 7.7 preliminary palette alignment, not Phase 7.8.
- no full 7.8 cross-page visual regression, screenshot QA, menu / dialog / detail-page polish, performance audit, business semantics, Movie-only boundary, database schema, migration, database update, commit or push change.

#### 7.7g Follow-up - Visual Details And Date Picker Feedback

Completed:

- Updated `SmartDatePicker` so clicking its switch while the dropdown is already open closes the dropdown instead of closing and immediately reopening through popup outside-click handling.
- Added a very fine white outline effect to solid poster-corner favorite / want icons in Favorites.
- Added the same very fine white outline effect to solid poster-corner want stars in Movie Discovery while leaving unselected empty stars unchanged.
- Added a very fine white outline effect to all five rating stars in Movie / Season / Episode detail rating cards, including empty stars.
- Added a 1px semi-transparent white right-edge border for the sidebar in Dark theme; Light theme keeps the same edge resource transparent.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77VisualFixes\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no date filtering business-rule change, collection-state semantics change, rating calculation change, detail-page data change, database schema, migration, database update, commit or push change.

#### 7.7g Follow-up - Search Want Star, Fractional Rating Stars And Official Site Link

Completed:

- Hardened `SmartDatePicker` click-to-close behavior for the popup-close-before-toggle-click order by suppressing the next switch-open when the open popup is closed by clicking the switch.
- Kept the Movie Discovery poster-card want star visible for watched movies; watched movies now show an empty star, ignore the want-state mutation and display the same transient page message channel used by page-load failures.
- Slightly thickened the fine white outline effect on solid poster-corner favorite / want icons in Favorites and Movie Discovery.
- Added `RatingStarsBar` for proportional five-star rendering and switched Movie, Series, Season and Episode detail rating cards to it, including the Series overview IMDb / TMDB cards.
- Changed the Settings About row to open `https://xfverse.fun` with the default browser.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gLatest\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- This is still a Phase 7.7g follow-up, not Phase 7.8.
- no database schema, migration, database update, recommendation / Watch Insights business semantics, scoring-source data, commit or push change.

#### 7.7g Follow-up - Date Picker Ownership And Semantic Toast Styling

Completed:

- Reworked `SmartDatePicker` ownership so the switch click is the single source of open / close state: the popup no longer auto-closes through `StaysOpen=False`, and the switch no longer two-way writes `IsDropDownOpen`.
- Added owner-window outside-click closing for `SmartDatePicker`, keeping clicks inside the picker and popup content open.
- Thickened solid poster-corner favorite / want icon outlines slightly beyond the previous follow-up.
- Moved rating-star stroke color behind a theme resource: Light uses the sidebar background color, Dark keeps the white stroke.
- Added semantic transient page-message styling in Movie Discovery for Info / Warning / Error / Success, using translucent status backgrounds, matching borders / foregrounds and a higher placement.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gDatePickerToast\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no global toast service extraction, database schema, migration, database update, commit or push change.

#### 7.7g Follow-up - Profile Card Shadow, Refresh State And Summary Readability

Completed:

- Restored the Watch Insights profile module-card shadow by letting profile cards inherit the Home-aligned `GlassPageCardStyle` shadow again, and increased module spacing / scroll safety padding.
- Added a loading spin animation to the profile refresh glyph while a new profile is being generated.
- Replaced the top-right profile timestamp label with a combined refresh-status label that carries loading, generation time, cache / failure and insufficient-data states.
- Removed the separate status title below the taste summary area so status text is not duplicated under the first profile card.
- Added local summary normalization fallback in `WatchProfileService`: preserve AI paragraphs when present; split one long paragraph near sentence punctuation into two paragraphs; keep UI paragraph indentation and quote presentation.
- Kept the keyword count capped at 6, while adding a subtle 2x3 slot grid behind keyword chips to reduce empty visual space without inventing extra keywords.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gWatchInsightsProfilePolish\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no profile input-scope, Movie-only boundary, recommendation logic, database schema, migration, database update, commit or push change.

## Phase 7.6 - Settings / Scan / Cache

### 7.6a - Shared Component Baseline

Completed:

- Added reusable UI shells for 7.6: `SensitiveSettingInput`, `SettingFieldRow`, `ApiConfigCard`, `CacheCategoryCard` and `ScanPathCard`.
- Reused existing global ResourceDictionary styles and helper behaviors instead of introducing page-local color or spacing rules.
- Kept long-field handling on the existing true-truncation tooltip behavior and kept sensitive input display / reveal as UI-only presentation.
- Recorded the 7.6a execution result in `docs/ui-redesign/PHASE_7_6_SETTINGS_SCAN_CACHE_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no SettingsPage or ScanTasksPage layout rewrite yet;
- no path picker, scan log card, local-path remove Popover or cache workflow implementation;
- no scanner, API save / test, cache cleanup, Core service, database schema, migration, database update, commit or push change.

Business logic changes:

- None.

Non-stage page changes:

- None. The new controls are not wired into accepted pages in 7.6a.

Suggested commit message:

- `Phase 7.6a shared settings scan components`

### 7.6b - Settings General Tab And Cache Management

Completed:

- Rebuilt the Settings `通用` Tab around the Phase 7.6 structure: page title with theme icon, software cache management, behavior settings and About row.
- Wired the page content `ScrollViewer` to the modern auto-reveal scrollbar behavior.
- Reused 7.6a `CacheCategoryCard` for the three real software cache categories: poster cache, metadata / other cache and online subtitle cache.
- Reused 7.6a `SettingFieldRow` for poster-cache size limit and behavior-setting rows.
- Bound poster cache clearing to the real `IsPosterCacheClearAvailable` state, matching the existing other-cache and subtitle-cache disabled-state behavior.
- Kept cache cleanup buttons as danger actions and preserved confirmation copy that does not imply deleting media sources.
- Audited general settings. Theme mode is the only real persisted behavior setting in this slice; close-window behavior, play-open fullscreen and auto WebDAV scan are displayed as disabled / read-only current behavior.
- Added App-layer theme-change notification so changing theme in Settings also refreshes the main title-bar theme icon and tooltip.
- Recorded the 7.6b execution result in `docs/ui-redesign/PHASE_7_6_SETTINGS_SCAN_CACHE_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed with LF / CRLF warnings only.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Explicit non-goals:

- no API configuration Tab rebuild;
- no ScanTasksPage, path picker, scan progress, scan log or local-path removal Popover work;
- no player fullscreen behavior change;
- no close-to-tray behavior;
- no auto WebDAV scan scheduler in the initial 7.6b pass; a later follow-up wired startup WebDAV auto-scan as App-layer behavior;
- no Core scanner, cache-service, database schema, migration, database update, commit or push change.

Business logic changes:

- No Core business logic changed. The only behavior-level adjustment is App UI presentation sync: `IThemeService.ThemeChanged` notifies subscribers after applying a theme resource, and `MainWindowViewModel` refreshes its theme icon.

Non-stage page changes:

- MainWindow title-bar theme icon / tooltip now update when theme changes from Settings.
- No Player, ScanTasks, Library, Discovery, Detail or OnlineSubtitleSearch pages were edited.

Suggested commit message:

- `Phase 7.6b settings general cache management`

#### 7.6b Follow-up - Settings General Card Structure Alignment

Completed:

- Reworked the Settings `通用` Tab visual hierarchy to better match `01-设置-通用-全屏.png`: two large section cards, one continuous row-list panel inside cache settings, one continuous row-list panel inside behavior settings and an About row inside the behavior section.
- Removed implementation-stage explanatory copy from the visible general settings card headers.
- Kept the already approved real cache surface: poster cache, metadata / other cache and online subtitle cache only; no video-cache UI was restored.
- Added true-truncation tooltip behavior to row labels and primary values where overflow can happen, while leaving ordinary descriptions as wrapping text.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no API configuration rebuild;
- no scan-task, player, media-library, discovery, detail or online-subtitle page change;
- no cache-directory opener, video-cache entry, close-to-tray setting, player fullscreen setting or auto WebDAV scan backend;
- no Core service, database schema, migration, database update, commit or push change.

Business logic changes:

- None. This follow-up only changes `SettingsPage.xaml` structure and page-local styles.

#### 7.6b Follow-up - Settings General Sketch Structure Second Pass

Completed:

- Removed the visible full-page `PageCardStyle` shell from `SettingsPage.xaml`, so the `通用` and `API 配置` cards are no longer visually nested inside one large page card.
- Hid the duplicate in-page Settings title and description; the main shell title remains the page title, while the page retains the settings tabs. The later tab follow-up removed the extra in-page top-right theme button.
- Tightened the `通用` Tab spacing, section-card padding, row-list padding and row height so the cache section, behavior section and About row match the sketch hierarchy more closely.
- Changed behavior-setting descriptions to single-line ellipsis with the existing true-truncation tooltip behavior.
- Confirmed by runtime screenshots that the `通用` Tab now presents two large cards with continuous inner row lists, and the `API 配置` Tab keeps service cards directly in the page content area.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no cache-directory opener, video-cache entry, close-to-tray backend, player-fullscreen setting or auto WebDAV scan backend;
- no API save / test semantic change and no fake AI test command;
- no scan-task business logic, player, media-library, discovery, detail or online-subtitle page change;
- no Core service, database schema, migration, database update, commit or push change.

Business logic changes:

- None. This follow-up only changes `SettingsPage.xaml` visual hierarchy and page-local styles.

#### 7.6b Follow-up - Settings Tabs Match Movie Discovery

Completed:

- Changed `SettingsPage.xaml` back to a root `Grid` with the `TabControl` directly inside it, matching Movie Discovery's page-level tab placement.
- Replaced the visible Settings tab header with a Movie Discovery-style manual tab-button template; the native `TabPanel` is hidden and only backs content selection.
- Removed the extra in-page top-right theme toggle button. The main window title-bar theme button and the theme controls inside the general settings card remain.
- Kept the `通用` Tab without page-level scrolling and kept the `API 配置` Tab on a modern vertical scrollbar.
- Added Settings ViewModel tab-selection state / command only for the manual tab-button template.

Validation:

- First build attempt was blocked by a leftover running `MediaLibrary.App` process locking the app exe.
- After closing that process, `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no main-window title-bar theme button removal;
- no theme persistence, cache cleanup, API save / test, scan-task or Core service behavior change;
- no database schema, migration, database update, commit or push change.

Business logic changes:

- None. The new tab state is UI selection plumbing only.

#### 7.6b Follow-up - Settings General Detail Alignment

Completed:

- Removed the `缓存设置` header's right-side status text and refresh button; software cache status remains loaded by Settings activation.
- Aligned `缓存设置` and `行为设置` headings with the first-column row labels and added a longer, thicker, darker divider below each heading.
- Removed the inner rounded row-list containers from both setting cards while keeping lighter row-level separators.
- Changed right-side cache action buttons from fixed 154px width to text-driven compact widths.
- Reworked `海报缓存上限`: removed the separate `单位 MB` line, moved `MB` inside the input chrome, reduced input height, tightened the editable area and reduced the corner radius.
- Moved About out of the behavior settings card into a standalone bottom row and reused the XFVerse app icon geometry.
- Forced non-clickable Settings card / row surfaces to use the normal arrow cursor so only real controls show clickable affordance.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no cache loading / clearing, theme persistence, close-window, player-fullscreen or auto-scan behavior change;
- no API configuration, scan-task, player, media-library, discovery, detail or online-subtitle page change;
- no Core service, database schema, migration, database update, commit or push change.

Business logic changes:

- None. This follow-up is Settings general UI layout and local style only.

#### 7.6b Follow-up - Settings Behavior Switches And Local Preferences

Completed:

- Replaced Settings general behavior controls with segmented switches: close window, play-open fullscreen, theme mode and, at that moment, disabled auto WebDAV scan. A later follow-up wired startup WebDAV auto-scan as a real setting.
- Implemented close-window behavior as the user-defined `退出软件 / 缩小到托盘` choice, not an exit-confirmation dialog setting.
- Added App-layer local behavior preferences for close-window behavior and play-open fullscreen without Core schema fields or migrations.
- Added a tray icon lifecycle for the main window when close behavior is set to tray; tray menu can restore the window or exit.
- Added player-open fullscreen preference wiring so later PlayerWindow opens either fullscreen or normal-window according to Settings.
- Added `System` theme mode and kept the selected theme mode from being overwritten by the resolved Light / Dark resource.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no auto WebDAV scan scheduler in that specific pass; a later follow-up wired startup WebDAV auto-scan as App-layer behavior.
- no cache cleanup, API save / test, scan-task, recognition, online-subtitle or Core service behavior change;
- no database schema, migration, database update, commit or push change;
- no manual UI runtime test in this pass.

Business logic changes:

- App-layer behavior preference file `app-behavior-preferences.json` now stores close behavior and player-open fullscreen preference.
- `MainWindow` reads close behavior on close: `exit` exits, `tray` hides to system tray and can be restored or exited from the tray menu.
- `PlayerWindowService` reads player-open fullscreen preference before creating `PlayerWindow`.

Non-stage page changes:

- `MainWindow` close behavior is now affected by Settings.
- `PlayerWindow` initial fullscreen state is now affected by Settings. Existing playback controls and close-confirmation flow were not changed.

#### 7.6b Follow-up - Settings Alignment And Startup WebDAV Auto Scan

Completed:

- Fixed Settings general row alignment by using a fixed first column and elastic second column across cache and behavior rows.
- Changed the WebDAV auto-scan behavior row to a single-line `自动扫描 WebDAV` label and a real enabled segmented switch.
- Added `AutoScanWebDavOnStartup` to the App-layer behavior preference file.
- Added `StartupWebDavScanService`, which reads the preference after App startup and runs existing `IMediaScanService.RunScanAsync` in the background when enabled.
- Reduced Settings section heading row height, moved the heading text slightly downward, moved the About row lower and converted the whole About row into the click target.
- Removed the middle About-row description text while preserving app icon, label, version and a right-side indicator.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no new scan recognition rule, scan progress popup, pause control, Core schema field, migration, database update, commit or push;
- no API configuration, cache cleanup, player subtitle, media-library or discovery behavior change;
- no manual UI runtime test in this pass.

Business logic changes:

- Startup WebDAV auto-scan is now real App-layer behavior: when enabled, App startup queues a background call to existing `IMediaScanService.RunScanAsync` and then sends `NotifyScanChanged`.
- Existing manual ScanTasksPage scan commands and scanner semantics are unchanged.

Non-stage page changes:

- `App.xaml.cs` startup flow now queues the auto-scan check after normal startup.
- ScanTasksPage is not edited by this follow-up, but scan refresh notifications can arrive after startup auto-scan completes.

#### 7.6b Follow-up - Settings General Fine Alignment

Completed:

- Moved the Settings general cache / behavior row second column further right by widening the shared first column.
- Moved the About row lower in the fixed no-scroll general tab layout.
- Reduced cache / behavior section heading row height by roughly one third and tightened the divider offset.
- Second fine-tune pass moved the behavior settings card further down and changed the About row from ineffective top-margin spacing to an explicit bottom-anchored visual offset, because the previous final `Auto` row after a `*` spacer kept the row pinned to the available content bottom.
- Restored Settings general and API visible Chinese text that had become valid XAML but unreadable mojibake during the alignment pass.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="%TEMP%\\XFVerseCodexBuild\\"` passed with 0 warnings and 0 errors.
- A normal `dotnet build MediaLibrary.sln` reached compilation but was blocked by an already-running `MediaLibrary.App` process locking the Debug output files; that process was not terminated.

Explicit non-goals:

- no manual UI runtime test in this pass;
- no further cache, theme, close-window, player fullscreen or startup WebDAV auto-scan behavior change;
- no API save / test, scan-task, player, media-library, discovery, detail or online-subtitle page behavior change;
- no Core schema, migration, database update, commit or push change.

Business logic changes:

- None. This follow-up is SettingsPage layout and visible-copy repair only.

### 7.6c - Settings API Configuration Tab

Completed:

- Rebuilt the Settings `API 配置` Tab into four reused `ApiConfigCard` sections: TMDB, OMDb, OpenSubtitles and AI.
- Reused `SettingFieldRow` for API form rows and `SensitiveSettingInput` for password, token, API key and access-token style fields.
- Added UI input limits that match current `ApplicationSetting` storage boundaries: TMDB token 2048, TMDB API key 256, OMDb API key 256, OpenSubtitles endpoint 512, OpenSubtitles API key 512, OpenSubtitles username 256, OpenSubtitles password 2048, AI base URL 512, AI API key 2048 and AI model fields 128.
- Made OpenSubtitles structurally match the TMDB / OMDb cards: status badge, field rows, independent save, independent test and feedback text.
- Kept OpenSubtitles API-key-only mode explicit and kept username / password optional.
- Added safe card status projections in `SettingsViewModel` for TMDB, OMDb, OpenSubtitles and AI without exposing credential values.
- Removed the page-local OpenSubtitles `PasswordBox` sync code-behind path; the shared sensitive input now handles the binding.
- Added the editable AI default model field and kept the advanced routing list limited to current real AI call paths.
- Scoped the AI card save command to AI fields only, matching the card-level independent-save model.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed with LF / CRLF warnings only.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Explicit non-goals:

- no player subtitle menu, OnlineSubtitleSearchWindow, subtitle download, subtitle binding or delete-binding work;
- no OpenSubtitles client contract, probe, search or download service change;
- no detail-page subtitle entry;
- no ScanTasksPage, path picker, scan log or cache-cleanup business change;
- no database schema, migration, database update, commit or push change.

Business logic changes:

- No Core business logic changed. The only App-layer settings behavior change is that the AI card save now persists only AI fields instead of using the old private helper that also wrote unrelated API fields.

Non-stage page changes:

- None. Player, Detail, OnlineSubtitleSearch, ScanTasks and cache-management business pages were not edited.

Suggested commit message:

- `Phase 7.6c settings api configuration cards`

#### 7.6c Follow-up - Settings API Detail Alignment

Completed:

- Routed mouse-wheel events from API TextBox, PasswordBox and closed ComboBox controls back to the API tab ScrollViewer so input hover does not block page scrolling.
- Moved the sensitive-field reveal button inside `SensitiveSettingInput`, reduced API input chrome height, and let the input field occupy the old external eye-button width.
- Changed the TMDB optional key label to `API Key（可选）`.
- Reworked the `ApiConfigCard` status badge into a compact rounded rectangle with a breathing status light: green for test passed, yellow for untested and red for test failed or missing required test input.
- Removed field-level helper text below API inputs, the default-language selector and the AI advanced routing heading while retaining card-level descriptions.
- Updated the OpenSubtitles default-language selector to use the modern ComboBox style, Chinese display names and common-language-first ordering.
- Added App-layer API test status state for TMDB, OMDb and OpenSubtitles. Save returns the badge to untested; successful tests mark passed; failed tests and missing required keys mark failed. AI remains untested because no real AI test command exists.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild\"` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.
- No manual UI click test was performed in this pass.

Explicit non-goals:

- no AI test command;
- no OpenSubtitles probe/search/download contract change;
- no player subtitle flow, ScanTasksPage, general settings, cache management or media-library behavior change;
- no Core schema, migration, database update, commit or push change.

Business logic changes:

- None in Core. App-layer changes are limited to Settings API status projection and OpenSubtitles language display / sorting; stored language codes and existing API save / test service calls are unchanged.

Non-stage page changes:

- None. This follow-up only edited Settings API UI/control code and SettingsViewModel.

Suggested commit message:

- `Phase 7.6c settings api detail alignment`

### 7.6d - Scan Configuration And Path Pickers

Completed:

- Rebuilt the ScanTasks configuration area into structurally matched WebDAV and local-directory sections.
- Reused `ScanPathCard` for WebDAV / local path rows and `SettingFieldRow` for editor fields.
- Added WebDAV connection name editing and aligned WebDAV field limits to current storage boundaries: connection name 120, BaseUrl 500, username 200, password 1000, scan path 1000 and display name 200.
- Replaced the WebDAV password field with `SensitiveSettingInput`, hidden by default.
- Added App-layer `IScanPathPickerService` / `ScanPathPickerService`; local directory picking uses the system folder picker and does not enter Core.
- Added `WebDavPathPickerWindow` backed by `IWebDavService.ListDirectoryAsync`. It browses virtual WebDAV directories, supports parent / refresh / enter / select-current actions, and returns only the selected virtual path.
- Wired WebDAV and local picker commands in `ScanTasksViewModel`. Picker selection only fills the active edit field and optionally derives a safe display name when the display name is empty.
- Connected the page, path lists, log list and WebDAV picker list to the modern auto-reveal scroll behavior and kept horizontal scrolling disabled.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Explicit non-goals:

- no scan start, cancel, progress-statistics or scan-log visual closeout work;
- no pause-scan command or fake progress;
- no scanner recognition algorithm, AI / TMDB / OMDb / OpenSubtitles call semantics or Core write semantics change;
- no local scan-path remove Popover yet; 7.6e owns the remove confirmation and local-source visibility semantics;
- no database schema, migration, database update, commit or push change.

Business logic changes:

- No Core business logic or data model changed. The only new behavior is App-layer UI path picking.
- `ScanTasksViewModel` now has picker commands and safe default-display-name projections; save operations still use `ISettingsService.SaveScanPathAsync` and `SaveLocalScanPathAsync`.

Non-stage page changes:

- None. Settings, Player, Library, Discovery, Detail and OnlineSubtitleSearch pages were not edited for 7.6d.
- Shared DI registration was updated for the App-layer scan path picker service.

Suggested commit message:

- `Phase 7.6d scan path pickers`

#### 7.6d Follow-up - Season Overview And Scan Config Polish

Completed:

- Kept the Season detail episode overview in a three-and-a-half-line modern vertical scroll area, with horizontal scrolling and text trimming explicitly disabled.
- Tightened the ScanTasks WebDAV add-path and account cards by removing stretch rows, correcting the add-path folder glyph, and letting the save / test buttons size to their text.
- Changed the WebDAV account status projection to untested yellow, test-passed green and test-failed red; saving the WebDAV connection now runs one existing connection test automatically.
- Changed the local scan path list from `UniformGrid` to fixed-width `WrapPanel` rows so path boxes no longer resize when only a few paths exist.
- Set the local folder picker to a stable default initial directory when no explicit initial path is provided, instead of inheriting the system dialog's previous folder.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\xfv-b\"` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Explicit non-goals:

- no scanner recognition algorithm, media-library semantics, path-removal semantics, database schema, migration, database update, commit or push change.

Suggested commit message:

- `Phase 7.6d polish season overview and scan config`

#### 7.6d Follow-up - Scroll And Scan Progress Polish

Completed:

- Delegated mouse wheel input from non-scrollable Season episode overviews back to the outer episode list; scrollable episode overviews still consume their own wheel input.
- Replaced the WebDAV add-path folder glyph with a vector folder icon.
- Disabled the WebDAV save button unless the current fields differ from the saved connection baseline.
- Added a local folder picker fallback so an empty selected folder can still be returned and saved; folder contents are not validated in configuration.
- Restored the ScanTasks progress order to progress bar / current stage / current file above the metric cards, then narrowed metric cards and increased column gaps.
- Changed the progress track to a muted grey track and enlarged / vertically centered metric icons.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\xfv-b\"` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no scanner recognition algorithm, path-removal semantics, database schema, migration, database update, commit or push change.

Suggested commit message:

- `Phase 7.6d refine season scroll and scan progress`

### 7.6e - Scan Progress, History Logs And Safe Operations

Completed:

- Added `ScanLogCard` for the shared WebDAV / local scan history list. The card keeps the compact task-record structure and displays safe source, target alias, status, start / end time, scan counts, duration, sanitized error summary and reason summary.
- Added `LocalScanPathRemovePopover` for local scan-path row removal. The Popover is anchored beside the row action, supports explicit cancel / confirm, closes on outside interaction through popup behavior, handles Esc, and uses warning action styling.
- Kept the existing separated scan commands: `RunScanCommand` for WebDAV, `RunLocalScanCommand` for Local and `CancelScanCommand` for cancellation.
- Removed the progress-card `已识别` / `未识别` placeholder metrics because the current progress / result / log read models do not expose stable recognized / unrecognized counts.
- Changed the current-file progress row to single-line ellipsis with true-truncation tooltip behavior. Core progress reporting already reduces current items to file names.
- Removed WebDAV username display from scan history cards and added App-layer sanitization for history error summaries that replaces WebDAV URLs, credential-like key-value pairs and local paths.
- Recorded the 7.6e execution result in `docs/ui-redesign/PHASE_7_6_SETTINGS_SCAN_CACHE_PLAN.md`.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Explicit non-goals:

- no scan recognition accuracy, thresholds, AI recognition rules, TMDB / OMDb requests or OpenSubtitles flow changes;
- no pause-scan command, fake progress, background scheduler, filesystem watcher or automatic scan behavior;
- no Player, Detail, Library page structure or cache-cleanup behavior changes;
- no database schema, migration, database update, commit or push change.

Business logic changes:

- No Core code or data model changed in 7.6e.
- Existing `DeleteLocalScanPathAsync` semantics are retained: removing a Local scan path deletes the configuration and marks related Local `MediaFile` software records as `IsDeleted=true` for software visibility. It does not delete physical Local files or WebDAV files.
- App-layer UI behavior changed only by adding the local-remove confirmation Popover and safe scan-log text projection.

Non-stage page changes:

- None. Settings, Player, Library, Discovery, Detail and OnlineSubtitleSearch pages were not edited for 7.6e.
- Media-library visibility may reflect the existing Local-path removal semantics after a user confirms removal, but no media-library page or query code changed.

Suggested commit message:

- `Phase 7.6e scan progress logs and safe removal`

## Phase 7.4e - Discovery Regression Closeout

Completed:

- Closed out Phase 7.4 with a targeted read-only regression audit across Discovery search, ranking, embedded AI recommendations, Home entry routing, hidden Recommendation route compatibility, Movie-only recommendation boundaries and migration diff.
- Confirmed Home `发现更多影片` opens the Discovery AI recommendation tab through `MovieDiscoveryViewModel.OpenAiRecommendationsOnNextActivation()` and does not add a visible standalone AI recommendation navigation item.
- Confirmed TV person search is scoped to Discovery through TMDB `person/{id}/tv_credits`; TV search / ranking rows remain `DiscoveryTvSeriesCardViewModel` projections and are not converted into `AiRecommendationItem`.
- Confirmed recommendation seed, fingerprint and candidate inputs remain Movie / `UserMovieCollectionItems` based, with no TV Series / Season / Episode input path found in the audited recommendation service code.
- Confirmed Discovery layout memory remains an App-layer preference file and 7.4e added no schema or migration work.
- Recorded the user-confirmed later semantic update that TV search-card want-season tag copy displays `当前想看`; no code change is needed and 7.4e treats it as accepted.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Not done:

- No code, XAML, recommendation algorithm, recommendation prompt, TV recommendation, Watch Insights, scanner, player, schema, migration, database update, commit or push change.
- No full WPF screenshot / click-through validation was performed in this pass.

Suggested commit message:

- `Close out phase 7.4 discovery regression`

## Phase 7.0 - Complete

### 7.0a - Token / ResourceDictionary Baseline

- Established global visual token and ResourceDictionary baseline for the WPF app.
- Scope stayed in App UI resources and shared styling.
- Did not change media-library, scanning, playback or recommendation business logic.

Known validation:
- Build was expected for implementation-stage closeout.

Suggested commit message:
- `Phase 7.0a token resource baseline`

### 7.0b - Basic Controls And State Components Baseline

- Established reusable control and state styling baseline.
- Covered buttons, inputs, menus, cards, status surfaces and related visual primitives.
- Did not introduce a new UI framework.

Known validation:
- Build was expected for implementation-stage closeout.

Suggested commit message:
- `Phase 7.0b shared controls and states`

### 7.0c - MainWindow Custom Title Bar

- MainWindow moved toward a custom shell / title-bar baseline.
- Window behavior risk was tracked for DPI, resize, maximize and multi-monitor regression.
- Did not rewrite application navigation.

Known validation:
- Build and manual window smoke checks were expected.

Suggested commit message:
- `Phase 7.0c main window custom chrome`

### 7.0d - PlayerWindow / OnlineSubtitleSearchWindow Fixed-Dark Baseline

- Player-related windows received a fixed dark visual baseline.
- This was UI baseline work only; player business behavior and subtitle services remained out of scope.
- P61-01 was not resolved here.

Known validation:
- Build and player-window smoke checks were expected.

Suggested commit message:
- `Phase 7.0d player dark window baseline`

### 7.0e - Global Regression Closeout

- Closed Phase 7.0 with global static and build regression checks.
- Confirmed no DB / migration work was part of 7.0.

Suggested commit message:
- `Phase 7.0e global UI baseline closeout`

## Phase 7.1 - Complete

### 7.1a - Shell, Navigation, Brand, Account Menu, Theme Entry

- Reworked shell, navigation and brand presentation.
- Account menu uses local placeholder semantics; no real account system was introduced.
- Theme entry shows current theme.

Explicit non-goals:
- no account backend;
- no recommendation / player / scan business changes.

Suggested commit message:
- `Phase 7.1a shell navigation and account menu`

### 7.1b - HomePage Rebuild

- Rebuilt HomePage around current dashboard, recently watched / added and recommendation surfaces.
- Home may use minimal navigation state to open the AI recommendation tab.
- Statistics trend can reuse existing watch statistics / discovery data sources.

Explicit non-goals:
- no recommendation algorithm changes;
- no new database fields.

Suggested commit message:
- `Phase 7.1b home page rebuild`

### 7.1c - User Profile Dialog And Confirmation Dialog Variants

- User profile dialog remains a local placeholder / visual surface.
- Confirmation dialog variants support normal, warning and danger semantics.
- Dangerous wording keeps "移出媒体库", "删除记录" and "缓存清理" distinct from deleting media files or sources.

Suggested commit message:
- `Phase 7.1c profile and confirmation dialogs`

### 7.1d - Phase 7.1 Regression Closeout

- Closed 7.1 regression.
- Confirmed shell, home and dialog changes did not require DB / migration work.

Suggested commit message:
- `Phase 7.1d shell and home closeout`

## Phase 7.2 - Media Library

### 7.2 Plan / Audit - Complete

- Planned and audited media-library work before implementation.
- Page specs and Phase 6 coverage matrix were used as written specifications.
- Design drafts were visual references only.

Suggested commit message:
- `Phase 7.2 media library plan audit`

### 7.2a - Toolbar, Submitted Search, Filters, Sorting, Result Summary

- Added media-library toolbar structure.
- Preserved submitted search semantics: typing does not immediately filter; Enter / search icon submits.
- Added clear search, clear filters, filter combinations, sorting and result summary.
- Result summary includes "有播放源 / 无播放源" by confirmed product decision.

Explicit non-goals:
- no query-service business rewrite;
- no player or detail-page changes.

Suggested commit message:
- `Phase 7.2a media library toolbar filters and search`

### 7.2b - Poster / List Layout, Layout Memory, Media Item Visual Baseline

- Added poster / list layout switching.
- Added layout memory without database schema changes.
- Established media-item visual baseline for Movie, Series, Season and Other style rows/cards.
- Kept missing-poster placeholder as a resource hookup item for final-assets closeout.

Explicit non-goals:
- no DB field or migration for layout memory;
- no global visual polish beyond 7.2 scope.

Suggested commit message:
- `Phase 7.2b media library layout modes`

### 7.2c - Batch Entry, Batch Toolbar, Selection State, Special Item Hints

- Batch entry remains visible only when the sidebar is collapsed.
- Batch toolbar, selection state, select-all / clear-selection and special-item hints were aligned.
- Batch move-out and delete-record confirmations keep warning / danger semantics.

Explicit non-goals:
- no business semantic change to hide, restore or delete-record services;
- no fake full manual aggregation UI.

Suggested commit message:
- `Phase 7.2c media library batch mode`

### 7.2d - Removed Library Overlay

- Implemented removed-library in-page overlay.
- Added Movie / TV grouping, empty state and operation area.
- Restore, detail and delete-record actions use existing service semantics.
- User confirmed overlay should remain in-page rather than a drawer / side panel.

Explicit non-goals:
- no deletion of local or WebDAV files;
- no restore-as-rescan behavior.

Suggested commit message:
- `Phase 7.2d removed library overlay`

### 7.2e - Regression Audit Closeout

- Regression audit was executed across 7.2a-d.
- Some initial conclusions required correction according to user decisions:
  - "有播放源" in result summary is confirmed product wording, not a bug;
  - grouped placeholder currently passes user acceptance and is not a 7.2 blocker;
  - relative upstream diff may include authorized 7.0 / 7.1 / historical changes and must not be treated as 7.2 overreach by itself.

Explicit non-goals:
- audit did not fix code;
- audit did not introduce migrations.

Suggested commit message:
- `No commit recommended for audit`

### 7.2-final-assets - Current Closeout Item

- Assets directory contains `logo.svg`, `poster-placeholder.png` and `WatchPersonas`.
- Missing-poster placeholder and logo SVG are the final Phase 7.2 asset hookup scope.
- Current working tree contains resource-hookup work for the logo and shared poster placeholder; if not yet committed, treat it as pending final user review and manual commit.
- WPF has no built-in direct SVG rendering mechanism in the current project; the minimal approach is to use existing WPF resource / XAML vector mechanisms rather than add a large UI framework.

Explicit non-goals:
- no layout rewrite;
- no search / filter / sort changes;
- no player, detail, Core query, DB or migration changes.

Suggested commit message:
- `Hook up app logo and shared poster placeholder`

### 7.2-poster-card-texture - Reusable Texture Template

- HomePage poster cards reuse the media-library poster-card texture without changing card fields or data semantics.
- Reusable poster texture pattern:
  - fixed-size outer `Grid` instead of a clipping-only `Border`;
  - rounded `ClipMask` border plus `VisualBrush` opacity mask for image / overlay clipping;
  - palette shadow host with `PosterPaletteShadowBehavior` so the outer shadow can derive from the loaded poster;
  - secondary base shadow for stable depth when the poster is missing or not loaded;
  - dark surface brush under the placeholder / image;
  - shared `PosterPlaceholderTemplate` beneath the real poster image;
  - real poster image above placeholder with `RenderOptions.BitmapScalingMode="HighQuality"`, `SnapsToDevicePixels="True"` and decode width chosen by rendered poster size;
  - bottom dark depth gradient and top white highlight gradient inside the clipped content layer.
  - hover lift on the outer poster `Grid` with `TranslateTransform Y=-2`, matching the media-library poster-card interaction.
  - hover-lift posters must reserve at least 2-3px of top layout space or disable parent clipping; otherwise the lifted poster top edge can be clipped, especially in compact rows such as HomePage "最近新增".
- Decode width guidance from this pass:
  - small 82px-wide home posters use `DecodePixelWidth=180`;
  - 112px-wide recommendation posters use `DecodePixelWidth=240`;
  - 158px-wide continue-watching posters use `DecodePixelWidth=320`.

Explicit non-goals:
- no HomePage field, command, navigation or ViewModel changes;
- no media-library search / filter / sort changes;
- no player, detail, Core query, DB or migration changes.

Suggested commit message:
- `Polish home poster card texture`

### 7.2-poster-card-shadow-cache - Media Library Scroll Rendering

- Media-library poster cards keep the original glow / shadow texture parameters, but no longer attach per-item runtime `DropShadowEffect` instances in the virtualized list.
- `PosterCachedShadowBehavior` renders the original WPF `DropShadowEffect` parameters offscreen once and caches the resulting `ImageSource`.
- The palette glow still uses `PosterPaletteShadowBehavior`; when the target is an `Image`, it updates the cached shadow image color instead of cloning and replacing a live effect.
- The scroll-time lightweight texture mode was removed. Scrolling and idle states now use the same complete poster-card texture.
- Reusable cached-shadow pattern:
  - preserve original `BlurRadius`, `Direction`, `Opacity`, `ShadowDepth`, card size, corner radius and inner margin;
  - render the shadow with enough transparent padding so it is not clipped;
  - for 180x270 media-library posters, keep the cached shadow bitmap at least 84px padded on each side; smaller padding can expose rectangular glow edges during hover / scroll composition;
  - show the cached result as a behind-card `Image` with matching negative margins;
  - quantize palette glow colors only for cache reuse, not for base shadow;
  - do not reintroduce scroll-time visual toggles unless there is an explicit visual design for the state change.

Explicit non-goals:
- no media-library search / filter / sort or query semantic changes;
- no player, detail, Core query, DB or migration changes;
- HomePage still uses its existing poster texture implementation unless a later phase explicitly migrates it to cached shadows.

Suggested commit message:
- `Cache media library poster card shadows`

## Phase 7 Documentation Sync - Current

- Added Phase 7 dedicated maintenance docs:
  - `DesignDraft/PHASE_7_PLAN.md`
  - `DesignDraft/PHASE_7_STAGE_LOG.md`
  - `DesignDraft/PHASE_7_KNOWN_ISSUES.md`
- This is a docs-only phase.
- It should not modify business code, XAML, ViewModels, resource dictionaries, migrations or assets.

Validation:
- `git diff --check` should be run at phase end.
- Migrations diff should be empty.
- Build may be skipped if only Markdown changed.

Suggested commit message:
- `Sync Phase 7 maintenance docs`

## Current State Summary

- 7.0 complete.
- 7.1 complete.
- 7.2a-d complete.
- 7.2e audit executed.
- 7.2-final-assets and subsequent poster / Home / library polish follow-ups have been handled as post-closeout work.
- 7.3 Plan, 7.3a, 7.3b, 7.3c, 7.3d and 7.3e are complete.
- 7.6a shared Settings / Scan / Cache component baseline is complete.
- 7.6b settings general tab and cache management is complete.
- 7.6c settings API configuration tab is complete.
- 7.6d scan configuration and path pickers are complete.
- 7.6e scan run, progress, history logs and safe operations are complete.
- Recommended next 7.6 stage: 7.6f regression closeout and documentation sync.

## Maintenance Rule

At the end of every future Phase 7 substage, update:

- plan: scope and sequencing changes;
- stage log: what actually changed, validation, and explicit non-goals;
- Known Issues: blockers, deferred items, risks, noise and user-confirmed non-issues.

Codex must not require commit / push in these docs. User manages Git operations unless explicitly requested.

## Phase 7.3 - Details

### 7.3 Plan - Details Chain

Completed:

- Added `DesignDraft/PHASE_7_3_DETAILS_PLAN.md` as the dedicated 7.3 handoff plan.
- Confirmed 7.3 scope as Movie / Series / Season / Episode details, unified detail return, special-state detail experiences and unified correction-dialog alignment.
- Confirmed old design drafts are visual references only; current page specs and Phase 7 maintenance docs define the stage boundary.

Explicit non-goals:

- no player menu work;
- no P61-01 local cache menu removal;
- no scan / AI recognition threshold changes;
- no recommendation algorithm changes;
- no database schema, migration or database update.

Suggested commit message:

- `Plan Phase 7.3 detail pages`

### 7.3a - Shared Detail Foundation

Completed:

- Added a unified content-area icon back affordance for Movie, Series, Season and Episode detail pages.
- Added minimal App-layer in-memory detail origin tracking in `NavigationStateService`.
- `MainWindowViewModel` records accepted navigation requests before page activation finishes, so slow Home / Library activation cannot leave a stale detail origin on the stack.
- Page activation now displays the destination page first, then starts content loading on the Dispatcher background queue with a navigation-version / cancellation guard. This keeps the first detail back button responsive and prevents stale detail loads after a quick return.
- Detail fallback remains deterministic: Movie / Series to Library, Season to Series when possible, Episode to Season when possible.
- Existing Season / Episode text-return commands were preserved internally but now route through the shared detail-back behavior.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors before the running app locked the normal output exe.
- Follow-up compile checks used `dotnet build MediaLibrary.sln -p:UseAppHost=false`; the final check passed with 0 warnings and 0 errors while the desktop app was running.

Explicit non-goals:

- no detail page field/content rebuild yet;
- no source action semantic changes;
- no correction-flow service changes;
- no player, scan, recommendation, Core query, database schema, migration, database update, commit or push.

Suggested commit message:

- `Phase 7.3a shared detail navigation`

### 7.3b - Movie Detail

Completed:

- Rebuilt `MovieDetailPage` into the Phase 7 detail-page layout: poster hero, title/original title, status chips, overview, base metadata, action area, rating cards, tag cards and source list.
- Removed the old persistent detail `TabControl` presentation from Movie detail and kept subtitles out of the Movie detail surface.
- Moved Movie source correction from the old permanent correction tab into an in-page overlay entry. The overlay reuses the existing Movie / TV episode / unknown-season correction commands and services.
- Added lightweight Movie detail state helpers for ratings and source presence so empty rating and no-source states render without changing query or source semantics.
- Added a small rating-star converter for the Movie detail rating card display.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no Series / Season / Episode detail visual rebuild in this slice;
- no player menu, subtitle-management or P61-01 local cache work;
- no scan / AI recognition threshold changes;
- no source operation semantic changes;
- no Core service, recommendation algorithm, database schema, migration, database update, commit or push.

Suggested commit message:

- `Phase 7.3b movie detail baseline`

### 7.3c - Series And Season Details

Completed:

- Rebuilt `SeriesOverviewPage` into the Phase 7 detail-page layout: poster hero, unified back affordance, title/original title, status chips, overview, base metadata, source summary, collection action and Season list.
- Rebuilt `TvSeasonDetailPage` around poster hero, unified back affordance, season status chips, overview, metadata, season user-state actions, correction entry and Episode list.
- Kept recognized, unrecognized, metadata-only, no-source and grouped-placeholder detail states on the existing TV read models; no TV query semantics were changed.
- Kept Episode list actions wired to the existing detail, play and watched-state commands, including disabled play behavior for episodes without playable sources.
- Kept Season correction as an in-page overlay using existing commands and services, and converted visible Season-correction status/button text to Chinese.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors at closeout.

Explicit non-goals:

- no Episode detail visual rebuild in this slice;
- no player menu, subtitle-management or P61-01 local cache work;
- no scan / AI recognition threshold changes;
- no TV query service or correction service semantic changes;
- no delete-record / move-out detail actions;
- no Core service, recommendation algorithm, database schema, migration, database update, commit or push.

Suggested commit message:

- `Phase 7.3c series and season detail baseline`

### 7.3d - Episode Detail

Completed:

- Rebuilt `EpisodeDetailPage` into the Phase 7 detail-page layout: still / placeholder hero, unified back affordance, episode status chips, overview, single-episode metadata, primary actions, state summary and source list.
- Added Episode still-image binding through the existing poster-cache image behavior, while keeping the shared placeholder beneath missing still images. The detail still image uses an explicit 640px decode width so the 420px-wide hero image does not fall back to the shared 240px poster-cache default.
- Replaced the old persistent Episode detail tab / correction panel presentation with an in-page correction overlay entry.
- Preserved existing Episode commands for play, source play, set default source, manual probe, source split, watched state, refresh and correction.
- Kept unknown-season selection as a nested overlay inside the existing Episode correction flow.
- Removed temporary stage wording from the final Episode detail UI surface.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors at closeout.

Explicit non-goals:

- no shared cross-page correction-dialog shell beyond the Episode page overlay; that remains 7.3e;
- no subtitle-management, online subtitle, player menu or P61-01 local-cache work;
- no scan / AI recognition threshold changes;
- no source, TV query or correction service semantic changes;
- no delete-record / move-out detail actions;
- no Core service, database schema, migration, database update, commit or push.

Suggested commit message:

- `Phase 7.3d episode detail baseline`

### 7.3 Navigation Follow-up - Library First-load State

Completed:

- Fixed a 7.3a navigation timing regression where entering the media library from Home could show the real empty-state placeholder before the first library refresh completed.
- Added a media-library initial-loading gate so the first render shows a loading state until the first library query finishes.
- Kept the 7.3a behavior that switches pages before async content activation, so detail back responsiveness is not regressed.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.

Explicit non-goals:

- no media-library query, search, filter, sort, batch or detail-navigation semantic changes;
- no player, scan, Core service, database schema, migration, database update, commit or push.

Suggested commit message:

- `Fix library first-load placeholder during navigation`

### 7.2/7.3 Follow-up - Media Library Poster Shadow First-frame Stability

Completed:

- Fixed a media-library poster-card shadow / glow flicker that could appear after page switches or filter changes recreated poster card visuals.
- The cached shadow bitmap itself was already reused, but `PosterCachedShadowBehavior` delayed assigning it through a Dispatcher `Loaded` callback. Newly created cards could therefore render one frame with an empty shadow image before the cached shadow appeared.
- Changed cached-shadow application to assign synchronously once the target image is loaded, and to subscribe to the actual `Loaded` event for not-yet-loaded images. This keeps the existing full poster texture and cached shadow parameters unchanged.
- Stabilized palette glow color on recreated cards by caching computed shadow colors by poster source URL. When the same poster returns after filtering or navigation, the palette glow can be applied before the poster bitmap raises another delayed source-change pass.
- Avoided forcing palette glow back to the fallback color while the poster image source is temporarily null during async reload; the existing shadow color is preserved until a real poster bitmap or cached source color is available.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.

Explicit non-goals:

- no poster texture, shadow, glow, color, spacing, hover or cache-size parameter changes;
- no media-library query, search, filter, sort, batch or detail-navigation semantic changes;
- no player, scan, Core service, database schema, migration, database update, commit or push.

Suggested commit message:

- `Stabilize media library poster shadow first frame`

### 7.2-post-closeout follow-up - Poster Edges, Home AI, TV Coverage, Stable Library Sort

Completed:

- Media-library poster grid now reserves explicit virtualized viewport padding for top / left / bottom poster glow and the `Y=-2` hover lift. This keeps the existing cached glow / shadow texture and fixes edge clipping by giving the list viewport real paint space instead of changing the card texture.
- Home recent-poster cells reserve extra top / side layout room for the same hover-lift / shadow pattern.
- Home "recently added" now includes movie items and TV season items, including recognized and unrecognized seasons with active episode sources.
- Home AI recommendation preview now shows the recommendation status text from the AI recommendation view model instead of static scope copy, and exposes the same not-interested command beside the want-to-watch command.
- Media-library unidentified TV-like items now use season-style watched episode progress text instead of movie percent text.
- Batch mode no longer shows a duplicate "Other" hint when the batch hint would be identical to the existing category tag.
- Library visible-list "recently updated" ordering no longer uses Movie / TV Series / TV Season entity `UpdatedAt` directly. Old key: metadata entity `UpdatedAt`, which can be touched by scan identification, metadata refresh or AI tag refresh. New visible-list key: collection-state update, active media-source row update, and initial entity creation fallback. This preserves real source / user-state recency while avoiding pure metadata scan refresh from reshuffling unrelated cards.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no poster texture parameter change;
- no media-library search / filter / batch business semantic change;
- no scan confidence threshold change;
- no player, detail, database schema, migration, database update, commit or push.

Maintenance notes:

- Do not rewire visible media-library sort back to raw Movie / Series / Season `UpdatedAt` without first proving that scan metadata refresh cannot touch those fields.
- If poster edge clipping reappears, inspect viewport / ancestor clipping and layout padding first; do not blindly enlarge the cached shadow bitmap.

Suggested commit message:

- `Stabilize library sort and home TV previews`

### 7.2-post-closeout follow-up - Home Polish, Filter Multi-Select, Poster Paint Padding

Completed:

- Media-library poster grid paint padding was split from column-count calculation. Left / top / bottom padding can reserve shadow and hover-lift paint room without reducing the number of poster columns.
- Media-library poster grid horizontally centers by the real visible card width, not by the larger item slot width. This keeps the first card's left edge and the last card's right edge visually balanced.
- Media-library content type and decade filters support multi-select for non-`全部` options. `全部` remains a single-select close action; selecting every non-`全部` option resets back to `全部` and closes the menu.
- Collection status keeps multi-select, but does not collapse `喜爱` + `想看` + `不想看` back to `全部`, because those three user states are not equivalent to all collection states. `全部` still clears the other selections.
- Watch status returned to a normal single-select menu with `全部` / `已看` / `未看`; `不想看` belongs to collection status, not watch status.
- Home AI recommendation status is rendered as two controlled lines with expanded / collapsed sidebar widths, so the first line no longer truncates prematurely.
- Home AI preview buttons, rating badge, reason scrollbar, bottom discovery button, library metrics, recent-poster title alignment, section arrows and continue-watching progress bar received narrow scoped visual polish.
- Home library metrics, row height distribution, recent-poster safety spacing, section title gaps, AI recommendation vertical position and sidebar logo / collapse button alignment were tuned without changing dashboard data semantics.
- Home continue-watching / recently-added section arrows now use icon-only ghost buttons with hover background, and the shell sidebar toggle uses a local sidebar-panel glyph instead of text glyphs. No icon library was introduced for this narrow replacement.
- Added opt-in HomePage navigation / render diagnostics under `XFVERSE_POSTER_CACHE_DIAGNOSTICS=1` to separate HomeViewModel refresh time, HomePage first-render / visual-tree timing, live `DropShadowEffect` count, cached shadow image count, and palette-shadow target kind. This is diagnostic-only and does not change poster texture behavior.
- Global button focus styling no longer draws a click/focus border on ordinary mouse interaction.
- Sidebar collapse / expand button uses a more modern shell glyph and no longer shows hover text while expanded.

Validation:

- Temp-output App build passed with 0 warnings and 0 errors when the running desktop app locked the normal `bin` output.

Explicit non-goals:

- no media-library query-service business rewrite;
- no search submit semantics change;
- no poster texture parameter change;
- no player, detail, database schema, migration, database update, commit or push.

Maintenance notes:

- Poster shadow safety padding must not be subtracted from virtualized poster column-count width. Treat it as paint room, not item spacing.
- When poster slots include trailing spacing, center the row by visible card width if the design requirement is equal left / right card-edge distance.
- If only one side of poster glow is clipped, inspect the corresponding ancestor clipping / viewport padding before changing glow, cached shadow or card dimensions.
- Home AI status width should track the usable text column: expanded width must end before the refresh button, and collapsed width may grow only with the actual AI panel width.

Suggested commit message:

- `Polish home cards and media filters`

### 7.7g Follow-up - Poster Glow And Viewport Compensation Correction

Completed:

- Reverted the direct symmetric `VirtualizingWrapPanel` width/padding change because poster shadow paint room must not reduce or shift the virtualized column layout.
- Restored the Phase 7 viewport compensation strategy: Media Library and Discovery search ListBox viewports expand on clipped sides while panel padding preserves the original poster coordinates and column count.
- Extended the existing Discovery ranking `-84/+84` ScrollViewer compensation model to the right side with matching `-24/+24` viewport and inner-root offsets.
- Increased non-detail poster palette/base opacity without changing blur radius, bitmap padding, poster dimensions, or detail-page poster styling.
- Increased only the expanded-sidebar account avatar to 38px inside a fixed 32px layout column, preserving its right edge and the collapsed 32px state.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.
- `git diff -- src/MediaLibrary.App/Controls/VirtualizingWrapPanel.cs` returned empty after reverting the incorrect layout algorithm change.

Explicit non-goals:

- no detail-page poster glow change;
- no search, ranking, recommendation, media-library, profile, scan, database schema, migration, database update, commit or push behavior change.

### 7.7g Follow-up - Poster Top Gutter And Expanded Avatar Correction

Completed:

- Removed the negative top viewport expansion from Media Library and Discovery search poster lists. Their horizontal shadow-safe expansion remains, while the existing top `ViewportPadding` now provides real in-scroll paint room so scrolled card bodies cannot render above the content boundary.
- Added a real 36px top gutter inside both ranking ScrollViewers so the first hero poster glow is painted inside the viewport instead of being clipped at its top edge.
- Increased cached palette/base opacity again for every non-detail poster surface, including the movie-correction candidate poster. Detail-page poster opacity remains unchanged.
- Gave the expanded account avatar a real 38px layout column and translated only the visual 6px left. Its right edge and following text coordinates remain unchanged; the collapsed avatar and column remain 32px.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Explicit non-goals:

- no virtualized column-count algorithm change;
- no detail-page poster glow change;
- no search, ranking, recommendation, media-library, profile, correction, scan, database schema, migration, database update, commit or push behavior change.

### 7.7g Follow-up - Poster Glow Midpoint And First-row Spacing Restoration

Completed:

- Reduced the latest non-detail poster glow increase by half, using the exact midpoint between the previous accepted opacity and the latest stronger opacity.
- Restored Media Library and Discovery Movie/TV search `ViewportPadding.Top` from 44px to the original 10px, removing the added first-row whitespace.
- Kept the ListBox top boundary at its normal position, so vertical scrolling cannot paint poster bodies into the toolbar/content area.
- Kept horizontal poster paint-room compensation and the ranking hero top gutter unchanged.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Explicit non-goals:

- no detail-page poster glow change;
- no virtualized layout algorithm, search, ranking, recommendation, correction, database, migration, database update, commit or push behavior change.

### 7.7g Follow-up - Poster Scrollbar Alignment And Ranking Top Gap

Completed:

- Shifted only Media Library and Discovery Movie/TV poster-view scrollbars 34px left with `RenderTransform`, compensating for the horizontal shadow-safe viewport expansion without changing measured viewport width or poster columns.
- Changed the ranking scrollbar transform from 4px right to 20px left. This is the smaller correction appropriate for the ranking viewport's 24px right expansion.
- Reduced the ranking hero top content gutter from 36px to 18px while retaining space for the first poster glow.

Validation:

- The first temp-output build attempt failed because prior Codex temp build directories exhausted disk space. After removing only verified `%TEMP%\\XFVerseCodexBuild*` directories, the retry passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.
- `git diff -- src/MediaLibrary.App/Controls/VirtualizingWrapPanel.cs` remained empty.

Explicit non-goals:

- no poster column, card coordinate, shadow texture, search, ranking, recommendation, database, migration, database update, commit or push behavior change.

### 7.7g Follow-up - Correction Playback-source Shadow Scope

Completed:

- Added cached inline shadow/glow only around the playback-source selector inside the Season correction episode-mapping panel.
- Kept the shared playback-source ComboBox style free of live `Effect`, so Movie/Episode detail-page playback-source selectors and ordinary correction ComboBoxes are unchanged.
- Reused theme-specific cached inline shadow color, opacity, blur and depth tokens.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Explicit non-goals:

- no other ComboBox, correction field, detail-page playback-source selector, mapping behavior, database, migration, database update, commit or push change.

### 7.7g Follow-up - Correction Playback-source Shadow Paint Box

Completed:

- Corrected the playback-source selector cached shadow after runtime feedback showed the first wrapper remained visually clipped by the 48px mapping row.
- Kept the row height unchanged and reserved `10px` horizontal / `6px` vertical paint room inside the existing row.
- Inset the cached shadow card boundary to the actual ComboBox bounds and raised only this selector above its row siblings.
- Increased this selector's cached shadow opacity to `0.40`; no other dropdown was changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Explicit non-goals:

- no other ComboBox, popup template, detail page, mapping behavior, database, migration, database update, commit or push change.

### 7.7g Follow-up - Correction Playback-source Instance Correction

Root cause:

- The previous two passes targeted the Season correction episode-mapping source ComboBox, but the reported control is the playback-source selector in the Movie/Episode correction shell header.
- Cached shadow rendering itself was working; the wrong control instance meant the user-visible selector remained unchanged.

Completed:

- Fully reverted the cached-shadow wrapper from the Season episode-mapping source selector.
- Added cached inline shadow hosts only around the Movie and Episode correction HeaderContent selectors bound to `SelectedCorrectionSource`.
- Used `-12,-8` outer compensation with matching `12,8` inner paint space, preserving selector coordinates and size.
- Set local opacity to `0.40`; color, blur and depth remain theme-aware.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Explicit non-goals:

- no ordinary correction ComboBox, Season mapping selector, detail-page normal playback selector, data binding, correction behavior, database, migration, database update, commit or push change.

### 7.2-post-closeout follow-up - Home Navigation Stall Diagnosis

Completed:

- Reproduction diagnostics showed Home navigation stalls were dominated by repeated `HomeViewModel` library-overview refreshes on every normal reactivation, not by Home poster glow / shadow rendering.
- Home now reuses the already loaded dashboard on normal page reactivation and only reapplies active playback state. Data-change events still drive targeted refreshes for playback history, collection, recommendation, scan, metadata and library changes.
- No Home poster texture, glow, shadow, corner radius or cached-shadow strategy was changed in this pass.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migrations diff remained empty.
- `git diff --check` passed with line-ending warnings only.

Maintenance notes:

- If Home navigation lag returns, reintroduce a short-lived navigation / render timing probe before changing poster visuals; the previous temporary Home diagnostics were removed after the cache fix was verified.
- Use poster cached-shadow migration for Home only if visual diagnostics prove poster effects are the dominant cost.

### 7.2-post-closeout follow-up - Home Diagnostics Closed And Library Toolbar Polish

Completed:

- Home page navigation / render timing diagnostics were removed after the reactivation-cache fix solved the reported switch-to-home stall. The Home dashboard reactivation cache behavior remains in place.
- Media-library content-type filter now starts from the product default of Movie + TV selected instead of All; clearing filters returns to the same Movie + TV default.
- Media-library "clear filters" button now uses the same toolbar button sizing as the batch-selection entry and is right-aligned with that action column.
- Sidebar account avatar was reduced slightly so the collapsed navigation account button no longer clips the avatar.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migrations diff remained empty.
- `git diff --check` passed with line-ending warnings only.

### 7.3e - Unified Correction Dialog Shell

Completed:

- Added a shared `CorrectionDialogShell` control for detail-page correction overlays.
- Movie detail, Season detail and Episode detail now use the shared shell for the overlay mask, dialog card, title / summary header, close button, scroll container and footer action area.
- Existing Movie / Episode correction bodies continue to own target-type selection, TMDB search, AI assist, candidate cards, unknown-season selection and submit commands.
- Existing Season correction body continues to own recognized / unknown target selection, AI assist, TMDB search, episode-number mapping, status text and confirm command.
- Source split remains a separate warning-confirmation flow and was not merged into the correction dialog.
- No delete-record, move-out, subtitle, player-menu, local-cache or artificial-aggregation actions were added to ordinary detail pages.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false` passed with 0 warnings and 0 errors during implementation.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors at closeout.
- Migrations diff remained empty.
- `git diff --check` passed with line-ending warnings only.

Explicit non-goals:

- no correction ViewModel, candidate-search, AI-recognition, TMDB, source-split or correction-service semantic changes;
- no player, subtitle, P61-01 local-cache, scan, Core service, database schema, migration, database update, commit or push;
- no visual redesign beyond extracting the existing correction overlay chrome into the shared shell.

Maintenance notes:

- Future correction-dialog polish should first extend `CorrectionDialogShell` for shared chrome changes, while keeping page-specific correction bodies owned by their page ViewModels unless a dedicated correction-body refactor is planned.
- Do not move source split into the correction dialog; it is governed by `page-spec/global-dialogs.md` warning confirmation semantics.

Suggested commit message:

- `Phase 7.3e unified correction dialog shell`

### 7.3 Follow-up - Movie Detail Draft Alignment

Completed:

- Re-aligned `MovieDetailPage` with the movie-detail page-spec and the old full-screen draft structure while keeping current Phase 7 styling and product semantics.
- Moved the top detail area closer to the draft hierarchy: left poster column with the unified back button, center title / original title / status chips / ratings / scrollable overview, and right-side movie information plus tag cards.
- Moved the main action buttons into a dedicated action row above the play-source panel, matching the page-spec requirement that playback and state actions sit above sources.
- Restored the play-source panel to a full-width bottom section instead of sharing the lower row with ratings and tags.
- Added lightweight semantic glyphs to the main action buttons using the existing Segoe MDL2 Assets glyph set; no icon library was introduced.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Explicit non-goals:

- no Movie detail ViewModel, correction-service, source-service, player, subtitle, scan, Core service, database schema, migration, database update, commit or push changes;
- no fake director / writer / actor / production-company fields were added because the current `MovieDetailModel` does not expose those values;
- no change to correction dialog body behavior beyond preserving the existing 7.3e shared shell hookup.

Suggested commit message:

- `Align movie detail layout with draft`

### 7.3 Follow-up - Movie Detail Structured Layout Polish

Completed:

- Rebuilt the Movie detail top area as a two-column composition: the 2:3 poster remains on the left, while title, original title, release date, runtime, ratings, overview, crew and tags now share one rounded hero background on the right.
- Kept the poster left edge aligned with the unified back button and aligned the hero background top / bottom edges with the poster.
- Simplified the title metadata row to original title, `yy-MM-dd` release date and `hh-mm-ss` runtime.
- Reworked the TMDB / IMDb rating cards into equal-width cards with a source star, centered score / stars area and centered vote-count area.
- Applied the media-library-style hover-only modern scrollbar to the Movie overview and Movie play-source list.
- Added hover tooltips for truncated crew values and rendered each genre / emotion / scene tag as an individual rounded chip.
- Replaced the Movie play-source summary cards with a compact table-like list: fixed headers, centered cells, ellipsis + tooltip fallback, safe path subline, short probe state and per-source last-position / actual-duration display.
- Reduced each Movie play-source row to one compact separator-delimited line and kept the row actions in one line: play, default, split and probe.
- Changed the Movie detail primary action area to five evenly distributed buttons spanning from the poster left edge to the hero right edge.
- Restored a compact detail-route title bar for Movie, Series, Season and Episode detail pages. Detail routes hide the left shell title but keep the right-side theme, minimize, maximize and close buttons.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.
- Full-screen runtime screenshots were captured at `2704x1696` for Movie, Series, Season and Episode detail routes.

Explicit non-goals:

- no scanner, identification, player, subtitle, cache, database schema, migration, database update, commit or push changes;
- no change to Movie / Episode playback-record synchronization semantics;
- no change to Series / Season / Episode field semantics beyond restoring the shared compact detail title bar.

Suggested commit message:

- `Polish movie detail layout and detail title bar`

### 7.3 Follow-up - Movie Detail Feedback Closeout

Completed:

- Kept the Movie poster at 2:3 and moved the shared back action into the compact detail title bar for Movie, Series, Season and Episode routes.
- Slightly increased compact detail title-bar height while preserving caption-button sizes. Removed title-bar hover text from theme, back, minimize, maximize and close actions.
- Changed Movie release metadata to icon-led `yyyy-MM-dd` and `hh:mm:ss` values.
- Kept TMDB and IMDb as two fixed rating cards even when a source has no rating. Missing values render as unknown with empty stars.
- Refined each rating card with a centered source header, a left-side star badge, smaller score / vote typography, a score-vote divider and comma-separated vote counts.
- Removed the inner border around the Movie overview and widened the right-side crew / tag column.
- Changed Movie primary actions from equal-width slots to content-sized buttons with more vertical separation from adjacent sections.
- Moved Movie play-source headers below the section title, removed the video-bitrate column, widened filename and recent-played columns, and kept compact separator-delimited rows.
- Play-source format now omits the leading dot. Resolution cells render only a short `p` / `K` label while retaining the raw dimensions as the resolution tooltip.
- Reused `TrimmedTextToolTipBehavior` so Movie crew and ordinary play-source values expose tooltips only when text is actually truncated.
- Narrowed source-row play, split and probe actions while reserving more width for default-source state.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.
- Full-screen runtime screenshots were captured at `2704x1696` for Movie, Series, Season and Episode detail routes after the title-bar migration.

Explicit non-goals:

- no scanner, identification, player, subtitle, cache, database schema, migration, database update, commit or push changes;
- no semantic changes to Movie / Episode playback records or default-source selection;
- no broad Series / Season / Episode visual redesign beyond the shared detail title-bar navigation move.

Suggested commit message:

- `Refine movie detail data layout and title bar navigation`

### 7.3 Follow-up - Detail State Semantics And Episode Layout Closeout

Completed:

- Clarified the product terminology for detail pages: the visible distinction is with-play-source detail versus without-play-source detail. It is not an in-library versus out-of-library distinction. Internal persistence carriers must not leak into UI wording.
- Reduced Movie detail availability tags to source presence only: with source, no source or unidentified / needs correction. Collection membership remains an independent state.
- Matched the compact detail title-bar back-button inset to the close-button inset while preserving the shared title-bar behavior on Movie, Series, Season and Episode routes.
- Narrowed the fixed TMDB / IMDb rating cards, kept equal outer and inner spacing, vertically centered each source title with its star badge and increased the score-vote divider weight.
- Kept both rating cards visible when values are unavailable. Vote counts use comma grouping.
- Changed the Movie action row to content-sized, evenly spaced buttons spanning from the poster left edge to the hero-card right edge.
- Unified the Movie preference action: unwatched records render want / cancel-want, watched records render favorite / cancel-favorite. Watched, want, favorite and not-interested changes retain the documented mutual-exclusion behavior.
- Updated Movie state glyphs: watched uses check / cross state, favorite uses outline / filled heart state and not-interested uses an exclamation mark.
- Renamed the Movie and Episode source header to `filename / file path`.
- Reworked Episode detail against the Movie detail hierarchy while retaining episode-specific fields: 16:9 still image, compact hero information, separate action row and compact source table.
- Added Episode source short-resolution display, raw-resolution tooltip, short probe state, per-source position / duration text, modern scrollbars, truncated-text tooltips and compact source-row actions.
- Removed the Episode hero-content overlap found during runtime automation verification by constraining the hero to title, pills, overview and a compact episode-information column.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors after the Episode layout correction.
- `git diff --check` passed with line-ending warnings only.
- Migrations diff remained empty.
- UIAutomation verified Movie compact-title-bar insets, equal Movie action-row spacing, preference-state transitions and Episode content boundaries.
- UIAutomation verified the Episode overview ended before the standalone action row and the compact source table remained below that row.

Explicit non-goals:

- no scanner, identification, player, subtitle, cache, database schema, migration, database update, commit or push changes;
- no automatic mapping of internal persistence-carrier differences to visible UI terminology;
- no schema expansion for favorite persistence on recommendation-only records without a stable Movie carrier.

Known issue:

- A recommendation-only record without a stable Movie carrier can persist want / cancel-want through the existing collection service, but cannot persist favorite after watched without a separate product and schema decision. The UI must not fake that state.

Suggested commit message:

- `Close out detail state semantics and episode layout`

### 7.3 Follow-up - Detail Shell Backdrop And Interaction Polish

Completed:

- Moved the detail ambient treatment from page-local rounded containers to the shared detail-route shell. Movie, Series, Season and Episode details now share one full-window backdrop that covers the compact title bar and content area.
- Added a cached multi-color poster backdrop pipeline. It samples the current poster or episode still, selects a restrained primary color plus distinct secondary and accent colors, and renders one small static fluid-style bitmap for reuse.
- Kept the liquid-glass treatment static: the cached bitmap includes soft flow fields and subtle glass highlights, while the compact detail title bar uses a translucent overlay. No frame-by-frame blur, animation or live gradient calculation was added.
- Removed the page-local ambient rounded backgrounds so the effect no longer reads as another content card.
- Localized Movie country / region and language display values to Chinese without changing stored metadata.
- Replaced the Movie not-interested glyph with a Segoe MDL2 exclamation symbol and retained the compact rectangular title / tag chips.
- Added auto-reveal modern scrollbars for Movie and Episode overview and source lists: scrollbars stay quiet while idle and reveal during scrolling.
- Kept the Movie action row aligned to the source panel boundaries and preserved equal horizontal spacing.
- Moved Movie detail state persistence off the UI dispatcher after optimistic state updates. Watched, want-to-watch, favorite and not-interested buttons now provide visible feedback before SQLite persistence finishes, while failures still roll the state back.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.
- Migrations diff remained empty.
- UIAutomation verified Movie country / region and language display as Chinese, action-row boundary alignment and return-to-home / reopen-detail routing.
- UIAutomation and a 100 ms post-click capture verified immediate visible feedback for watched, want-to-watch, favorite and not-interested state changes.
- A full-screen `2704x1696` runtime screenshot verified that the cached detail backdrop covers the title bar and detail content shell rather than creating a page-local rounded background.

Explicit non-goals:

- no live blur, animated background, real-time poster color extraction loop or frame-by-frame gradient calculation;
- no scanner, identification, player, subtitle, cache schema, database schema, migration, database update, commit or push changes;
- no broad final UI redesign beyond the detail-route shell and requested Movie / Episode polish.

Suggested commit message:

- `Polish detail shell backdrop and state interactions`

### 7.3 Follow-up - Detail State Cursor Latency Fix And Shared Glass Baseline

Completed:

- Fixed the Movie detail state-button cursor lag after optimistic UI updates. Watched, want-to-watch, favorite and not-interested buttons no longer enter the disabled visual state while their background SQLite persistence is still running.
- Preserved write safety with a shared in-method persistence guard. Repeated or cross-state clicks during an active state save return immediately instead of issuing concurrent writes.
- Extended `AsyncRelayCommand` with an opt-in `disableWhileExecuting` switch. Existing commands keep the original disable-while-running behavior by default; only the Movie detail state actions opt out.
- Added one shared liquid-glass ResourceDictionary baseline with restrained Light / Dark gradients, low-contrast borders, selected-state surfaces and background-bearing button variants.
- Applied that shared baseline now to Home, Library and the existing Movie, Series, Season and Episode detail pages.
- Documented the extension rule for later page phases: settings, scan, history, favorites, insights, navigation, popups, dialogs, tags and equivalent rebuilt components must reuse the same shared resources and receive a final Phase 7.8 consistency audit.

Explicit non-goals:

- no broad restyling of pages outside Home, Library and current detail routes;
- no live blur, animated glass textures or frame-by-frame visual calculations;
- no database schema, migration, database update, commit or push changes.

Suggested commit message:

- `Add shared glass baseline and fix detail state cursor latency`

### 7.3 Follow-up - TV Detail Alignment And Player Polish

Completed:

- Confirmed that the current liquid-glass palette is an implementation baseline. Final global color balancing remains a Phase 7.8 task.
- Refined the Movie rating cards: star badge and source title align vertically, score body spacing is slightly lower, the score / vote divider is stronger and hover contrast is clearer.
- Changed PlayerWindow startup to reuse the existing fullscreen transition path immediately after load. Player caption buttons now match the software title-bar button dimensions.
- Rebuilt `SeriesOverviewPage` against the Movie detail hierarchy: 2:3 poster, aligned hero card, single-line title / original title, date and season-source summary, fixed TMDB / IMDb cards, scrollable overview and one `电视剧信息` card.
- Added read-only Series rating lookup through the existing TMDB / OMDb clients. Missing rating values remain visible as unknown values with empty stars.
- Replaced Series Season cards with compact separator-delimited rows. Each row uses a 2:3 poster, title, overview, date, episode count, progress, source state and detail action.
- Rebuilt `TvSeasonDetailPage` around a 2:3 poster, single-line title / original title, date / Episode summary, scrollable overview, compact TMDB rating and one `单季信息` card.
- Moved Season state actions below the hero card and replaced Episode cards with compact separator-delimited rows. Episode rows retain detail and play actions; state mutation remains on the Season-level action row or Episode detail.
- Reworked `EpisodeDetailPage` with a larger 16:9 still, narrower information card, single-line title, date / runtime metadata, centered compact TMDB rating and a `单集信息` card.
- Kept Episode play-source rows aligned with the Movie source-table structure and removed the redundant per-source correction action. The page-level correction action remains available.
- Added read-only TMDB Season and Episode rating lookup using the existing cached TMDB service. No schema or migration change was introduced.
- Fixed first-open detail interaction latency at the root cause: poster backdrop and poster-shadow cache misses now render off the UI dispatcher, and inactive Home / Recommendation pages no longer refresh hidden data after detail-state persistence.

Validation:

- `dotnet build MediaLibrary.sln` passed repeatedly with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.
- Migrations diff remained empty.
- Runtime screenshots were intentionally minimized after the Movie detail page became the stable comparison baseline. Final visual acceptance remains manual.

Deferred:

- Series production status, network and production-company values are not stored by the current TV entities. The UI displays unavailable fallback text rather than fabricated data.
- Final Light / Dark palette tuning, cross-page color balancing and broad liquid-glass consistency audit remain Phase 7.8 work.

Explicit non-goals:

- no database schema, migration, database update, commit or push changes;
- no scanner, identification, subtitle, recommendation algorithm or cache-management semantic changes;
- no live blur, animated gradient or frame-by-frame poster-color extraction;
- no broad player-menu redesign beyond startup fullscreen and caption-button sizing.

Suggested commit message:

- `Align TV detail pages and polish player detail UX`

### 7.3 Follow-up - TV Detail Exact Layout Closeout

Completed:

- Kept the accepted Movie detail page as the stable comparison baseline. Runtime screenshots are no longer repeated when the Movie layout has not changed; new captures are reserved for visual blockers or explicit verification needs.
- Replaced the Series Season summary rows with the final five-line structure: special / season number, TMDB season name, first-air date, source-bearing episode count and watched summary. Rows keep 2:3 artwork, separator lines, compact detail actions and modern auto-reveal scrolling.
- Reordered the Series `电视剧信息` card to the final UI hierarchy: production status, production company, broadcast platform, country / region, language, genre, total seasons and total episodes. Missing TV entity fields remain explicit unavailable values.
- Shortened the Season hero card while keeping the 2:3 poster height. The reserved lower band now contains four evenly distributed Movie-style actions: preference, watched state, not-interested state and season ownership correction.
- Replaced the Season rating presentation with one short horizontal TMDB card: star / TMDB column, score / stars column and vote-count column separated by stronger dividers.
- Rebuilt Season Episode rows with a larger episode-number gap, title, two-line overview and fixed-width air-date, source-count, `hh:mm:ss` runtime and last-played fields. Overflow uses trimmed-text tooltips where applicable.
- Extended the Episode still image to a true 16:9 `520 x 292.5` visual while retaining a shorter `236`-high information card to its right. The lower reserved band now contains the three evenly distributed actions: play default source, watched state and recognition correction.
- Reused the short horizontal TMDB rating card inside Episode detail, removed the redundant runtime item from the title metadata line and aligned the `单集信息` card with the Season information hierarchy minus aggregate episode counts.
- Kept Episode playback-source rows structurally aligned with Movie source rows. Per-source correction stays removed; page-level correction remains the single entry.
- Added only read-only TV presentation fields and cached rating lookups. No scanner, identification, persistence-schema or migration semantics changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.
- Migrations diff remained empty.
- Static XAML review confirmed the four detail information titles: `电影信息`, `电视剧信息`, `单季信息`, `单集信息`.
- Runtime screenshots were intentionally not repeated because the accepted Movie detail page remains the comparison baseline and the user requested minimized captures.

Explicit non-goals:

- no live blur, animated gradient or frame-by-frame poster-color extraction;
- no scanner, identification, subtitle, cache-management or recommendation-algorithm changes;
- no database schema, migration, database update, commit or push changes.

Suggested commit message:

- `Close out TV detail layouts against movie baseline`

### 7.3 Follow-up - Persisted TV Production Metadata And Library Crew Rows

Completed:

- Added user-approved persisted `TvSeries` metadata for TMDB production status, broadcast platforms / networks and production companies.
- Added `AddTvSeriesProductionMetadata`; it only adds three nullable `TvSeries` columns and updates the EF snapshot. No database update was executed.
- Extended TMDB TV Series detail parsing and the existing hydration, scan-identification and unknown-Season correction upsert paths. Placeholder reset paths clear the new values instead of retaining stale identified metadata.
- Versioned the persistent TMDB TV Series detail cache key to `v2`, so an old cached JSON payload without the new fields cannot suppress the first refresh after upgrade.
- Updated summary-hydration completeness checks so existing `TvSeries` rows missing the new values are refreshed when the current TMDB payload has them.
- Replaced Series / Season / Episode detail placeholders with real Series-level production metadata. Season and Episode pages read the owning Series values instead of duplicating them in Season or Episode storage.
- Updated media-library Movie list rows to read persisted Movie director and actor values. The list title line now separates title from a creator region; inside that region director receives 20% and actors receive 80% of the available width.
- Director and actor list text uses ellipsis trimming and the existing trimmed-text tooltip behavior. A tooltip appears only when the rendered text is actually truncated.

Not done:

- No TV rating persistence, TV creator / showrunner persistence, new TV detail card, scanner rule change, database update, commit or push was added.
- No Movie schema change was needed for media-library crew rows because `Movie.DirectorText` and `Movie.ActorsText` already existed.

Validation:

- Pre-migration `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Final `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.
- Migration audit confirmed that `AddTvSeriesProductionMetadata` only adds `NetworksText`, `ProductionCompaniesText` and `ProductionStatus` to `TvSeries`.

Suggested commit message:

- `Persist TV production metadata and show library crew`

### 7.3 Follow-up - Detail Hierarchy, TV State And Layered Glass Closeout

Completed:

- Strengthened the shared liquid-glass baseline for Light and Dark themes with distinct nested-card opacity, edge highlights and bounded static shadows. The blur-like appearance remains resource-driven and cached; no live backdrop blur was added.
- Made unidentified Movie / Series / Season / Episode details derive their static page backdrop palette from the shared placeholder poster when no real artwork exists. Added larger placeholder labels for detail posters and wide Episode artwork.
- Corrected Home `片库预览` trend text to `较上月`, kept signs aligned with the actual delta and separated the larger arrow glyph from the value.
- Aligned rating bodies vertically, split `/` and `10` for baseline alignment and added the sixth Movie action for `加入媒体库 / 移出媒体库`. No-source Movies retain the disabled correction action.
- Added bounded background Movie-crew hydration for historical library rows missing director / actor metadata. Existing persisted Movie columns are reused; no schema change was introduced.
- Extended TV-like filename detection from two-digit to four-digit `SxxxxExxxx` compatibility without changing parser priority.
- Updated Series Season rows to suppress redundant season names, use compact poster depth and avoid focus-driven scroll jumps from the detail button.
- Updated Season detail so the TMDB card sits above the overview, the right information card scrolls, the action row includes season library visibility, Episode numbers expand for four digits and Episode rows include watched state plus an in-row watched toggle.
- Fixed manual Season / Episode watched mutation at the service boundary: it no longer writes playback timestamps or progress. Season Episode-row toggles refresh locally instead of clearing and rebuilding the list.
- Updated Episode detail with a larger 16:9 still, `SxxExx + meaningful title`, series / original-name row, one-line TMDB source header, scrollable production information and the `播放默认源 / 单集识别修正` action wording.
- Changed series and season library actions to `加入媒体库 / 移出媒体库` toggles using existing hide-only semantics.

Validation:

- `dotnet build MediaLibrary.sln` passed during implementation with 0 warnings and 0 errors.
- Runtime screenshots were intentionally skipped because the user requested manual visual acceptance and minimized captures.

Explicit non-goals:

- no live blur, animation or frame-by-frame visual calculation;
- no database update, new migration, commit or push;
- no player-menu redesign, subtitle change or final cross-page UI rebuild.

Suggested commit message:

- `Polish detail hierarchy and TV state semantics`

### 7.3 Follow-up - Media Library Tag Scope And Search Polish

Completed:

- Reworked the media-library tag menu into `全部 / 电视剧 / 电影`. `全部` clears tag filtering; TV opens its own TMDB genre table; Movie opens the existing type / emotion / scene groups.
- Added a balanced TV-tag grid layout derived from the actual TV genre count. Tag cells are centered and use visible hover / selected backgrounds.
- Reused the TMDB TV genre mapping for persisted-name compatibility, including `Sci-Fi & Fantasy -> 科幻奇幻`. Existing media-library rows normalize on load; newly fetched TV details store normalized labels.
- Added the media-library search placeholder `需要搜索的影视作品名/导演/演员`.
- Extended submitted media-library search matching from title / original title to persisted director and actor fields.

Not done:

- No schema change, database update, commit or push was added.
- No fuzzy search, pinyin search or TV cast persistence was added.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Suggested commit message:

- `Polish media library tag filters and search`

### 7.3 Follow-up - Movie Correction Dialog Alignment

Completed:

- Extended the shared `CorrectionDialogShell` with an optional title-adjacent header-content slot. Existing Season / Episode shells keep their page-owned body content; Movie uses the slot for the selected playback source.
- Rebuilt the Movie correction body as `MovieCorrectionDialogContent`: larger title chrome, file-icon playback-source selector, one rounded correction card, inline status row, clearable placeholder inputs and three target modes.
- Movie correction results now use compact separator rows with 2:3 poster, internally scrollable overview, compact metadata and `修正为TA / 已为该影片`.
- TV Episode correction results now use expandable Series / Season rows with right-edge arrows and direct `修正到剧 / 修正到季` actions. The direct actions compose the existing target-selection and existing correction-apply commands; correction service semantics are unchanged.
- Existing unknown-Season targets now render inline in the same dialog. Selecting a Season and applying the Episode number no longer requires the second picker overlay.
- Added playback-source switching inside the Movie correction dialog. Opening defaults to the default source when available and switching sources resets candidate state through the existing source-correction entry path.

Known boundary:

- TMDB TV correction candidates do not currently hydrate TV credits, so the TV director field displays `-` rather than fabricated data.
- The old Movie correction markup remains commented as a short-lived manual comparison reference and should be deleted after acceptance.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- No schema, migration, database update, commit or push change was introduced by this follow-up.

Suggested commit message:

- `Polish movie correction dialog layout`

### 7.3 Follow-up - Detail Regression Fixes And Persisted TV Crew

Completed:

- Brightened the cached multi-color detail backdrop and kept the shared liquid-glass treatment static. No live blur, animated gradient or frame-by-frame palette extraction was added.
- Versioned poster backdrop and shadow requests. Switching from a recognized detail to an unidentified detail now immediately restores placeholder-poster colors and ignores stale async palette results from the previous poster.
- Moved large and wide `无海报` detail labels upward while keeping horizontal centering.
- Unified the Player top-right maximize action with the existing fullscreen route used by the fullscreen button and double click.
- Delayed Movie / Episode parent `LastPlayedAt` writes until playback progress is actually saved. Opening and closing before progress persistence no longer creates a transient detail-page recent-play value without history.
- Corrected Movie preference and media-library visibility icons, unidentified Movie tag fallbacks, media-library tag submenu right-side placement and the library search placeholder.
- Reduced Series Season-list wheel jump by using pixel scrolling, restored Season-row poster depth and aligned Series / Season / Episode detail field groups against the Movie-detail baseline.
- Fixed Season and Episode-row watched-state local refresh so button text and icons update immediately while persistence runs asynchronously.
- Added user-approved `TvSeries.DirectorText`, `WriterText` and `ActorsText` persistence with `AddTvSeriesCrewMetadata`.
- Extended TMDB TV detail requests with `append_to_response=credits`, merged `created_by` and writing crew for writers, persisted the owning-Series crew fields and bumped the TV detail cache schema to `v3`.
- Projected real TV director / actor values into media-library Series and Season list rows. Existing Movie rows continue to use persisted Movie director / actor values. The list layout now gives director more width and actors less width than the previous 20 / 80 split.

Not done:

- No database update, commit or push was executed.
- No Season- or Episode-level duplicate crew columns were added.
- TMDB TV correction-search candidate rows still do not fetch per-result credits; stored Series details are hydrated when entering the recognized detail chain.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors before migration generation.
- `AddTvSeriesCrewMetadata` only adds nullable `ActorsText`, `DirectorText` and `WriterText` columns to `TvSeries`.
- `git diff --check` passed with line-ending warnings only.

Suggested commit message:

- `Polish detail regressions and persist TV crew metadata`

### 7.3 Follow-up - Movie Correction Dialog Interaction Closeout

Completed:

- Replaced the movie-correction playback-source font glyph with a vector file icon, so the selector does not depend on a missing symbol font glyph.
- Aligned the shared correction-dialog close button to the title top edge and increased the close icon size.
- Applied compact media-library-style input and dropdown sizing to the movie-correction playback-source selector, target selector and query fields.
- Updated movie and TV query placeholders to `请输入需要搜索的影片名` and `请输入需要搜索的电视剧名`.
- Mirrored the TMDB search icon horizontally, vertically aligned toolbar icons and replaced the AI lightning glyph with a four-point star.
- Added one correction busy gate for TMDB and AI-assisted searches. The result area shows a centered spinner while requests are running, TMDB and AI actions cannot run concurrently, and AI requests publish a waiting status before the response arrives.
- Added bounded candidate-detail enrichment for correction results: Movie loads at most 10 details, TV loads at most 12 details, and both use a concurrency limit of 4. A single detail failure falls back to the search-row projection instead of clearing the whole result list.
- TV correction candidates now show hydrated director data when available, normalized TV genre text, localized country / region and language text.
- TV and unknown-Season candidate lists now use pixel scrolling, `特别篇` for Season 0 and the same season-count glyph used by the TV detail pages.
- Changed Movie-dialog TV result actions from immediate apply to target selection. `确认修正 / 取消` is centered at the bottom only in TV Episode mode.
- Removed the initial `已选择播放源...` status copy while keeping real request, selection, success and failure status rows.
- Removed the result-list bottom border so separators only belong to result rows.

Not done:

- No correction-service write semantics, scan rules, schema, migration, database update, commit or push changed in this follow-up.
- No runtime screenshot was taken; visual acceptance remains manual as requested.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Suggested commit message:

- `Close out movie correction dialog interactions`

### 7.3 Follow-up - Movie Detail Runtime Load Regression Fix

Completed:

- Fixed the Movie-detail runtime XAML crash caused by `CorrectionDialogComboBoxStyle` referencing `ComboBoxStyle` before the later `Inputs.xaml` dictionary had loaded. The correction dropdown now reuses the same `FormComboBoxStyle` base as the media-library compact dropdown.
- Fixed the Movie-correction candidate overview `Run.Text` binding by making the read-only `OverviewDisplayText` projection explicitly `OneWay`.
- Added a configurable shared-shell footer gap and set the Movie-correction shell gap to `0`, so a Movie dialog without footer actions keeps the correction card bottom inset aligned with the title top inset.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- A temporary STA runtime probe loaded and laid out `MovieDetailPage` successfully.
- The same runtime probe loaded and laid out `MovieCorrectionDialogContent` with a Movie candidate successfully, covering the read-only overview binding template.

Not done:

- No correction-service write semantics, scan rules, schema, migration, database update, commit or push changed in this follow-up.

Suggested commit message:

- `Fix movie detail correction runtime load`

## Phase 7.4 - Discovery Plan / Audit

Completed:

- Read the current Phase 7 plan, Phase 7 known issues, movie-discovery product docs, page specs, relevant Discovery / Recommendation / Home / Media Library XAML and ViewModel code, shared UI rule docs, and the referenced Discovery / Media Library screenshots.
- Added the single Phase 7.4 execution plan at `docs/movie-discovery/PHASE_7_4_DISCOVERY_PLAN.md`.
- Split 7.4 into search toolbar/modes/filters/layout state, search result cards, ranking tab, AI recommendation tab/preference dialog, and Discovery regression closeout.
- Captured the user-confirmed search requirements: media-library-like search/filter UI, no search-card progress bar, top-left Movie/TV tag, Movie top-right `+ 想看`, TV `想看` tag only when at least one season is want-to-watch, rating badge treatment, Discovery-specific result copy, no `已移出媒体库` button, TV search-method option, dynamic placeholders, and list-row upper-right state/action placement.
- Added per-substage required-reading lists, reusable component references, similar-page references, UI-rule references, output requirements, acceptance criteria, business-logic accounting and non-stage-page accounting.
- Linked the detailed plan from `DesignDraft/PHASE_7_PLAN.md`.

Not done:

- No implementation code, UI resources, business services, database schema, migration, database update, commit or push was changed in this planning pass.
- No build was run because the change is documentation-only.

Validation:

- Documentation scope only. Implementation validation is deferred to the relevant 7.4 substages and the 7.4e closeout.

Suggested commit message:

- `Plan Phase 7.4 discovery work`

### 7.4a - Search Toolbar, Modes, Filters And Layout State

Completed:

- Replaced the Discovery search layout placeholder command with a real poster / list layout toggle.
- Added Discovery-specific file-backed layout memory through `IDiscoveryPreferencesService` and `discovery-preferences.json`; this does not share `library-preferences.json`.
- Changed the search-method selector to stay visible for both Movie and TV search.
- Changed the search-method first option to the media-neutral `按片名搜`.
- Added dynamic search placeholder and tooltip text:
  - Movie title search: `输入需要搜索的电影`
  - TV title search: `输入需要搜索的电视剧名`
  - Person search: `输入需要搜索的导演/演员`
- Added TV person search for Discovery only through TMDB person search plus `person/{id}/tv_credits`, projecting results into `DiscoveryTvSeriesCardViewModel`.
- Updated Discovery search loading, empty, loaded and clear-filter status copy to follow the active media type and search method.
- Added functional baseline list containers for Movie and TV search results. Full search UI visual alignment remains 7.4b, including toolbar/filter styling, result summary layout, poster/list containers and cards.

Acceptance:

- Movie / TV media switching remains available.
- Movie and TV both expose title/person search methods.
- The three required placeholder strings are bound through `TextBoxPlaceholderBehavior`.
- Search filters continue to use the existing Discovery filter controls and field sets.
- Layout toggle switches between poster and list containers and persists through an App-layer preference file without database fields.
- TV person search produces TV Series discovery rows only; it does not create Movie recommendation candidates or Watch Insights inputs.
- Loading / empty / error status text remains page-local and does not show multiple competing status layers.

Business logic changes:

- Scoped Discovery search change: `ITmdbService.SearchTvSeriesByPersonAsync` and `GetPersonTvCreditsAsync` were added and are used only by `MovieDiscoveryViewModel` when TV + person search is selected.
- UI preference change: Discovery search layout is persisted in `discovery-preferences.json`.
- No recommendation algorithm, prompt, candidate generation, profile/persona/fingerprint, Watch Insights, scan, correction, player, schema, migration or database update behavior changed.

Non-stage page changes:

- No Home, Media Library, Recommendation, Detail, Player or Settings pages were edited.
- Shared DI registration was updated for the new Discovery preference service.
- Core TMDB service/interface gained the TV person credits method used by Discovery search.

Not done:

- At 7.4a closeout, the full search UI visual pass was deferred to 7.4b. 7.4b later completed the structural toolbar / filter / result-card pass, while exact media-library visual parity remains Deferred by current user instruction.
- Ranking tab and AI recommendation tab were not changed.
- No database update, commit or push was executed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Suggested commit message:

- `Implement discovery search layout and TV person search`

### 7.4b - Search UI Visual Alignment

Completed:

- Replaced the Discovery search toolbar baseline with a media-library-like compact button / ContextMenu structure while keeping Discovery-specific Movie / TV field sets.
- Kept search result status and summary copy on the Discovery-specific active search messages.
- Reworked search result containers, poster cards and list rows for Movie and TV.
- Movie search cards now have no progress bar, show a top-left `电影` tag, a top-right want-state action, rating badge treatment and conditional add-to-library action.
- TV search cards now have no progress bar, show a top-left `电视剧` tag, and show a `想看` tag only when at least one season is want-to-watch. They do not expose a TV `+ 想看` action.
- Movie list rows keep the right-bottom `电影` tag and use the upper-right slot for the Movie want-state action.
- TV list rows keep the right-bottom `电视剧` tag and use the upper-right slot only for the conditional `想看` season-state tag.
- Added page-local search UI styles and a page-local ContextMenu opener. No shared Home, Recommendation or Media Library resource was edited.

Acceptance:

- Search toolbar / filter controls follow the media-library button/menu structure without changing Discovery filter semantics.
- Poster search cards have no progress bar and no `已移出媒体库` action.
- Movie search cards expose want-state actions in poster and list layouts.
- TV search cards do not expose a `+ 想看` action; `想看` is only a conditional label for existing want-to-watch seasons.
- Rating badges are visible in poster and list layouts.
- List rows retain Movie / TV right-bottom type labels and do not use that slot for source labels.
- Exact media-library visual parity is not claimed for this pass; button size, layout-toggle placement, search input size, search icon treatment and dropdown menu styling remain Deferred per user instruction.

Business logic changes:

- No search service, TMDB request, recommendation, scan, schema, migration, database update, media-library visibility or player behavior changed.
- UI binding/projection only: search filter selection commands for menu controls, short rating badge projection and a TV want-season tag projection.

Non-stage page changes:

- None. Home, Media Library, Recommendation, Detail, Player and Settings pages were not edited.

Not done:

- Ranking tab visual consistency remains 7.4c.
- AI recommendation tab and preference dialog polish remain 7.4d.
- Exact media-library visual parity for Discovery search controls remains deferred by current user instruction.
- No database update, commit or push was executed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Suggested commit message:

- `Align discovery search UI visuals`

### Watch Insights Restore and Profile Visual Follow-up

Completed:
- Removed redundant scroll restore opacity toggles that caused navigation and no-op refresh flashes.
- Fixed Watch Insights scrollbar hover reveal and draggable track bindings.
- Added the personality-poster page backdrop through the existing local cached palette-bitmap pipeline.
- Refined keyword breathing, theme contrast, summary pixel scrolling, DNA tag palettes/layout, progress indicators, description spacing, and subtle asynchronous card motion.
- Constrained narrative DNA prompt tags to 2-4 Chinese characters and advanced the profile prompt version.

Not done:
- No application launch, database update, migration, commit, or push was executed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.

Suggested commit message:
- `Polish watch insights restore and profile visuals`

### Watch Insights Persona and Quadrant Follow-up

Completed:
- Enlarged the persona poster by 20 percent while retaining its left/top anchor and cached glow strategy.
- Moved the persona heading to the top, enlarged/spaced the persona label, and placed a bold AI lead plus scrollable body to its right.
- Added backward-compatible `persona.lead` profile data and advanced the profile prompt version without changing recommendation prompt context.
- Replaced the quadrant's four decorative cards with an enlarged, transparent cross-axis plot driven by the existing AI X/Y values.
- Added a cached-glow breathing coordinate point and a direct, scrollable quadrant analysis region.

Not done:
- No application launch, database update, migration, commit, or push was executed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.

Suggested commit message:
- `Rework watch insights persona and quadrant cards`

### Watch Insights Watch-vs-Like Triptych Follow-up

Completed:
- Converted the watch/like/want cards from a flat three-column row into a layered triptych with a raised center card and inward-overlapping recessed side cards.
- Added smooth hover scale, translation, opacity, and cached-shadow response with code-behind Z-order control.
- Replaced liked ranks with hollow hearts and wanted ranks with hollow stars while preserving watched numeric ranks.
- Darkened the light-theme progress track and added a small bottom breathing area after the final module.

Not done:
- No application launch, profile/prompt change, database update, migration, commit, or push was executed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending warnings only.

Suggested commit message:
- `Add watch insights comparison triptych interaction`
