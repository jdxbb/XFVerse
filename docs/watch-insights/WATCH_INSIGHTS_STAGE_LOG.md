# Watch Insights Stage Log

## 2026-06-17 Watch Insights Visual Polish Follow-up

Stage goal:
- Polish Watch Insights iconography, loading colors, persona spacing, DNA clipping, frequent-tag rotation, taste-rank badge color, and preference-graph pointer feedback.

Completed:
- Moved the Watch Persona body upward and reduced the gap between the intro and persona label area.
- Switched the overview watched icon to `check` and aligned watched/not-interested overview icon foregrounds with the surrounding icon color treatment.
- Added loading spinner/text color styles that use accent color on light backgrounds and white on dark theme or poster-backdrop surfaces.
- Rotated the frequent-tag title icon 45 degrees clockwise.
- Fixed Watch DNA card and description viewport heights so scrollability detection no longer changes small-card height, while still showing slightly more of the last visible line.
- Matched dark-theme high-frequency combination rank badge fill/border treatment to the accent bubble family.
- Increased preference-graph mouse ripple generation frequency while keeping the effect throttled.

Explicitly not done:
- No Watch Insights statistics query, AI generation, recommendation algorithm, database schema, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:
- Blocker: none.
- Deferred: spinner contrast and DNA clipping still require manual visual verification in both theme modes.
- Noise: loading contrast uses theme/poster state as a background proxy rather than measuring rendered pixel luminance.

## 2026-06-17 Watch Insights Profile Spinner Crash And Preference Toggle Fix

Stage goal:
- Fix the profile refresh spinner crash introduced during icon migration, keep Watch Profile generation alive after page navigation cancellation, and correct custom recommendation preference toggle behavior.

Completed:
- Split the Watch Insights profile and statistics refresh icon styles so statistics no longer inherits the profile loading trigger.
- Changed refresh spinner storyboards to use named `BeginStoryboard` registrations that `StopStoryboard` can resolve when loading ends.
- Kept Watch Profile service generation detached from page activation cancellation, preserving the existing automatic generation mechanism while allowing in-flight generation to finish and write cache after navigation.
- Disabled enabling custom recommendation preferences when no saved preference text exists.
- Auto-enabled custom recommendation preferences only when saving from empty preference text to non-empty text.
- Removed automatic recommendation reloads from custom preference enable/disable, confirm, and clear operations; users must click refresh to request a new batch.
- Hardened Phosphor icon SVG parsing so a malformed or unsupported icon path does not crash WPF rendering.

Explicitly not done:
- No Watch Profile auto-generation mechanism, statistics query, recommendation algorithm, AI prompt, database schema, database update, migration, commit, or push was changed.

Validation:
- Windows Application log showed `.NET Runtime` event 1026 at the crash time with `System.InvalidOperationException` from `StopStoryboard` resolving `ProfileRefreshSpinStoryboard`.
- Static review confirmed custom preference save/toggle/clear no longer call `RequestReloadAsync(forceRefresh: false)`.
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify clicking generate profile, waiting for completion, and switching pages during generation no longer exits the app.
- Deferred: manually verify custom preference toggle states and manual refresh-only recommendation behavior in the running app.
- Noise: historical Windows Error Reporting entries remain on disk and can still appear in Event Viewer history.

## 2026-06-16 Watch Insights Micro Layout Polish

Stage goal:
- Apply the requested visual polish pass for Watch DNA, preference graph waves, watch-vs-like conclusion placement, tab highlight outline, taste combination, rhythm grid lines, and viewing calendar navigation.

Completed:
- Made the exploration DNA icon read as a compass with visible ticks, center point, and two-part needle.
- Adjusted DNA progress label and scrollable analysis text heights so progress labels are not clipped and scrollable copy shows full text lines.
- Slowed and extended the randomized preference waves, reduced their roundness, and made the wave opacity/force fade out over distance and time.
- Raised the watch-vs-like conclusion slightly, shifted the calendar month navigation left, and moved the taste-combination Top5 card/occurrence text as requested.
- Changed selected taste-combination links to full opacity and widened the rhythm chart low/middle/high grid-line spacing.
- Changed the light-theme tab highlight outline to dark gray.

Explicitly not done:
- No statistics query, watch-history semantics, AI prompt, recommendation logic, database schema, database update, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1 -v:minimal` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static patch review.
- Deferred: manually verify the revised DNA compass, progress labels, scrollable DNA copy, slow preference waves, taste-combination placement, rhythm grid lines, calendar navigation, and light-theme tab outline in the running WPF app.
- Noise: visual micro-positioning may need one more pass after seeing the exact rendered window size and theme.

## 2026-06-16 Profile Gender Validation And Prompt Export

Stage goal:
- Update the user profile dialog gender and contact validation behavior, connect Watch Persona posters to the saved user gender, and export the current profile/recommendation prompt templates with placeholders.

Completed:
- Replaced the profile dialog gender text box with a non-editable two-option combo box for female/male, with female as the default.
- Added save-time validation for phone, age, and email fields; invalid fields keep the dialog in edit mode, turn red through theme error brushes, and clear the error color immediately when edited.
- Allowed empty phone, age, and email values; non-empty values must match common phone/email formats or an age from 1 to 120.
- Normalized user profile gender in the model and profile service so missing or unsupported gender values fall back to female.
- Updated Watch Insights persona poster selection to read and listen to `IUserProfileService`; male profiles now use male persona poster/palette candidates where available.
- Exported prompt templates for taste summary, Watch DNA, persona, quadrant, and watch-vs-like insight conclusion to the requested local prompt folder with dynamic inputs replaced by placeholders.

Explicitly not done:
- No Watch Profile AI generation semantics, persona taxonomy, statistics calculation, recommendation algorithm, database schema, database update, migration, commit, or push was changed.
- No full desktop UI pass was run in this turn.

Validation:
- `dotnet build MediaLibrary.sln -m:1 -v:minimal` passed with 0 warnings and 0 errors.
- Migration diff remained empty.
- Static review confirmed persona poster gender is no longer hard-coded to female in `ApplyPersonaPoster`.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify the gender combo box opens, closes, and reopens on repeated clicks.
- Deferred: manually verify invalid phone, age, and email fields turn red in both light and dark themes, then clear immediately after editing.
- Deferred: manually verify Watch Persona poster and palette switch after saving male/female profile values.
- Noise: exported prompt files live outside the repository in the requested local folder.

## 2026-06-16 Taste Combination Top5 And Triple-Only Graph Polish

Stage goal:
- Apply the requested taste-combination layout and graph filtering polish without changing the underlying watch statistics service output.

Completed:
- Shifted the right Top5 card left by 16px and down by 8px using a local transform.
- Added a Top5-only circular rank badge style and a theme resource for a slightly deeper badge border.
- Removed Top5 progress bars; each ranking row now contains only rank, combination chips, and occurrence count, vertically centered.
- Moved the occurrence count left by narrowing its column and left-aligning the text.
- Changed selected graph-link glow to a blurred, lower-opacity wide path so it reads less like a hard outline.
- Rebuilt graph node selection from complete type x emotion x scene combinations only, capped at four labels per column.
- Rebuilt graph links from visible complete three-category combinations only and removed the old pair-edge fallback path.
- Preserved the four-slot column height and evenly spread columns with fewer than four labels across that same height.

Explicitly not done:
- No statistics calculation, AI prompt, recommendation logic, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed the Top5 progress bar path was removed and the obsolete pair-edge graph methods are gone.
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify Top5 card offset, row alignment, rank badge border contrast, and occurrence-count position in both themes.
- Deferred: manually verify graph columns show only labels from complete three-category combinations and distribute fewer than four labels evenly.
- Deferred: manually verify selected-link glow reads as soft glow rather than a hard outline.
- Noise: graph display now intentionally uses the Top10 complete combinations as the visual source, while the core snapshot still exposes aggregate pair edges for compatibility.

## 2026-06-16 Tab Gray Outline And Overview Delta Arrow Placement Correction

Stage goal:
- Correct the latest Watch Insights visual feedback for tab highlight outlines and overview month-over-month arrow placement without changing statistics semantics.

Completed:
- Changed `ColorWatchInsightsTabChromeStroke` so selected/hovered tab labels use a light-gray outline in the light theme and a dark-gray outline in the dark theme.
- Left the shared chrome text outline for the collapsed status subtitle and tab-right status text unchanged.
- Removed delta arrows from the current-value rows in the overview status cards, total watch time card, and watch-day card.
- Rebuilt each affected delta row as horizontal text plus arrow, so the arrow now appears immediately after the `compared with last month` delta value.

Explicitly not done:
- No statistics calculation, delta formatting semantics, profile AI, recommendation logic, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed the remaining overview delta-arrow bindings now sit in delta rows.
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify the tab gray outline contrast in both light and dark themes.
- Deferred: manually verify all overview non-zero arrows appear after the `compared with last month` number, not beside the main current value.
- Noise: the existing historical log entry still documents the earlier black/white outline pass that this entry supersedes.

## 2026-06-16 Quadrant Rhythm Calendar Waves And Watch-Like Polish

Stage goal:
- Apply the requested visual polish across taste quadrant, viewing rhythm, viewing calendar, preference graph waves, and Watch-vs-Like positioning without changing data semantics.

Completed:
- Split the taste-quadrant `A x B` heading into separate X-axis label, neutral `x`, and Y-axis label; X/Y labels now use their corresponding quadrant axis colors.
- Moved the taste-quadrant coordinate canvas slightly left and raised the left title/body block a little more in expanded-sidebar layout.
- Reworked `SplineAreaChart` so the viewing-time curve uses a vertical value ramp: x-axis blue, low purple, middle pink-orange, high red.
- Applied the same value ramp to the viewing-time filled area while keeping the previous top-strong / bottom-light opacity falloff.
- Added dedicated deeper blue calendar heat resources for `<30min` days and the matching legend swatch.
- Removed the calendar month navigation group's previous horizontal offset so previous/current/next is centered against the calendar grid.
- Changed preference-bubble waves to emit from random positions on the four content-area edges only; extended wave duration, reach, influence radius, and fade timing.
- Raised the Watch-vs-Like triptych stage and conclusion block slightly; moved the wanted star badge up by 0.5px.

Explicitly not done:
- No statistics calculation, profile generation, AI prompt, recommendation logic, database schema, database update, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Static search confirmed the quadrant heading now uses split labels, the calendar low-heat brush is referenced, and edge-only bubble wave emitters are active.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify quadrant label colors and coordinate offset in both sidebar states.
- Deferred: manually verify rhythm chart gradient, calendar nav centering, deeper low-heat blue, edge-origin waves, and raised Watch-vs-Like layout in the running WPF window.
- Noise: viewing-time chart colors are generated inside the chart control rather than exposed as separate theme resources.

## 2026-06-16 Watch DNA Icon And Overflow Height Polish

Stage goal:
- Apply the requested Watch DNA visual polish: replace placeholder text icons with pure icons and make all DNA cards grow slightly when any description requires internal scrolling.

Completed:
- Replaced the six Watch DNA placeholder text icons with no-background vector icons inside the existing 48x48 icon slot: movie ticket, heart, sofa, route branching, waveform, and compass.
- Matched type, emotion, scene, and narrative icon colors to the corresponding DNA chip color family; rhythm uses a cyan-blue theme brush and exploration uses a purple theme brush.
- Added `IconKind` to the DNA card read model so XAML icon selection no longer depends on display text.
- Added `IsProfileDnaTextScrollable` and a view-side ScrollViewer overflow check so any scrollable DNA description can raise a shared layout state.
- When the shared DNA overflow state is active, every DNA card gets a slightly taller minimum height.
- Non-progress DNA cards move the chip row and description down together; progress DNA cards keep the progress bar/labels fixed and move only the description down.

Explicitly not done:
- No DNA wording, AI prompt, profile semantics, statistics semantics, recommendation behavior, database schema, database update, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Static search confirmed the DNA template now uses `IconKind`, vector icon targets, and `ProfileDnaDescription` ScrollViewer markers.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify icon shapes, colors, and no-background rendering in both light and dark themes.
- Deferred: manually verify the shared overflow height state with one long DNA description and confirm progress cards keep their progress area fixed.
- Noise: DNA icons are vector paths, not bitmap assets or an external icon font.

## 2026-06-16 Watch Insights Persona Lead Brackets And Acrylic Paint Label

Stage goal:
- Apply the follow-up visual review for `你的观影人格`: inline lead brackets, inherited lead font, quote-muted bracket color, and a heavier acrylic-paint persona label backdrop.

Completed:
- Kept the lead brackets as inline `Run` text before and after the lead copy, so the lead reads as one complete sentence instead of relying on separate overlay components.
- Changed the bracket style to inherit the lead text font, size, and weight, and matched its color treatment to the muted taste-summary quote color/opacity.
- Increased the persona label row/control height to provide safe drawing room for the paint layer above and below the text.
- Removed the old projected text shadow rendering path from `PersonaPaletteText`; the label text fill/outline behavior is otherwise unchanged.
- Replaced the previous separated stroke backdrop with an opaque acrylic/impasto-like body plus four connected back-and-forth brush passes.
- Removed the old edge-stroke path that could create a thin internal line, and added clipped dry-brush texture marks at a restrained right-up to left-down 30-degree angle.

Explicitly not done:
- No persona label text, AI prompt, profile data, statistics data, recommendation logic, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed the old projected-shadow call and `PaintStroke` path are gone from `PersonaPaletteText`.
- Static search confirmed `AcrylicPass` / `TextureMark` are now the only persona-paint backdrop data records.
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify the lead brackets match taste-summary quote color in both themes and inherit the lead font visually.
- Deferred: manually verify the acrylic paint reads as connected, opaque, thick, textured strokes without top/bottom clipping or thin seam artifacts.
- Noise: the acrylic texture remains vector-rendered from palette colors, not a bitmap paint asset.

## 2026-06-16 Watch Insights Chrome Outline And Overview Delta Arrows

Goal: address Watch Insights tab/status outline feedback and overview month-over-month delta presentation without changing statistics semantics.

Completed:

- Added a dedicated `ColorWatchInsightsTabChromeStroke` theme token and `WatchInsightsTabChromeTextEffect` for selected/hovered Watch Insights tab labels.
- Set the light-theme tab-highlight outline to black and the dark-theme tab-highlight outline to white.
- Increased the shared `WatchInsightsChromeTextEffect` blur radius for the collapsed status subtitle and tab-right status text, preserving the existing light white / dark black outline colors.
- Added overview delta-arrow properties so non-zero month-over-month deltas render an up/down arrow immediately to the right of the current numeric value.
- Changed zero month-over-month overview delta text to `较上月无变化`.
- Applied the same arrow and zero-text behavior to four overview status cards, total watch time, and watch-day count.

Validation:

- Default `dotnet build MediaLibrary.sln -m:1` was blocked because the running `MediaLibrary.App` process locked the default output executable.
- `dotnet build MediaLibrary.sln -m:1 -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Not done:

- No statistics calculation, watch-history semantics, profile AI, recommendation behavior, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed by temp-output build.
- Deferred: runtime WPF validation should compare tab-highlight black/white outline, status subtitle/status text outline thickness, and overview arrow placement in both themes.
- Noise: the default output build still fails while the app executable is open; this is an output-file lock rather than a compile error.

## 2026-06-16 Taste Combination Graph Layout And Bezier Links

Goal: address taste-combination layout, spacing, tooltip, link selection, and link rendering feedback without changing statistics semantics.

Completed:

- Increased the graph node vertical spacing from 94px to 108px and expanded the graph canvas from 430px to 500px high.
- Centered the `类型` / `情绪` / `场景` column captions over their 74px node columns and removed the taste-combination subtitle period.
- Replaced straight `Line` links with cubic Bezier `Path` links; each path starts at the source node right edge, ends at the target node left edge, and uses restrained horizontal control points.
- Added source-to-target gradient brushes using the same type / emotion / scene color families as the preference-bubble graph.
- Lowered inactive-link maximum opacity and added a separate thick low-opacity path layer for selected-link glow, avoiding live `DropShadowEffect`.
- Added Top10 combination related-node metadata so selecting any node in an `A x B x C` combination highlights both pair links for that combination.
- Added graph-node tooltip detail: `标签：次数` plus up to three highest-count `组合：次数` lines, with a graph-local 120ms tooltip delay.
- Reworked the right Top5 panel to size to content, vertically center against the left graph, use larger item gaps, shift combo/progress content right, and align `x次` with the progress bar row.

Validation:

- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Not done:

- No taste-combination calculation, statistics query, profile AI, recommendation behavior, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed by build.
- Deferred: runtime WPF validation should confirm Bezier curvature, gradient contrast, selected-link glow strength, Top5 vertical rhythm, tooltip timing/content, and node-column alignment in both themes.
- Noise: selected-link glow is a second cached-free path layer, not a blurred shadow.

## 2026-06-16 Watch-vs-Like Triptych Size And Shadow Safe Area

Goal: address watch-vs-like feedback for default overlap, star badge size, larger downward cards, clipped top/bottom shadows, and conclusion placement without changing data or profile semantics.

Completed:

- Reviewed earlier shadow clipping notes in this log and reused the same direction: add real safe drawing space and remove local clipping instead of shrinking card content.
- Enlarged the three flat triptych cards from 320x252 to 338x264, expanded the logical canvas to 978x286, and expanded the visible stage to 326px high / 1000px max width.
- Reduced adjacent card overlap from 24px to 18px and pushed the default side-card translations outward so the middle primary card covers the right card less before hover.
- Lowered the default primary lift from 10px to 6px and lowered the hover/intermediate/far translation targets, so the larger cards read as growing downward.
- Enlarged only the `经常想看` star badge viewbox from 30px to 33px while keeping its 30px badge host centered.
- Changed the triptych stage and Viewbox to `ClipToBounds=False` and gave the stage extra vertical drawing room, matching the prior shadow-safe-grid fix pattern for clipped cached shadows.
- Reduced the module minimum height from 548px to 532px so the conclusion panel sits slightly higher after the triptych stage grows.

Validation:

- Static inspection confirmed the three staged card visibility bindings, named card controls, and `ApplyWatchLikeFocus` path still line up.
- Static inspection confirmed the module remains on `CachedShadowBorder` shadows and does not reintroduce live `DropShadowEffect`.

Not done:

- No watch/like/want ranking data, profile conclusion text, AI prompt, statistics calculation, recommendation behavior, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none identified by static inspection.
- Deferred: runtime WPF validation should confirm the larger cards, reduced default overlap, unclipped top/bottom shadows, centered larger star badge, and raised conclusion block at supported window widths.
- Noise: local `ClipToBounds=False` is intentional for shadow rendering; cached-shadow hit testing remains bounded by the existing `CachedShadowBorder` behavior.

## 2026-06-16 Watch Insights Stage4 Visual Tree Split

Goal: reduce the first-entry spinner hitch identified in `watch-insights-perf.log`, without changing Watch Insights visual content or profile/statistics service semantics.

Completed:

- Split the Profile Stage4 watch-vs-like module so stage 4 now materializes the lightweight module shell, while stages 5, 6, and 7 materialize the left, center, and right focus cards separately.
- Kept the existing card sizes, default opacity, z-order, scale, translation, shadow, and hover focus animation parameters unchanged.
- Added a visual-tree render-commit gate for Profile and Statistics initial loading: final stage flags no longer hide the loading overlay immediately; the overlay hides only after the final stage has yielded through the render dispatcher once.
- Kept Statistics visual stages unchanged but applied the same render-commit gate so stage 5 cannot hide the loading overlay before its final render pass.

Expected impact:

- The Profile first-entry loading overlay may remain visible slightly longer, but the heavy watch-vs-like card group is no longer created in one UI-thread burst.
- Any remaining slow frame during final card creation is kept under the loading overlay instead of appearing after the page body becomes visible.

Validation:

- Static inspection confirmed profile visual stage count is now 7 and exposes stage 5/6/7 readiness properties.
- Static inspection confirmed the three watch-vs-like cards are each guarded by stage 5/6/7 visibility triggers based on the existing card style.
- `dotnet build MediaLibrary.sln -m:1 -p:OutDir=<temp>` passed with 0 warnings and 0 errors.

Not done:

- No card visuals, hover behavior, statistics/profile service semantics, AI/profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed by temp-output build.
- Deferred: runtime perf validation should compare new `visual-tree-stage-ready` timing against the previous Stage4 1656ms / 931ms slow-frame trace.
- Noise: default-output build can still be blocked if the running app executable is open.

## 2026-06-16 Watch Insights Chrome Contrast, Shadow Safe Area, And Loading Diagnostics

Goal: address Watch Insights chrome contrast and module-card right-edge clipping feedback, while only diagnosing the first-entry spinner stutter from existing logs.

Completed:

- Adjusted Watch Insights chrome text tokens so selected/hovered tab text keeps the existing highlighted foreground but uses a white outline in light theme and a black outline in dark theme.
- Adjusted the collapsed titlebar subtitle and tab right status text to use ink-black foreground with white outline in light theme, and fog-white foreground with black outline in dark theme.
- Increased transparency on the Watch Insights titlebar/tab separator and the tab-bottom separator by tuning the shared chrome line token.
- Reapplied the earlier shadow-safe-grid approach for Profile Analysis and Watch Statistics: the scroll viewport now expands farther left/right, while matching safe columns keep module-card content aligned. This gives 54px large-card shadows enough right-side drawing room without shrinking the card body.
- Reviewed `logs/watch-insights-perf.log` without changing loading/performance code.

Loading diagnostics:

- The latest first-entry profile trace showed `initial-loading-visible` at 02:29:19.003, stage 1 at 02:29:19.250, stage 2 at 02:29:19.285, stage 3 at 02:29:19.320, and `initial-loading-hidden` at 02:29:19.356.
- After the loading overlay was hidden, profile stage 4 did not complete until 02:29:21.013. During that gap the log recorded slow frames of 598ms, 931ms, and 133ms.
- This points to late stage-4 visual materialization on the UI thread as the visible spinner hitch source, rather than profile stages 1-3 or the data load itself.
- The statistics tab showed the same pattern at smaller scale: stage 5 completed after the overlay hid, with 225ms and 223ms slow frames around that transition.

Validation:

- Static inspection confirmed both Watch Insights tab scroll viewers use the widened 56px left/right shadow-safe gutters.
- Static inspection confirmed the shared Watch Insights chrome tokens drive the titlebar subtitle, tab status text, tab hover/selected text outline, titlebar-tab separator, and tab-bottom separator.
- Default output build could not overwrite the running app executable because `MediaLibrary.App` was open and locked the file.
- `dotnet build MediaLibrary.sln -m:1 -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Not done:

- No loading/performance code was changed for the first-entry spinner stutter.
- No profile/statistics service semantics, AI/profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed after temp-output build validation.
- Deferred: a later performance pass should keep the loading overlay visible until the final deferred profile/statistics stage has either completed or been moved to a lighter background-friendly path.
- Noise: the normal output build is blocked while the running app executable is open; existing performance log records include older crash/stutter runs and should be compared by timestamp.

## 2026-06-15 Preference Bubble Inner Ring Removal

Goal: address Watch Insights preference-graph feedback by removing the visible white-gray inner ring from preference bubbles without changing statistics service semantics.

Completed:

- Removed the inner glass rim overlay from preference bubbles.
- Removed the now-unused `CreateBubbleGlassRimBrush` helper.
- Kept bubble base colors, outer border, depth shadow, water ripple behavior, and rebound tuning unchanged.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` has no whitespace errors; it only reports LF-to-CRLF warnings for touched files.

Not done:

- No profile/statistics service semantics, AI profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed.
- Deferred: full WPF visual verification is still required to confirm the inner white-gray ring is gone in light and dark themes.
- Noise: none beyond existing LF-to-CRLF warnings.

## 2026-06-15 Preference Graph Bubble Motion and Glass Polish

Goal: address Watch Insights preference-graph feedback for stronger bubble rebound, fewer water ripples, and cleaner bubble appearance without changing statistics service semantics.

Completed:

- Increased boundary rebound by raising the bubble boundary restitution and edge acceleration strength.
- Increased bubble-to-bubble collision response by raising spacing, separation force, correction strength, collision force, and collision impulse.
- Removed the distant secondary ripple visual and the far-reaching flow-wave force field; mouse movement now creates only the local ripple layer.
- Removed the static bubble highlight, lower shade overlay, and extra color-shift rim that made the bubbles look heavy.
- Kept the original tag-kind background/border mapping and added only a subtle glass rim overlay.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` has no whitespace errors; it only reports LF-to-CRLF warnings for touched files.

Not done:

- No profile/statistics service semantics, AI profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed.
- Deferred: full WPF visual verification is still required for preference-graph bounce strength, ripple density, and bubble glass appearance.
- Noise: none beyond existing LF-to-CRLF warnings.

## 2026-06-15 Statistics Calendar Alignment Polish

Goal: address Watch Insights statistics-calendar layout feedback for calendar placement, header controls, and the right-side metric cards without changing statistics service semantics.

Completed:

- Moved the calendar body further left.
- Moved the first-row calendar controls slightly right by a smaller amount than the calendar body shift.
- Moved the right-side calendar metric cards slightly right.
- Moved all three right-side calendar metric cards slightly upward.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` has no whitespace errors; it only reports LF-to-CRLF warnings for touched files.

Not done:

- No profile/statistics service semantics, AI profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed.
- Deferred: full WPF visual verification is still required for the statistics-calendar alignment and right-side metric-card placement.
- Noise: none beyond existing LF-to-CRLF warnings.

## 2026-06-15 Quadrant and Watch-Like Layout Polish

Goal: address Watch Insights follow-up feedback for the taste quadrant text/chart balance and the watch-vs-like conclusion spacing without changing profile/statistics service semantics.

Completed:

- Expanded the taste quadrant text column and text max width toward the right while keeping its left edge fixed.
- Reduced the gap between the taste quadrant text and chart columns to give the body copy more usable width.
- Moved the taste quadrant coordinate chart upward as a whole.
- Moved the watch-vs-like insight conclusion closer to the large card bottom by reducing the module bottom padding.
- Removed the trailing punctuation from the watch-vs-like subtitle.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` has no whitespace errors; it only reports LF-to-CRLF warnings for touched files.

Not done:

- No profile/statistics service semantics, AI profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed.
- Deferred: full WPF visual verification is still required for taste quadrant text width, chart vertical position, and watch-vs-like conclusion spacing.
- Noise: none beyond existing LF-to-CRLF warnings.

## 2026-06-15 Persona Lead Layout and Bracket Polish

Goal: address Watch Insights persona-card feedback for persona label shadow softness, lead sentence layout, overflow tooltip behavior, and lead corner bracket placement without changing profile/statistics service semantics.

Completed:

- Softened the rendered persona label projected shadow by reducing glow alpha, glow stroke width, and projected shadow opacity.
- Changed the persona lead layout so expanded navigation uses a clause-split two-line lead, while collapsed navigation uses a single line for leads up to 33 non-whitespace characters.
- Added a long-lead collapsed-navigation fallback: leads over 33 non-whitespace characters also use the two-line lead layout without local text truncation.
- Added fixed line-height clause rows with `CharacterEllipsis` trimming and visual-trim-only tooltip behavior for persona leads, so tooltip appears only when the visible lead is actually clipped.
- Replaced fixed-position lead corner brackets with larger dynamically positioned brackets that follow the first and last visible lead characters, using the same muted quote color treatment as the taste-summary quotation marks.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` has no whitespace errors; it only reports LF-to-CRLF warnings for touched files.

Not done:

- No profile/statistics service semantics, AI profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed.
- Deferred: full WPF visual verification is still required for persona lead wrapping, tooltip timing, and dynamic bracket placement under expanded/collapsed navigation.
- Noise: none beyond existing LF-to-CRLF warnings.

## 2026-06-15 Overview Subtitle and DNA Card Polish

Goal: address Watch Insights follow-up feedback for the Statistics overview subtitle and Profile DNA card details while keeping profile/statistics service semantics unchanged.

Completed:

- Added the `你的观影时间，一目了然` subtitle under `洞察总览`.
- Removed the trailing punctuation from the `观影 DNA` subtitle.
- Reduced the DNA progress endpoint marker and changed its theme colors away from white so it adapts to light and dark themes.
- Reduced the collapsed-navigation DNA grid spacer width from 104px to 88px, giving each DNA card slightly more width.
- Removed the DNA description scroll cue clipping so the default non-hover state shows the same three-and-a-half-line text area that previously only appeared after hover.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` has no whitespace errors; it only reports LF-to-CRLF warnings for touched files.

Not done:

- No profile/statistics service semantics, AI profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

Known Issues:

- Blocker: none confirmed.
- Deferred: full WPF visual verification is still required for DNA card width, endpoint color, and three-and-a-half-line description behavior.
- Noise: none beyond existing LF-to-CRLF warnings.

## 2026-06-15 Loading Spinner, Diagnostics, Glass Chrome, and Palette Cleanup

Goal: address Watch Insights feedback for initial loading spinner consistency, first-entry spinner stutter diagnostics, title/tab glass transparency and continuity, and dirty poster-derived backdrop color without changing profile/statistics service semantics.

Completed:

- Matched the Watch Insights initial loading overlays to the shared app spinner sizing used by other pages by using the shared `LoadingSpinnerTemplate` at the same 38px host size.
- Added view-layer initial-loading diagnostics for visible/hidden loading spans, loading-time slow-frame summaries, and `initialLoading` markers on slow-frame and frame-summary entries.
- Added `forceRefresh` to Watch Insights profile/statistics load-start diagnostics so first activation, tab activation, manual refresh, and forced refresh paths are easier to separate.
- Made the Watch Insights title bar and tab chrome use the same stronger poster glass layer, removed the Watch Insights title-bar bottom border, and overlapped the tab glass by 1px to avoid the visible color break between the two chrome bands.
- Reduced light/dark Watch Insights chrome white/sheen overlays and border alpha so the chrome reads as glass instead of an opaque white band.
- Softened the `Glass` cached backdrop bitmap generation and reduced vivid palette boosting; Watch Insights route-level poster backdrop now uses the standard palette mode to avoid over-saturated dirty backdrop colors.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` is empty.
- `git diff --check` has no whitespace errors; it only reports LF-to-CRLF warnings for touched files.

Not done:

- No profile/statistics service semantics, AI profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.
- No full WPF screenshot/manual runtime verification in this turn; user-side verification should inspect the first-entry spinner smoothness and the updated `logs/watch-insights-perf.log` entries.

Known Issues:

- Blocker: none confirmed by build.
- Deferred: full WPF visual verification is still required for spinner smoothness, title/tab glass continuity, and persona-poster backdrop color.
- Noise: LF-to-CRLF warnings remain in `git diff --check`; no whitespace errors were reported.

## Phase 7.7g Follow-up - Shadow Safe Grid Correction

Goal: correct the repeated Watch Insights Profile/Statistics module-card shadow clipping by fixing the clipping root cause instead of shrinking card width, while keeping Profile and Statistics service semantics unchanged.

Completed:

- Identified the remaining root cause as the card body still sitting on the effective content edge after the previous negative-margin/padding compensation. The card effect could still bleed into the clipped edge, and padding-only tuning made the card width feel smaller.
- Replaced the Profile and Statistics tab content padding with an explicit shadow-safe grid: a real top/left gutter for module-card effects, a right-side expanded scroll viewport to preserve the module content width, and no negative content margin.
- Applied the same structure to Profile Analysis and Watch Statistics so both tabs use the same shadow-safe layout model.
- Corrected the safe grid from one-sided right expansion to symmetric left/right viewport expansion, so module-card bodies align with the original content column while shadows still have space to render on both sides.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gInsightShadowSymmetric\"` passed with 0 warnings and 0 errors.

Not done:

- No statistics/profile service semantics, AI/profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

## Phase 7.7g Follow-up - Color And Profile State Cleanup

Goal: address follow-up feedback for TV season list divider length, Library list-view card background, poster state icon color, rating-star stroke, watch-progress fill, and remaining Profile Analysis status text without changing service semantics, Movie-only boundaries, database schema, migration, database update, commit, or push.

Completed:

- Kept the TV season target-episode row highlight expanded, but moved the `Episode` list top divider into a normal-width overlay so the line no longer extends into the page background.
- Changed only the Library list-view item background to use the navigation background color; poster cards and other page cards keep their existing backgrounds.
- Reused `BrushRatingStarFill` for solid poster want/favorite icons, making the selected star/heart lighter while preserving the white outline treatment.
- Lightened the light-theme rating-star stroke to a muted gray; dark-theme rating-star stroke remains unchanged.
- Added a fixed light-blue watch-progress fill resource and applied it to Library/History media progress bars and the Home continue-watching thin progress bar.
- Removed the remaining Profile Analysis status/warning text between `观影口味总结` and `观影 DNA`, including the keyword explanatory empty text.
- Increased Watch Insights Profile/Statistics scroll padding on the top and sides so module card shadows have a larger safe drawing area.
- Set the taste-summary scroll surface to physical scrolling for smoother pixel-level scroll behavior.
- Slightly increased the core-keyword slot and chip height after the previous over-tightening pass.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gColorStateCleanup\"` passed with 0 warnings and 0 errors.

Not done:

- No statistics/profile service semantics, AI/profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

## Phase 7.7g Follow-up - Episode Highlight And Tab Refresh Alignment

Goal: address follow-up feedback for TV season target-episode highlighting, Library layout toggle parity, Watch Insights shadow safe area, taste-summary overflow, keyword slot height, and Statistics refresh placement without changing service semantics, Movie-only boundaries, database schema, migration, database update, commit, or push.

Completed:

- Expanded the TV season episode list horizontally across the card padding and compensated row content padding so the target-episode highlight can cover the full row instead of stopping at the inner list edge.
- Aligned the Library layout switcher with the Movie Discovery search layout switcher by using the same glass segment host, themed hover state, secondary foreground, and selected accent treatment.
- Corrected the Watch Insights Profile and Statistics shadow safe area by removing the negative content margin that pushed cards back into the clipped viewport.
- Changed the taste-summary text area to a modern auto-revealed scroll surface using the Watch Insights scrollbar style, so long summaries can scroll instead of truncating.
- Reduced core-keyword slot and chip height with fixed centered rows, keeping the 2x3 layout while making the outer frames less dominant.
- Moved the Statistics refresh action and combined status/time text into the same Tab-divider action layout used by Profile refresh; the Statistics card header now only keeps the range switch.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gEpisodeLibraryStats\"` passed with 0 warnings and 0 errors.

Not done:

- No statistics/profile service semantics, AI/profile generation, recommendation, scan, collection, TV/Movie boundary, database schema, migration, database update, commit, or push change.

## Phase 7.7g Follow-up - Theme Layering And Statistics Top Cleanup

Goal: address the latest visual feedback for rating stars, Movie Discovery layout toggle theming, Watch Insights card layering, profile summary spacing, keyword density, and Statistics top content without changing statistics/profile services, Movie-only boundaries, database schema, migration, database update, commit, or push.

Completed:

- Added a theme-level `BrushRatingStarFill` so light-theme detail rating stars use a lighter brighter fill while dark theme keeps the existing accent tone.
- Strengthened the light-theme `BrushGlassBorder` to make nested glass cards easier to distinguish on Home, Watch Insights, and other shared glass-card surfaces.
- Changed the Movie Discovery search layout toggle host and hover state to use theme glass resources instead of a fixed temporary light card color.
- Increased Watch Insights Profile and Statistics scroll safe padding while offsetting content margins so top/side shadows can render without moving the cards.
- Removed the profile empty-state card between `观影口味总结` and `观影 DNA`; status remains in the Tab action label and the taste-summary empty state.
- Removed the Statistics top module-state and warning-chip blocks so `洞察总览` is the first visible Statistics card.
- Removed the extra blank line between profile summary natural paragraphs while keeping two full-width leading spaces per paragraph.
- Reduced the visual weight and spacing of the core-keyword slot frames.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gThemeLayering\"` passed with 0 warnings and 0 errors.

Not done:

- No service semantics, AI/profile generation, recommendation, scan, collection, Movie-only boundary, database schema, migration, database update, commit, or push change.

## Phase 7.7g Follow-up - Profile Tab Refresh And Quote Summary Pass

Goal: adjust the Profile Analysis refresh placement, cursor behavior, card spacing and taste-summary readability based on user feedback without changing statistics, recommendation, Movie-only boundaries, database schema, migration, database update, commit, or push.

Completed:

- Moved `刷新画像` out of the taste-summary card and into the top Tab divider action area.
- The profile refresh action now appears only when `画像分析` is selected.
- Replaced the text refresh button with a transparent MDL2 refresh icon button whose tooltip is `刷新画像`.
- Placed `LastProfileRefreshedAtText` to the left of the refresh icon.
- Explicitly restored arrow cursor behavior for the Profile and Statistics content surfaces; interactive controls keep their own hand cursor.
- Replaced large Watch Insights module cards with a page-local card style that removes the rectangular glass shadow effect and adds larger bottom spacing.
- Added top and bottom scroll-content padding to reduce card edge clipping.
- Removed the cached-status pill from the taste-summary card.
- Tightened the taste-summary header gap so summary and keyword content starts directly under the subtitle.
- Changed the taste-summary body to a quote-style text treatment with large muted opening and closing quote marks.
- Empty summary content now shows centered empty-state text inside the same content area without an extra border or background.
- Profile summary display formatting preserves AI-returned natural paragraphs and only adds two full-width spaces at each paragraph start.
- Updated the profile summary prompt to ask future AI output for two natural paragraphs separated by a newline.

Validation:

- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildWatchInsightsRefreshTabLayout\"` passed with 0 warnings and 0 errors.

Not done:

- No statistics service口径, recommendation, player, scan, collection, Movie-only boundary, database schema, migration, database update, commit, or push change.

## Phase 7.7g Follow-up - Profile Summary Layout Pass

Goal: adjust the Profile Analysis first card based on user feedback without changing profile generation, statistics, recommendation, Movie-only boundaries, database schema, migration, database update, commit, or push.

Completed:

- Removed the top profile module-state card from the Profile Analysis Tab so the first visible card is `观影口味总结`.
- Moved `刷新画像` into the `观影口味总结` card header right action area, next to compact status and last-refresh text.
- Removed the three lower-left summary chips from the taste-summary area.
- Fixed the taste-summary card height and aligned the summary panel bottom with the right keyword panel.
- Reduced the keyword panel height and changed core keywords to a two-row, three-column `UniformGrid`.
- Replaced keyword chip pills with low-radius rounded rectangles.

Validation:

- First build retry failed because the temporary build output volume was full; after removing Codex temp build outputs, `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuildWatchInsightsProfileLayoutFinal\"` passed with 0 warnings and 0 errors.

Not done:

- No profile service口径, AI generation, recommendation, player, scan, collection, Core business rule, database schema, migration, database update, commit, or push change.

## Phase 7.7g Follow-up - Tab Alignment And Modern Surface Pass

Goal: align the Watch Insights top Tab strip with Movie Discovery and modernize Watch Insights component surfaces without changing statistics/profile services, Movie-only boundaries, database schema, migration, database update, commit, or push.

Completed:

- Matched the Watch Insights `TabControl` template positioning to Movie Discovery's top-tab coordinates: same negative top offset, same content top padding, hidden native header host, divider placement and selected-content margin source.
- Switched Watch Insights main module cards from the older page-card surface to the shared glass page-card surface used by modernized Home / Library / Discovery / detail pages.
- Switched inner insight panels to the shared glass inline-panel baseline, so profile, overview, calendar, chart and ranking components use the same modern translucent surface family.
- Replaced the Watch Insights local white scrollbar thumb colors with the same resource-driven 6px modern scrollbar treatment used by Discovery / Settings.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuildInsightsPolish\"` passed with 0 warnings and 0 errors.

Not done:

- No service口径, AI/profile generation, recommendation, player, scan, collection, Core business rule, database schema, migration, database update, commit, or push change.

## Phase 7.7g Statistics Lower-Area Visual Closeout

Goal: close the Watch Statistics lower area against the Phase 7.7 UI redesign scope without changing statistics service semantics, profile generation, recommendation input, Movie-only boundaries, database schema, migration, database update, commit, or push.

Completed:

- Rebuilt the lower statistics modules around `偏好图谱`, `标签排行月榜`, `观影节奏`, and `口味组合地图`.
- Changed the preference graph to a scalable bubble canvas using real type / emotion distribution data, with bubble size based on tag counts and legend colors from existing theme resources.
- Changed tag rankings to three Top3 cards for type, emotion and scene tags, keeping real count / progress bindings.
- Changed viewing rhythm to a time-bucket bar chart plus weekday/weekend and duration distribution panels.
- Changed taste combination to a scalable node/link canvas plus Top10 list, using real combination nodes / links or the existing Top10 fallback when graph data is sparse.
- Added App-layer display projections for bubble placement, rhythm summary, dominant duration text, graph node presentation and stable chart sizing.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\XFVerseCodexBuild77g\"` passed with 0 warnings and 0 errors.

Not done:

- No statistics service口径 change, profile prompt/model/cache change, recommendation change, TV Watch Insights, mixed Movie + TV profile, database schema change, migration, database update, commit, or push.
- No full 7.7 regression closeout or global 7.8 visual polish.

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

## External Movie Favorite Signal Follow-up

Completed:
- Watch Statistics and Watch Profile input now include `UserMovieCollectionItem.IsFavorite` so no-source external favorites are counted as favorite signals.
- Favorite tag comparison and profile samples now merge external favorite collection rows with local `Movie.IsFavorite` rows by TMDB identity.

Boundaries kept:
- No profile prompt structure or recommendation generation policy was changed.
- No database update, commit, or push was run.

Validation:
- `dotnet build MediaLibrary.sln -p:BaseOutputPath=<temp-build-output>` passed with 0 warnings and 0 errors after migration generation.

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

## Watch Insights Profile Layout And State Polish

Stage goal:
- Improve the Profile Analysis hierarchy and interaction feedback without changing profile generation, prompts, database schema, or media-library semantics.

Completed:
- Split `你的观影人格` and `口味象限` into separate full-width module rows so they match the width of other large Watch Insights cards.
- Persisted the selected Watch Insights Tab and separate scroll offsets for `画像分析` and `观影统计` through the existing in-memory navigation-state service.
- Centered the taste-summary text inside its left inner card.
- Reworked the six profile keywords into a staggered, slightly rotated tag cloud with per-tag color variants and hover lift feedback.
- Reworked the four tag-based DNA cards to use three evenly distributed rounded-rectangle tags with matching colors and hover lift feedback.
- Replaced both DNA progress components and the `看得多 vs 真喜欢` progress bars with the same 4-pixel rounded progress visual used by the media library.
- Standardized DNA card header, component, and description regions so descriptions align by row.
- Added two-character local indentation to DNA descriptions without changing the AI prompt.
- Limited DNA description viewports to roughly three lines, using the Watch Insights modern scrollbar for overflow; mouse-wheel input remains inside overflowing text and returns to page scrolling when no internal overflow exists.
- Enlarged the persona poster, added rounded clipping, and reused the media-library palette shadow behavior.
- Added hover lift feedback to the `经常观看` / `喜爱` / `想看` group cards.

Explicitly not done:
- No profile prompt, profile service, statistics service, recommendation, player, scan, or collection behavior change.
- No database update and no migration.
- No new automated test project.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.
- Desktop visual automation could not start because the Computer Use runtime reported a package export error; manual visual acceptance remains required.

Known Issues:
- Blocker: none confirmed by build or static review.
- Deferred: global animation timing/easing and unrelated Watch Insights polish remain outside this patch.
- Noise: the desktop automation runtime failure is external to XFVerse and does not affect the application build.

## Watch Statistics Component Polish

Stage goal:
- Simplify Watch Statistics component hierarchy and align selected visual details with the Profile Analysis component language.

Completed:
- Removed the dynamic secondary text below `你的观影世界，一目了然` in the overview header.
- Removed the inner glass card around `本月偏好图谱` / `累计偏好图谱`; the bubble visualization now renders directly in the full-width module card content area.
- Increased the preference-graph share of the two-column row and widened the module gap for clearer visual hierarchy.
- Updated statistic metric panels to use the same rounded inner-panel spacing and subtle hover lift used by Profile Analysis interactive cards.
- Added subtle hover lift feedback to preference bubbles while preserving type/emotion color semantics.
- Reused the media-library rounded progress-bar style for ranking, week-part, duration-distribution, and taste-combination progress displays.
- Slightly increased overview metric card height and padding to reduce crowding.

Explicitly not done:
- No statistics calculation, cache, time-range, calendar, profile, prompt, recommendation, player, or database behavior change.
- No database update and no migration.
- No broad redesign of every Watch Statistics module.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Known Issues:
- Blocker: none confirmed by build or static review.
- Deferred: manual visual acceptance and remaining global Watch Statistics polish.
- Noise: existing line-ending normalization warnings do not affect the application build.

## Watch Insights Loading And Profile Detail Polish

Stage goal:
- Replace dirty first-load content with a focused loading state, remove visible scroll restoration, and refine profile keyword, DNA, persona, and bottom-spacing details.

Completed:
- Added centered initial-loading overlays for Profile Analysis and Watch Statistics, using the shared rotating loading spinner and concise status text.
- Kept manual/background refresh behavior unchanged when existing content is already available; the full overlay only covers first load without data.
- Hid the active Tab scroll viewer while a saved non-zero offset is being restored, then revealed it after the target offset is applied, preventing the visible top-to-saved-position movement.
- Restored taste-summary text to left text alignment while keeping the text block itself evenly inset inside the left inner card.
- Updated the summary-card prompt to require exactly six keyword objects with 2-6-character labels and scores restricted to 1/2/3, with exactly two keywords at each score.
- Bumped the profile prompt version so newly generated profiles use the scored-keyword contract.
- Added backward-compatible scored-keyword parsing and normalization; legacy `keywords: string[]` caches remain displayable without database changes.
- Arranged keyword scores in `1 / 3 / 2` and `2 / 3 / 1` rows.
- Applied score-based tag sizing: score 1 keeps base size, score 2 grows by 10%, and score 3 grows by 20%.
- Made keyword tag width adapt to label length while retaining bounded placement within the keyword cloud.
- Added a subtle repeating opacity breath to keyword tags without removing hover lift feedback.
- Moved all DNA tag/progress components upward and aligned the progress components to the same centered component row as tag-based genes.
- Enabled the existing half-line overflow cue behavior for DNA descriptions while preserving nested-wheel handling.
- Removed the persona text inner card and duplicate small persona title; the remaining persona type uses a reduced display size.
- Reduced Profile Analysis bottom whitespace by removing the final module bottom margin and shrinking the trailing spacer.

Explicitly not done:
- No database field, database update, or migration.
- No statistics calculation, recommendation, player, scan, collection, or media-file behavior change.
- No prompt change outside the profile summary keyword contract.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Known Issues:
- Blocker: none confirmed by build or static review.
- Deferred: manual visual acceptance of loading overlay timing, keyword-cloud spacing, and DNA half-line cue.
- Noise: legacy cached keywords receive deterministic local scores until a new profile is generated with the updated prompt.

## Watch Profile Refresh Failure Status

Stage goal:
- Make profile refresh failures actionable when the previous profile cache is restored.

Completed:
- Confirmed the reported immediate fallback was caused by the configured AI provider returning HTTP 402, rather than by the scored-keyword parser.
- Added a shared, privacy-safe AI failure formatter for authentication, billing/quota, permission, model/endpoint, rate-limit, timeout, and service-availability failures.
- Profile refresh now stores the safe failure reason, preserves it after cache normalization, and displays it in the profile status and cached-fallback module state.
- Existing profile cache remains available after a failed refresh; no profile payload or media-library state is deleted.

Explicitly not done:
- No AI account balance, provider configuration, API key, endpoint, or model setting was changed.
- No profile prompt, recommendation algorithm, database schema, database update, or migration was changed in this failure-status fix.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Known Issues:
- Blocker: the external AI provider must have usable quota/billing status before a new profile can be generated.
- Deferred: manual WPF acceptance of long failure text and tooltip behavior.
- Noise: the previous profile remains visible by design when refresh fails.

## Watch Insights Performance Diagnostics

Stage goal:
- Add enough timing diagnostics to separate service/query cost, ViewModel projection cost, WPF layout/render cost, scroll restoration, and frame stalls for both Watch Insights tabs.

Completed:
- Added `logs/watch-insights-perf.log` as a dedicated Watch Insights performance log, separate from the large AI perf log.
- The new log records profile/statistics service events, profile input events, ViewModel projection stages, selected tab changes, activation timing, scroll restoration attempts, first idle layout metrics, visual/effect counts, render tier, and slow-frame summaries.
- Stopped eager loading the hidden Watch Insights tab on activation. The page now loads only the selected tab first; the other tab loads when selected.
- Removed forced `UpdateLayout()` calls from Watch Insights scroll restoration and nested text mouse-wheel handling.
- Preserved existing tab and scroll-offset persistence behavior.

Root-cause findings from code inspection:
- The previous activation path could load Profile and Statistics at the same time; Profile input can also request all-time statistics, so the page could issue overlapping statistics work before the user opened the second tab.
- Scroll restoration and nested text wheel handling forced synchronous layout on the UI thread.
- Both tabs use a dense non-virtualized WPF visual tree with multiple live drop shadows and small animations; the new layout diagnostics will quantify whether this is the remaining bottleneck during manual reproduction.

Explicitly not done:
- No application launch or local reproduction was performed by Codex; reproduction is left to the user as requested.
- No statistics calculation, profile prompt, database schema, database update, migration, recommendation, scan, player, or collection behavior was changed.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:
- Blocker: none confirmed after build.
- Deferred: user-side reproduction is needed to compare `serviceMs`, `projectionMs`, `layout-idle idleMs`, `visuals`, `dropShadows`, and `slowFrames` in `logs/watch-insights-perf.log`.
- Noise: slow-frame diagnostics only summarize frames at or above 50 ms and log individual frames at or above 100 ms to avoid log spam.

## Static Cached Shadow Migration

Stage goal:
- Replace Watch Insights card/tag glow paths that still used WPF `DropShadowEffect` with the same cached bitmap shadow strategy used by media-library poster shadows.

Completed:
- Added a reusable `CachedShadowBorder` that draws a cached bitmap shadow from the existing poster shadow generator, sized from the control's runtime `RenderSize`; cache misses are generated off the UI thread and then repainted.
- Exposed the poster cached-shadow generator for non-poster border rendering without changing poster behavior.
- Migrated Watch Insights module cards, inner panels, keyword tags, DNA tags, preference bubbles, graph nodes, calendar cells, and quadrant cards from live `ShadowStatic*` effects to cached bitmap shadows.
- Audited other pages and migrated the shared glass-card style chain (`HeroCardStyle`, `GlassPageCardStyle`, `GlassInlinePanelStyle`, `GlassHome*`, and `GlassCompactCardStyle`) plus their Border usages to `CachedShadowBorder`.
- Removed the remaining static glow usage from correction combo boxes and scan progress indicators; poster-adjacent small card shadows were also moved to cached bitmap borders.
- Extended poster palette shadow coloring to `CachedShadowBorder`, then migrated Home poster palette/base shadows from live `DropShadowEffect` to runtime-sized cached bitmap shadows.

Explicitly not done:
- Popup, dialog, menu, and player overlay shadows still use their existing popup/dialog effects; these are separate transient surfaces and not part of the card/glow performance issue.
- Unused `ShadowStatic*` token definitions remain for compatibility until a dedicated style-token cleanup.
- No database schema, database update, migration, prompt, recommendation, scan, player, or media-library semantic behavior change.

Validation:
- Global XAML scan shows no actual `ShadowStatic*` usage outside token definitions.
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: user-side WPF reproduction should confirm lower `effects/dropShadows` counts in `logs/watch-insights-perf.log`.
- Noise: cached shadows are regenerated in the background when runtime size or shadow parameters change, then reused from the in-memory bitmap cache.

## Light Theme Cached Shadow Parity

Stage goal:
- Restore visible light-theme shadow depth after the static cached-shadow migration while keeping dark-theme glow behavior unchanged.

Completed:
- Added theme-level cached-shadow color, opacity, and depth tokens for large cards, inline cards, compact poster base shadows, score badges, and scan progress glow.
- Light theme now uses black cached shadows with non-zero depth for card surfaces; dark theme keeps the existing pale glow with zero depth where appropriate.
- Updated shared glass-card styles, Watch Insights local cached-shadow styles, scan logs, profile dialog cards, movie detail tags, watch-history filter cards, discovery score badges, scan progress glow, and Home poster base shadows to read cached-shadow parameters from theme resources.
- Rechecked migrated `CachedShadowBorder` usages so local card shadows no longer depend on fixed dark-theme glow values.

Explicitly not done:
- Home poster palette glow default color remains poster/palette-driven and can still be overwritten by the poster palette behavior at runtime.
- Existing popup, icon, menu, date-picker, and media-library poster effects remain outside this card cached-shadow parity fix.
- No database schema, database update, migration, prompt, recommendation, scan, player, or media-library semantic behavior change.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: user-side WPF validation should compare light and dark themes on Watch Insights, Home, Movie Discovery ranking badges, scan progress, movie detail tags, and watch-history filters.
- Noise: fixed `ShadowStatic*` compatibility tokens and non-card transient effects still exist by design.

## Cached Shadow Hit-Test Boundary Fix

Stage goal:
- Prevent cached shadow drawings from blocking buttons outside the owning card.

Completed:
- Identified that `CachedShadowBorder` draws its bitmap shadow outside the control's render bounds via negative padding.
- Limited `CachedShadowBorder` hit testing to the control's actual `RenderSize`, so the out-of-bounds shadow remains visible but no longer intercepts clicks on neighboring buttons or cards.
- Kept child interaction inside the card unchanged.

Explicitly not done:
- No page-specific `IsHitTestVisible` workaround was added.
- No layout, navigation, database schema, database update, migration, prompt, recommendation, scan, player, or media-library semantic behavior change.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: user-side WPF validation should confirm buttons next to cached-shadow cards are clickable after entering affected pages.
- Noise: cached shadows still render outside card bounds by design; only hit testing is clipped to the card bounds.

## Cached Shadow Blur Density Follow-up

Stage goal:
- Bring cached card/tag shadow spread closer to the old theme-specific `DropShadowEffect` resources.

Completed:
- Added theme-level cached-shadow blur-radius tokens for large cards, inline cards, compact poster base shadows, score badges, and scan progress glow.
- Routed shared glass-card styles and Watch Insights local cached-shadow styles through the blur-radius tokens instead of fixed high blur values.
- Light theme now uses the old light-theme blur levels for large/inline/score/scan shadows, reducing the overly diffuse look introduced by the static cached-shadow migration.

Explicitly not done:
- Poster palette shadows remain poster/image-color driven and keep their poster-specific blur settings.
- No database schema, database update, migration, prompt, recommendation, scan, player, or media-library semantic behavior change.

Validation:
- `dotnet build MediaLibrary.sln`
- Result: 0 warnings, 0 errors.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: user-side WPF validation should compare cached shadow density against the previous theme look in both light and dark themes.
- Noise: cached shadows are still bitmap-rendered local assets; this pass only adjusts spread, not the caching strategy.

## Cached Shadow Extra Tightening

Stage goal:
- Make cached card/tag shadows slightly more concentrated after visual feedback.

Completed:
- Reduced the theme cached-shadow blur-radius tokens again for large cards, inline cards, compact poster base shadows, score badges, and scan progress glow.
- Kept the existing theme colors, opacities, depths, cached bitmap strategy, and poster palette shadow behavior unchanged.

Explicitly not done:
- No database schema, database update, migration, prompt, recommendation, scan, player, or media-library semantic behavior change.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>`
- Result: 0 warnings, 0 errors.
- Normal output build was blocked by a running `MediaLibrary.App` process holding the app exe.

Known Issues:
- Blocker: none confirmed by temp-output build.
- Deferred: user-side WPF validation should confirm the updated shadow density in light and dark themes.
- Noise: cached shadows remain bitmap-rendered local assets; this pass only adjusts spread.

## Poster Base Shadow Parameter Restoration

Stage goal:
- Keep the Watch Insights personality poster on the cached bitmap shadow strategy while restoring the original poster-specific base shadow color.

Completed:
- Restored the personality poster base shadow color from the generic cached compact token to explicit `#000000`, matching the original poster shadow parameters used before the shared cached-shadow token migration.

Explicitly not done:
- No prompt, profile generation, statistics, cache schema, database update, migration, recommendation, scan, player, or media-library semantic behavior change.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:
- Blocker: none confirmed by temp-output build.
- Deferred: user-side WPF validation should confirm the personality poster glow strength after the shared shadow follow-up.
- Noise: poster palette glow remains poster/image-color driven.

## Non-detail Poster Glow Strength Follow-up

Stage goal:
- Strengthen the Watch Insights personality poster glow consistently with other non-detail poster surfaces.

Completed:
- Increased the personality poster palette opacity from `0.40` to `0.50` and base shadow opacity from `0.24` to `0.30`.
- Kept poster dimensions, blur radius, cached bitmap padding, palette color behavior, and profile data unchanged.

Explicitly not done:
- No detail-page poster styling, prompt, profile generation, statistics, cache schema, database update, migration, recommendation, scan, player, or media-library semantic behavior change.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:
- Blocker: none confirmed by temp-output build.
- Deferred: user-side WPF validation should confirm the stronger personality poster glow in both themes.
- Noise: this is opacity-only and does not widen the glow.

## Non-detail Poster Glow Strength Follow-up 2

Stage goal:
- Keep the Watch Insights personality poster aligned with the second non-detail poster glow increase.

Completed:
- Increased personality poster palette/base opacity from `0.50/0.30` to `0.58/0.36`.
- Kept dimensions, blur radius, bitmap padding, palette behavior, profile generation, and all detail-page poster values unchanged.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:
- Blocker: none confirmed by temp-output build.
- Deferred: user-side visual validation remains required.
- Noise: opacity-only visual adjustment.

## Non-detail Poster Glow Midpoint Follow-up

Completed:
- Reduced personality poster palette/base opacity from `0.58/0.36` to `0.54/0.33`, the midpoint between the previous and latest versions.
- Detail-page poster values and profile behavior remain unchanged.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:
- Blocker: none confirmed by temp-output build.
- Deferred: user-side visual validation remains required.
- Noise: opacity-only adjustment.

## Watch Insights Restore, Backdrop, Scrollbar, Taste and DNA Polish

Stage goal:
- Remove navigation/refresh flashes and finish the requested profile-analysis interaction and visual refinements without reintroducing live shadow effects.

Completed:
- Limited scroll restoration to real offset mismatches. Refresh completion no longer hides and re-shows an already restored tab, while a genuinely reset viewer is still restored before it becomes visible.
- Fixed the Watch Insights scrollbar template range/value bindings, added hover reveal, and retained wheel-triggered auto reveal so both main tabs and overflowing text areas can be dragged.
- Added a page-local personality-poster palette backdrop using the same quantized, cached bitmap strategy as the detail backdrop. No live blur or per-frame palette sampling is used.
- Changed taste-summary wheel handling to a fixed 36-pixel step and increased only the summary body line height.
- Added dedicated light/dark score colors for the six keywords and a cached-shadow breathing treatment based on draw opacity and tiny bitmap scaling. The underlying shadow bitmap is not regenerated per frame.
- Added dedicated soft-pink, pale-yellow, and pale-green DNA tag palettes for type, emotion, and scene genes, including brighter dark-theme borders/backgrounds.
- Reduced DNA tag widths through asymmetric outer-item margins so the first and third outer edges stay fixed while inner gaps grow.
- Moved tag/progress/text regions to their requested vertical positions, kept the description viewport at approximately three and a half lines, and increased only DNA description line height.
- Added a shorter DNA progress track and a breathing endpoint indicator.
- Added asynchronous cached-card ambient floating near Y +/-2 px with smooth hover lift and deeper cached-shadow draw opacity.
- Updated the DNA prompt version and constrained narrative tags to the maintained 2-4 Chinese-character whitelist.

Explicitly not done:
- No application launch or user-flow reproduction was performed; runtime visual acceptance remains user-owned for this pass.
- No database update, migration, commit, push, recommendation behavior, scan behavior, player behavior, or media-library semantic behavior changed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build/static validation.
- Deferred: manually verify theme contrast, backdrop intensity, scrollbar dragging, restored-offset first frame, keyword breathing, and DNA motion in the running WPF app.
- Noise: prompt version `wi-profile-persona-23-parallel-v11-dna-tag-length` intentionally marks older cached profiles as generated by an older rule set until manually refreshed.

## Persona and Taste Quadrant Layout Follow-up

Stage goal:
- Recompose the persona and taste-quadrant cards around larger visual anchors, dedicated analysis text regions, and an actual AI-coordinate visualization.

Completed:
- Expanded the personality poster from `278x370` to `334x444`, preserving its left/top anchor so growth occurs toward the right and bottom. Cached poster glow dimensions were updated with the poster.
- Moved the persona title block from vertical center to the top of the card and increased the poster-to-content gap.
- Increased the persona label from 28 to 50, strengthened its weight, and added subtle visual character spacing through a dedicated display projection.
- Rebuilt the persona content area as two columns: the large persona label on the left and analysis on the right, beginning slightly below the title row.
- Added optional `persona.lead` profile data. The AI prompt now requests one concise lead sentence followed by a distinct evidence-based body; old caches receive a local neutral lead during normalization.
- Added a bold lead style and a 6.5-line scrollable body with the modern auto-reveal scrollbar and overflow cue. Persona body line height is 27.
- Removed the four decorative quadrant cards and the quadrant canvas background.
- Expanded the quadrant canvas from `310x260` to `465x390`, shifted it left relative to the previous right-side footprint, and aligned its top with the card title row.
- Added a true cross-axis layout with `情绪沉浸/轻松消遣` on Y and `熟悉安全/新鲜探索` on X.
- Reprojected the existing AI-provided `xAxisScore/yAxisScore` values onto the enlarged plot area and replaced the old point with a cached-glow breathing indicator. No quadrant-specific point color is used.
- Removed the quadrant text sub-card and placed the quadrant name/analysis directly in the module card. The body uses the same 27 line height and a 7.5-line modern scroll area.
- Advanced the profile prompt version to `wi-profile-persona-23-parallel-v12-persona-lead` without changing recommendation prompt context.

Explicitly not done:
- No application launch or runtime screenshot verification was performed.
- No recommendation algorithm/prompt, database update, migration, scan, player, or media-library semantic behavior changed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build/static validation.
- Deferred: manually verify poster/content proportions, long persona names, both text scroll areas, and coordinate-point placement at representative axis extremes.
- Noise: old cached profiles display the local neutral lead until a manual refresh generates the AI lead required by prompt v12.

## Watch-vs-Like Triptych Interaction Follow-up

Stage goal:
- Turn the three watch/like/want comparison cards into a layered, depth-oriented triptych while preserving their existing profile semantics.

Completed:
- Replaced the three-column `ItemsControl` layout with three explicit `ContentControl` surfaces bound to the existing watch, like, and want groups.
- Default state now uses a raised middle card (`scale=1.05`, `ZIndex=2`) and recessed side cards (`scale=0.93`, `ZIndex=1`) translated 30px inward for overlap.
- Hovering any card immediately promotes it to `ZIndex=3`, animates it to `scale=1.05`, and deepens its cached shadow. The other two animate to `scale=0.93` and `opacity=0.65`.
- Side-card hover adds a small additional inward translation to strengthen the parallax/overlap response.
- Leaving the whole triptych restores the middle-forward default state with 190ms cubic easing.
- Kept shadows on the cached bitmap path; the interaction animates only draw opacity, transform, and element opacity.
- Replaced liked-item numeric ranks with `♡` and wanted-item ranks with `☆`; watched items retain `1/2/3`.
- Darkened the light-theme Watch Insights progress track from `#C5CDD8` to `#AEB9C8`.
- Increased the final profile-module bottom margin by 18px to restore a small page-bottom breathing area.

Explicitly not done:
- No profile generation, prompt, recommendation, statistics calculation, database update, migration, application launch, commit, or push was performed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build/static validation.
- Deferred: manually verify overlap hit targets, animation feel, card content clipping, and light-theme track contrast in the running WPF app.
- Noise: the heart/star glyphs use `Segoe UI Symbol`; appearance follows the installed Windows font rendering.

## 2026-06-14 Statistics Overview and Calendar Redesign

Stage goal:
- Rebuild the Statistics overview metrics and monthly calendar around viewing-day semantics, a Top 6 tag cloud, and a complete fixed calendar grid.

Completed:
- Removed range prefixes from the overview metric-card titles and replaced the watched-movie metric with distinct viewing days for both month and all-time ranges.
- Added `WatchDays`, longest-streak start/end dates, most-active-day movie count, and current-month state to the statistics snapshot contract.
- Advanced the statistics logic version so older cached payloads cannot retain the previous watched-count/calendar-cell contract.
- Reused Movie Detail semantic icons for watched, favorite, want-to-watch, and not-interested states; added clock, calendar, and price-tag icons to the second row.
- Reduced the four first-row card widths by one quarter while preserving the outer edges, and reused the same card width for viewing duration and viewing days.
- Centered all overview values except the tag cloud.
- Limited frequent tags to Top 6, displayed occurrence counts, scaled them by relative frequency, and placed ranks 1-6 in the requested center/corner arrangement using the cached keyword-chip style.
- Changed the calendar service output to a Monday-first 35/42-cell grid containing disabled adjacent-month dates instead of UI-generated placeholders.
- Reduced the visible calendar width, increased cell spacing, kept weekday headers aligned to the seven columns, and limited cells to a centered day number.
- Added two-line day tooltips with weekday, distinct movie count, and accumulated watch duration.
- Moved month navigation below the title and to the left of the legend, aligned the first summary card to that row, narrowed all three summary cards, and increased their vertical gaps.
- Replaced the summary abbreviations with calendar-check, flame, and trophy icons; enlarged primary values and added the longest-streak `MM.dd-MM.dd` range.

Explicitly not done:
- No application launch, runtime screenshot, database update, migration, commit, or push was performed.
- No recommendation, profile-generation, scan, player, or media-library business semantics were changed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed apart from expected LF-to-CRLF working-copy warnings.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build/static validation.
- Deferred: user-side visual validation is required for the 25% card-width reduction, fixed 560px calendar footprint, tooltip placement, and Top 6 tag-cloud balance at supported window sizes.
- Noise: the statistics cache logic version changed intentionally; the first statistics load recomputes the snapshot.

## 2026-06-14 DNA Card Frozen-Transform Crash Fix

Stage goal:
- Stop Watch Insights from terminating while loading DNA-card ambient animations.

Completed:
- Confirmed through the Windows `.NET Runtime` event that `ProfileDnaCard_Loaded` was animating a sealed/frozen `TranslateTransform.Y` and throwing an unhandled `InvalidOperationException`.
- Added a shared transform accessor that clones frozen template transforms into a mutable per-card `TransformGroup` before ambient or hover animation begins.
- Preserved the existing asynchronous DNA float and hover behavior; no animation was removed.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed after the targeted code fix and solution build.
- Deferred: user-side reproduction is required to confirm Watch Insights opens normally and DNA cards continue animating.
- Noise: none.

## 2026-06-14 DNA Progress Indicator Name-Scope Crash Fix

Stage goal:
- Fix the second Watch Insights load crash exposed after the frozen-transform issue was removed.

Completed:
- Confirmed from the Windows `.NET Runtime` event at `2026-06-14 15:23:24` that the DNA progress template's Loaded Storyboard could not resolve `ProgressIndicatorDot` in the `ControlTemplate` name scope.
- Moved the opacity and scale breathing Storyboard onto the indicator ellipse itself, where no cross-template target-name resolution is required.
- Preserved the existing endpoint breathing effect and progress-bar layout.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.

Validation:
- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build/static validation after both observed unhandled exceptions were fixed.
- Deferred: user-side reproduction is required to confirm the page now opens and remains stable.
- Noise: none.

## 2026-06-14 Window-Level Persona Poster Backdrop Fix

Stage goal:
- Make the personality-poster palette background use the same full-window cached backdrop path as Movie Detail.

Completed:
- Extended the existing `MainWindow` poster-palette backdrop host to Watch Insights without changing Watch Insights into a detail route.
- Routed the local `PersonaPosterImageUri` through the same palette extraction and frozen cached-bitmap backdrop behaviors used by Movie Detail.
- Removed the page-local backdrop and its extra opacity/gradient layers, which were confined by the normal content-host margin and muted the extracted colors.
- Reused the Movie Detail translucent title-bar treatment while keeping its normal Watch Insights layout, controls, and navigation semantics.
- Kept the sidebar, navigation state, and page content margins unchanged while allowing the palette background to cover the complete window root and remain visible through the title bar.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.
- No profile-generation, statistics, navigation, or detail-page business behavior was changed.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed apart from expected line-ending warnings.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side visual verification is required for full-window coverage and palette appearance in both themes.
- Noise: the first uncached persona poster still requires one local palette extraction; subsequent applications reuse the existing caches.

## 2026-06-14 Vivid Persona Backdrop Palette

Stage goal:
- Increase Watch Insights personality-poster backdrop color clarity without changing Movie Detail or other detail-page backgrounds.

Completed:
- Added an opt-in `Vivid` poster-palette mode while retaining the existing extraction and cached frozen-bitmap pipeline.
- Applied the vivid mode only while Watch Insights is the active route.
- Increased extracted palette saturation and usable brightness range before static backdrop generation; Movie Detail and other poster backdrops remain on the unchanged standard mode.
- Kept the base extracted palette cache shared and deterministic while allowing the final Watch Insights palette to generate its own cached backdrop bitmap.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.
- No persona poster assets, profile-generation prompts, theme resources, or detail-page color parameters were changed.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed apart from expected line-ending warnings.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side visual verification is required to judge the vivid-mode strength in both themes and across different persona posters.
- Noise: switching between standard and vivid modes can create one additional cached backdrop bitmap per distinct quantized palette.

## 2026-06-14 Force-Directed Preference Bubble Graph

Stage goal:
- Upgrade the preference graph into a full-width, three-category force-directed bubble visualization without reintroducing expensive dynamic shadow rendering.

Completed:
- Changed the preference-graph subtitle to `你的偏好，汇聚于此`.
- Added scene-tag bubbles from the existing `SceneDistribution` snapshot data and updated the legend to type blue, emotion pink, and scene soft green.
- Selected up to six labels from each category so lower-frequency scene labels cannot be displaced entirely by the other categories.
- Removed the white category circles from inside bubbles; each bubble now contains only a larger semibold label and a smaller occurrence count.
- Removed the right-side tag-ranking card and expanded the preference graph into one full-width module card.
- Replaced fixed ViewModel coordinates and the `ItemsControl` layout with a named `BubbleCanvas` populated by lightweight runtime bubble controls.
- Added center gravity, pairwise collision repulsion, bounded elastic response, low-amplitude sinusoidal drift, velocity damping, and speed limiting in the existing rendering loop.
- Added hover radius easing; the enlarged physical radius pushes neighboring bubbles away and returns them after pointer exit.
- Paused physics while the statistics tab or bubble canvas is outside the active scroll viewport.
- Used fixed layout hosts plus `ScaleTransform` for radius animation so each frame does not trigger bubble remeasurement.
- Kept dynamic bubbles free of cached or live shadows; the proposed radius-shadow pre-cache was explicitly not added.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.
- No third-party physics library, new statistics query, schema field, or shadow-cache expansion was added.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by build or static inspection.
- Deferred: user-side validation is required for collision spacing, hover expansion strength, drift speed, and label readability at supported window widths.
- Noise: the physics loop intentionally uses O(n²) pair checks, bounded to at most 18 bubbles and paused outside the viewport.

## 2026-06-14 Viewing Rhythm Chart Redesign

Stage goal:
- Replace the rigid rhythm charts with smoother semantic visualizations and make duration distribution represent actual watched time.

Completed:
- Replaced the viewing-time column chart with a lightweight custom spline-area chart using a smooth cubic curve, vertical brand-color gradient fill, and a radial highlight at the peak.
- Removed the high/middle/low labels and rendered three subtle dashed horizontal grid lines behind the curve.
- Kept the time-bucket labels and watched-duration captions aligned below the curve.
- Replaced weekday/weekend progress rows with a centered two-segment donut chart.
- Replaced the weekday/weekend text boxes with custom vector office-computer and popcorn icons inside their corresponding blue and pink rounded legend swatches.
- Kept ratio, total watched time, and daily average details below each semantic icon.
- Changed duration distribution from distinct-movie count share to accumulated actual `DurationWatchedSeconds` share within each movie-runtime bucket.
- Added watched-duration text to each duration row and changed the dominant-duration summary to rank by actual watched time.
- Advanced the statistics cache logic version so cached count-based duration distributions are recomputed.
- Implemented all three visuals as local WPF drawing controls that redraw only when data, theme brushes, or size change; no third-party chart library or rendering-loop animation was added.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.
- No watch-history write semantics, media duration inference rules, or unrelated statistics modules were changed.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by build or static inspection.
- Deferred: user-side visual verification is required for spline curvature, peak highlight strength, donut spacing, icon clarity, and small-window label density.
- Noise: histories whose movie runtime cannot be resolved remain excluded from runtime-bucket percentages, matching the previous bucket eligibility rule.

## 2026-06-14 Watch Insights Transition and Motion Polish

Stage goal:
- Remove page-return and no-op refresh flashes, restore direct scrollbar interaction, and strengthen the requested profile animations without changing insight semantics.

Completed:
- Removed the scroll-restoration opacity gate that temporarily hid the active tab while a persisted offset was reapplied.
- Applied immediately available scroll offsets synchronously and kept retry-based restoration only for layouts whose scroll extent is not ready yet.
- Reused the existing profile and statistics UI projections when cache metadata or source fingerprints confirm that the returned data is unchanged, avoiding collection clear/repopulate flashes.
- Initialized the Watch Insights backdrop source from the local fallback persona poster before profile loading completes.
- Kept the backdrop on the same window-level palette extraction and cached frozen-bitmap pipeline used by detail routes, so it covers the full shell and page background.
- Enlarged the invisible scrollbar interaction lane while retaining a narrow visual thumb, revealed the scrollbar while its ScrollViewer is hovered, and kept it visible during mouse capture for dragging.
- Strengthened keyword breathing through a wider cached-shadow opacity/radius range and a small card-opacity pulse.
- Restored the required WPF progress-template parts for DNA progress bars, fixing percentage width calculation and exposing a larger breathing endpoint indicator.
- Lightened the DNA progress track in both themes.
- Increased the six DNA cards' asynchronous ambient vertical movement and shadow breathing while removing card-level mouse-hover lift; DNA tag hover remains unchanged.

Explicitly not done:
- No application launch, process termination, database update, migration, commit, or push was performed.
- No prompt, profile score, navigation persistence, or watch-history business semantics were changed.

Validation:
- The normal solution build was blocked only because the user-running application held the default executable open.
- An isolated-output `MediaLibrary.App` build passed with 0 warnings and 0 errors without stopping the running application.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated build or static inspection.
- Deferred: user-side runtime verification is required for transition continuity, scrollbar drag behavior, full-window palette coverage, and final animation strength.
- Noise: the currently running application must be restarted before it can load these newly built changes.

## 2026-06-14 Persona and Taste Quadrant Refinement

Stage goal:
- Separate the persona poster from its analysis card, make the text layout respond to the persona-label width, and improve the taste-quadrant axis layout and point animation smoothness.

Completed:
- Moved the persona poster out of the large analysis card so it renders directly over the Watch Insights backdrop while the analysis card occupies only the remaining width.
- Restored the persona card title to the standard module-card top and left padding.
- Made the persona label non-wrapping and used its measured width to size the lead column, leaving the remaining card width to the body text.
- Moved the lead below the persona label and kept only the body in the right-side scrollable text region.
- Added the two-character local indentation to the persona body without changing the AI prompt.
- Set the persona body viewport to an exact half-line height so overflowing text retains the final partial-line continuation cue.
- Strengthened only the persona poster's cached palette glow and local shadow while preserving the static local-resource rendering strategy.
- Reduced the quadrant text column, widened the coordinate canvas, and placed the horizontal-axis labels outside the two axis endpoints.
- Updated the quadrant point coordinate projection for the wider axis bounds.
- Replaced animated cached-shadow properties with a static cached point plus a transform-only radial halo pulse to avoid per-frame shadow regeneration.

Explicitly not done:
- No application launch, process termination, prompt change, database update, migration, commit, or push was performed.
- No profile generation semantics, persona classification, or quadrant score semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors without stopping the running application.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for long persona labels, poster glow strength, half-line visibility, narrow-window layout, and perceived quadrant pulse smoothness.
- Noise: the currently running application must be restarted before it can load these changes.

## 2026-06-14 Watch-vs-Like Folded Triptych Refinement

Stage goal:
- Turn the flat watch-vs-like overlap into a folded-screen composition with perspective cues, semantic rank outlines, and clearer shared progress tracks.

Completed:
- Added edge-anchored `SkewTransform` states to the left and right cards with default angles of -8 and +8 degrees and horizontal compression to 0.9.
- Kept the center card flat and prominent while allowing it to fold slightly backward when either side card becomes active.
- Added a topmost translucent fold-shade template part to every card; side cards show it by default, the active card fades it out, and inactive cards fade it in.
- Extended the code-behind animation state to coordinate skew, horizontal and vertical scale, overlap translation, opacity, cached-shadow opacity, shade opacity, and `Panel.ZIndex`.
- Restored numeric ranks for all three groups and rendered the liked and wanted ranks inside local vector outline-heart and outline-star shapes.
- Shortened only the triptych progress bars with additional right-side inset.
- Replaced the opaque Watch Insights progress-track colors with theme-specific translucent tracks; all Watch Insights controls sharing the media-progress style receive the same update.
- Increased the final module card's bottom padding to create more space below the conclusion panel.

Explicitly not done:
- No application launch, process termination, profile prompt change, database update, migration, commit, or push was performed.
- No watch/like/wanted ranking data or progress-value calculation semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build or static inspection.
- Deferred: user-side runtime verification is required for fold direction, overlap depth, shade strength, hover handoff, outline readability, and track contrast in both themes.
- Noise: the running application must be restarted before the new XAML and animation code can be observed.

## 2026-06-14 Statistics Overview and Calendar Polish

Stage goal:
- Align the statistics overview metrics with the profile keyword language and improve calendar readability, spacing, and theme behavior.

Completed:
- Changed the frequent-tag visual content to match the profile core-keyword chips exactly: one centered label, the same rotated cloud placement, the same breathing cached shadow, and occurrence count retained as the tooltip and size input.
- Replaced the overview favorite and wanted glyphs with local filled vector heart and star icons.
- Added a shared overview metric-value style and applied it to the first-row counts and second-row watch-time/day values.
- Fixed the second-row watch-time and watch-day cards to the same 164-pixel height as the first row so their values no longer drop to the bottom of the taller frequent-tag row.
- Increased the overview module's bottom padding.
- Set calendar-day foreground explicitly from the themed button foreground, while retaining the muted disabled state for adjacent-month dates.
- Changed the calendar legend from a wrapping panel to a single horizontal row and allowed the control row to span the calendar's flexible middle column.
- Increased the calendar grid width from 560 to 588 pixels and slightly increased horizontal cell margins.
- Increased the right-side calendar value font, reduced the caption font, and expanded the icon-to-text gap.
- Increased the calendar module's bottom padding.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.
- No statistics query, count, watch-duration, calendar-date, or frequent-tag ranking semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build or static inspection.
- Deferred: user-side runtime verification is required for single-line legend fit, filled-icon weight, keyword-cloud readability, calendar density, and metric alignment in both ranges and themes.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-14 Preference Bubble Physics and Session Persistence

Stage goal:
- Preserve the preference-bubble simulation across page visits, rebalance the force model, and add pointer-driven fluid interaction and disposable visual ripples.

Completed:
- Vertically aligned all three legend dots with their labels and aligned the complete legend row with the subtitle line.
- Added process-lifetime bubble-state persistence keyed by statistics range and the complete type/emotion/scene label dataset.
- Persisted normalized position, velocity, and radius before canvas rebuild or page unload, then restored the same state after tab changes, navigation, page recreation, and canvas-size changes.
- Kept persistence memory-only so restarting the application resets the simulation without creating settings files or database records.
- Replaced linear diameter sizing with a 76-pixel base diameter and an 80% maximum radius increase using `sqrt(count / maxCount)`.
- Reduced center gravity from `0.18` to `0.008` and added localized soft edge constraints instead of relying on central clustering.
- Increased asynchronous low-frequency drift with independent phase-derived frequencies and secondary wave components.
- Strengthened collision separation with same-frame 52% overlap correction per particle, stronger repulsion, and a closing-velocity impulse.
- Added a 180-pixel pointer influence field combining distance-weighted radial repulsion and pointer-velocity fluid drag.
- Preserved individual bubble hover expansion and its collision-driven neighbor displacement.
- Added distance-thresholded pointer ripples that scale and fade for 900 milliseconds, ignore hit testing, and remove themselves from the canvas on completion.

Explicitly not done:
- No application launch, persistent settings file, database update, migration, commit, or push was performed.
- No preference-tag counts, category selection, or top-item limits were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build or static inspection.
- Deferred: user-side runtime verification is required for long-running separation stability, pointer-force strength, ripple density, state restoration, and behavior at supported window sizes.
- Noise: process-lifetime persistence intentionally resets when the application exits.

## 2026-06-15 Viewing Rhythm Chart and Layout Refinement

Stage goal:
- Make the viewing-time curve terminate cleanly at the baseline, accurately mark its spline peak, correct donut segment rendering, and reorganize the rhythm module into a clearer two-row layout.

Completed:
- Anchored the spline at the absolute bottom baseline on both sides while placing every zero-minute bucket directly on that same baseline.
- Replaced the old top/middle/bottom guides with three evenly spaced dashed guides at 25%, 50%, and 75% of the plot height.
- Derived the highlight position from the vertical extrema of every cubic Bezier segment instead of selecting only the largest discrete data bucket.
- Added a lightweight opacity pulse to the spline peak halo while retaining data-driven chart geometry.
- Changed donut arc caps from round to flat and scaled the inter-segment gap to protect very small percentages such as 2%.
- Added arc-aware donut tooltips with weekday/weekend, total duration, and daily average on three lines.
- Moved the weekday/weekend legend to the right of the donut, retained semantic icons, and placed each percentage at the right edge of its legend row.
- Removed the always-visible total and daily-average text below the legend.
- Rebuilt the module as a full-width viewing-time card on the first row and two equal-width, equal-height weekday/weekend and duration cards on the second row.
- Removed the duration subtitle and dominant-duration text, vertically centered percentages with their progress bars, and increased the module's bottom padding.

Explicitly not done:
- No application launch, process termination, statistics query change, database update, migration, commit, or push was performed.
- No viewing-time bucket, weekday/weekend ratio, or duration-distribution calculation semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for spline shape, mathematical peak placement, pulse strength, 2%/98% donut readability, arc tooltip hit areas, and equal-card layout at supported window sizes.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Taste Combination Graph Spacing Refinement

Stage goal:
- Separate the taste-combination legend from the graph content and simplify the graph presentation inside the main module card.

Completed:
- Removed the left graph's nested chart-card background so the graph renders directly inside the outer Watch Insights module card.
- Moved the frequency legend out of the fixed graph canvas and placed it below the complete 430-pixel graph region.
- Added an 18-pixel graph-to-legend gap so the legend no longer overlaps the lowest graph nodes.
- Increased the taste-combination module's bottom padding to 32 pixels, adding space below the inner ranking card.

Explicitly not done:
- No application launch, graph-data calculation change, database update, migration, commit, or push was performed.
- No taste-combination node, link, ranking, or frequency semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for legend spacing, graph width, and bottom whitespace at supported window sizes.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Initial Loading, Keyword Breathing, and DNA Indicator Refinement

Stage goal:
- Prevent profile cards from appearing beneath the first-load spinner, improve scrollbar drag affordance without thickening the visible thumb, restore clearly visible keyword breathing, and distinguish the DNA progress endpoint.

Completed:
- Changed both profile and statistics content ScrollViewers to start collapsed and become visible only after their initial-loading state ends.
- Changed both loading overlays to start visible and collapse only after the corresponding initial-loading state ends, eliminating the pre-binding card flash.
- Increased the scrollbar hit-test width from 12 to 18 pixels while retaining the existing 6-pixel visual thumb.
- Moved keyword breathing from the inherited style-level load trigger to each concrete keyword-chip instance so every generated chip starts its animation reliably.
- Increased the cached keyword-shadow opacity, assigned score-specific shadow colors, and strengthened the cached-shadow opacity and scale pulse while keeping the card-opacity change above 0.92.
- Kept keyword shadows on the local cached bitmap path; the breathing animation transforms the cached visual and does not regenerate a blur resource per frame.
- Increased the taste-summary card bottom padding from 22 to 28 pixels.
- Increased the DNA endpoint indicator from 14/7 to 18/10 pixels and changed it from the blue progress fill to the themed accent color with a surface outline.

Explicitly not done:
- No application launch, profile prompt change, statistics query change, database update, migration, commit, or push was performed.
- No profile keyword score, DNA score, or progress-value semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for first-frame loading visibility, scrollbar drag acquisition, keyword breathing strength, and DNA indicator contrast in both themes.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Persona Typography and Quadrant Detail Refinement

Stage goal:
- Recompose the persona analysis into three centered rows, derive the persona-title treatment from the local poster palette, strengthen the persona-poster glow, and improve quadrant labels, explanation layout, and score discovery.

Completed:
- Added a lightweight local `PersonaPaletteText` control that renders the persona label with a mostly deep-ink vertical gradient, a small contribution from poster palette colors, a one-pixel light palette outline, and layered palette-colored projection shadows.
- Reused the existing local poster-palette extraction and cache through an invisible palette host; no remote color service or per-frame blur generation was introduced.
- Increased the persona label from 50 to 56 pixels and centered it in the first content row.
- Centered the lead in the second row and applied narrower hair-space character spacing than the persona label's thin-space spacing.
- Centered the body in the third row inside a 70%-width column while preserving the local two-character indentation and half-line scroll viewport behavior.
- Increased only the persona poster's palette glow padding, blur radius, and opacity to produce a stronger, more dispersed halo.
- Replaced the quadrant explanation title with a local axis-direction pair such as `熟悉安全 x 情绪沉浸`, derived from the signs of the existing X/Y coordinates.
- Added a two-line point tooltip showing the active horizontal and vertical direction scores from 0 to 100.
- Applied local two-character indentation to the quadrant body, centered its title and body, widened the explanation region, and shifted it slightly right.
- Vertically centered both horizontal-axis captions with the axis, moved them inward, and moved the top and bottom captions farther away from the axis endpoints.

Explicitly not done:
- No application launch, AI prompt change, profile schema change, database update, migration, commit, or push was performed.
- No persona classification, quadrant coordinate, or score calculation source semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for palette typography contrast, outline weight, poster glow spread, three-row balance, tooltip wording, and axis-caption alignment in both themes.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Watch-vs-Like True 3D Triptych Refinement

Stage goal:
- Replace the overlapping two-dimensional triptych approximation with perspective-correct WPF 3D cards, unify all rank outlines, and create enough vertical space for the conclusion panel.

Completed:
- Changed the frequently-watched rank wrapper from a filled badge to the same transparent accent-colored outline treatment used by the heart and star ranks.
- Replaced the `SkewTransform` and inward translation composition with a `Viewport3D` containing three interactive `Viewport2DVisual3D` card surfaces.
- Added a perspective camera and real Y-axis rotations so the left and right cards recede through foreshortening instead of becoming flat parallelograms.
- Positioned the three cards at separate X coordinates with visible gaps, removing the previous intentional overlap and card-to-card obstruction.
- Positioned the center card slightly forward on the Z axis and the side cards slightly behind it for the default hierarchy.
- Updated hover animation to flatten and move the active card forward while rotating, shrinking, dimming, and moving the other cards backward without changing their horizontal spacing.
- Preserved the semantic fold shade and cached-shadow opacity animation on the hosted card visuals.
- Increased the triptych viewport height from 256 to 320 pixels, set the outer module minimum height to 548 pixels, increased bottom padding, and moved the conclusion panel down with a 30-pixel top gap.

Explicitly not done:
- No application launch, ranking-data change, database update, migration, commit, or push was performed.
- No often-watched, liked, or wanted score semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for perspective strength, card spacing, hover depth transitions, mouse hit testing on tilted surfaces, and conclusion spacing at supported window sizes.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Statistics Overview Range Layout and Comparison Refinement

Stage goal:
- Align the statistics overview with the profile keyword language, add meaningful month-over-month watch-history comparisons, and give the all-time range a denser layout without changing business semantics.

Completed:
- Reused the profile core-keyword chip layout, score-based sizing, rotation, cached shadow, hover feedback, and per-instance breathing animation for the top-six frequent tags.
- Changed the favorite and want-to-watch solid icons to the same themed `BrushRatingStarFill` color used by existing filled preference-state icons.
- Split watch days into a metric number and a small `天` unit so it follows the same typography and alignment as the `部` metrics.
- Added in-memory snapshot fields and service calculations for watch-time and watch-day deltas versus the previous month; when no previous-month history exists, the cards display the existing unavailable-comparison wording.
- Reduced the watch-time metric from the shared 34-pixel metric style to a dedicated 22-pixel style.
- Added a small `共` prefix to every all-time count, watch-day, and watch-time value.
- Reduced all-time overview metric cards from 164 to 140 pixels, vertically centered values below the header, and allowed the second row and outer module to move upward by the same 24-pixel reduction.
- Vertically centered the watch-time and watch-day cards against the frequent-tag card in both ranges.
- Bumped the statistics logic version so older cached snapshots without the new comparison fields are recomputed.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.
- No watch-history validity threshold, status-count semantics, tag ranking, or profile prompt was changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed for the four implementation files before the documentation update.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for both themes, long all-time duration strings, month/all height transitions, comparison wording, and visual centering against the frequent-tag card.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Viewing Calendar Spatial Alignment Refinement

Stage goal:
- Rebalance the viewing-calendar content, legend, and summary-card positions without moving the module title or month-navigation controls.

Completed:
- Shifted only the weekday header, date grid, and calendar notice 16 pixels right while leaving the title, subtitle, month navigation, and legend row at their existing vertical position.
- Changed the legend from right alignment to left alignment exactly 24 pixels after the next-month button, and placed the conditional return-to-current-month button after the legend so it cannot alter that interval.
- Shifted the three calendar summary cards 24 pixels left and 22 pixels down as one group.
- Shifted each summary icon 6 pixels right and increased the icon-to-text spacing from 18 to 26 pixels, moving the text regions right with the icons.
- Increased each date cell's horizontal margin from 7 to 9 pixels while retaining the existing fixed seven-column grid and vertical spacing.

Explicitly not done:
- No application launch, calendar data calculation, navigation behavior, database update, migration, commit, or push was performed.
- No calendar card width, typography, tooltip, heat-level, or click semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for legend spacing when the return-to-current-month button is visible and for summary-card clearance at supported window widths.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Preference Bubble Roaming and Directional Water Interaction

Stage goal:
- Prevent long-running preference bubbles from collapsing toward the center and make pointer movement behave like a pen disturbing a broad water surface.

Completed:
- Shifted the three-item preference legend 18 pixels left without changing the title or subtitle position.
- Removed the shared center-attraction target that caused all bubbles to converge over time.
- Added independent, phase-shifted roaming targets and a low-frequency spatial flow field so each bubble traverses a different part of the canvas instead of oscillating around one center.
- Reduced the soft edge inset, increased velocity retention, and added non-contact personal-space repulsion before collision, allowing bubbles to use more of the canvas while remaining separated.
- Replaced the hard 180-pixel circular pointer cutoff with a broad elliptical directional field derived from pointer velocity, with strong near radial displacement, forward flow drag, and side separation around the pointer path.
- Added persistent directional flow-wave particles that propagate from near to far over 1.45 seconds and continue applying weaker physical force as their elliptical wavefront expands.
- Changed visual ripples from uniform circles to velocity-oriented ellipses whose length, width, origin offset, and duration respond to pointer speed and direction.
- Hid the system cursor only while it is over the bubble canvas using `Cursor=None` and `ForceCursor=True`; hit testing, hover expansion, tooltips, and mouse movement remain enabled.

Explicitly not done:
- No application launch, preference-bubble data calculation, database update, migration, commit, or push was performed.
- No bubble count, count-based radius formula, color semantics, state persistence, or hover-radius semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for long-duration distribution, directional wake strength, ripple density during rapid movement, tooltip usability with a hidden cursor, and frame pacing on lower render tiers.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Viewing Rhythm Spline, Donut Tooltip, and Layout Refinement

Stage goal:
- Increase viewing-time resolution, remove visible spline joins, restore reliable donut-segment tooltips, and refine the lower rhythm-card layout.

Completed:
- Replaced the seven coarse viewing-time ranges with twelve fixed two-hour ranges from `0-2` through `22-24` and bumped the statistics logic version so cached rhythm snapshots are rebuilt.
- Replaced the Catmull-Rom-like per-segment control calculation with a natural cubic spline solved through a tridiagonal system, providing continuous first and second derivatives at every segment boundary.
- Clipped the spline and filled area to the plot region so natural-spline overshoot cannot render beyond the baseline or chart top, while retaining the mathematically calculated peak marker.
- Increased the rhythm module bottom padding from 32 to 38 pixels.
- Shifted the weekday/weekend donut and legend region 22 pixels right.
- Placed each percentage directly after its weekday/weekend label with an 8-pixel gap and changed it to the same body-text style.
- Replaced dynamic assignment to the control's `ToolTip` property with an explicit mouse-positioned `ToolTip` popup, added whole-control hit testing, and close handling outside the ring.
- Shortened the duration-distribution progress tracks by increasing their left inset from 10 to 28 pixels while retaining the right inset.
- Confirmed the duration card currently has no card-level subtitle binding; the per-row actual-time values remain because they are part of the requested time-based distribution data.

Explicitly not done:
- No application launch, watch-duration bucket semantics, database update, migration, commit, or push was performed.
- No duration-distribution percentage calculation, weekday/weekend calculation, chart height, or peak calculation semantics were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for twelve-label readability, spline overshoot clipping, donut tooltip placement, shifted lower-card balance, and progress-track length at supported window widths.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Loading, Glass Chrome, Keyword Breathing, and DNA Indicator Polish

Stage goal:
- Align Watch Insights initial loading with the shared page loading presentation, protect title/tab readability over the poster-derived backdrop, strengthen keyword breathing, and remove DNA progress-indicator clipping.

Completed:
- Replaced both oversized Watch Insights loading indicators with a local `34x34` spinner that matches the shared visual language, uses a cached rotating layer, and requests a steady 60 fps animation cadence.
- Switched loading captions to the shared inline-loading text style and spacing used by other pages.
- Added theme-aware glass surfaces between the dynamic poster backdrop and the Watch Insights shell title bar/tab bar; light and dark themes resolve through their existing glass resources.
- Added a visible `0.98-1.045` card-scale cycle to profile/statistics keyword chips and strengthened their cached-shadow scale range while preserving staggered rotation and hover lift.
- Raised the DNA progress template to an 18px visual lane while retaining a 4px track, then rebuilt the endpoint as a borderless solid accent dot with a radial glow and breathing scale/opacity animation.

Explicitly not done:
- No application launch, data-service change, prompt change, database update, migration, commit, or push was performed.
- No global loading-spinner template or non-Watch-Insights title/tab chrome was changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed apart from existing line-ending notices.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for spinner smoothness under real loading pressure, glass contrast in both themes, keyword breathing strength, and DNA endpoint overflow at low/high progress values.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Persona Theme Text and Taste Quadrant Layout Refinement

Stage goal:
- Improve persona typography in dark theme and rebalance the persona/quadrant text and coordinate layouts without changing insight data semantics.

Completed:
- Added theme-aware persona-title rendering: dark theme now uses a mostly white-gray vertical gradient with a small poster-palette tint, a poster-derived dark 1-pixel outline, and stronger poster-colored projection/glow; light theme retains the existing deep-ink direction.
- Increased the persona body width from approximately 70% to 85%, kept the block centered, and changed the body to left-aligned text while preserving the local two-character paragraph indent.
- Rebuilt the taste-quadrant horizontal grid with three equal flexible gaps around the fixed text and coordinate regions so navigation-bar collapse width is distributed evenly across the left margin, middle gap, and right margin.
- Moved the coordinate system left, moved the text region right, increased the axis-pair title size, and increased the spacing before the quadrant body.
- Moved the horizontal-axis endpoint labels farther outward while keeping them vertically centered on the axis.
- Added a point-local tooltip delay of 80 milliseconds so only the quadrant coordinate point responds faster.
- Kept the quadrant body left aligned and locally indented while stretching its text area within the centered text region.

Explicitly not done:
- No application launch, AI prompt change, coordinate calculation change, data-service change, database update, migration, commit, or push was performed.
- No global tooltip timing or non-Watch-Insights typography was changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for persona contrast against varied poster palettes, projection strength in both themes, narrow-window quadrant spacing, and the faster coordinate-point tooltip.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Watch-versus-Like Triptych Overflow Fix

Stage goal:
- Stop the center card in the watch-versus-like triptych from being enlarged by perspective depth until it escapes the visible page.

Completed:
- Constrained the triptych to a maximum `1040x300` rendering stage and enabled clipping at both the stage and `Viewport3D` boundaries.
- Widened the perspective-camera field of view from 30 to 36 degrees so all three cards remain inside the module at supported widths.
- Reduced the center card's default scale from `1.03` to `1.01` and removed its forward Z offset.
- Reduced hover scale from `1.04` to `1.02` and limited the active card's forward Z offset from `0.38` to `0.08`, preventing perspective depth from multiplying the intended hover scale.
- Retained the side-card fold angles, dark overlays, opacity response, and card data content.

Explicitly not done:
- No application launch, triptych data change, card-template content change, database update, migration, commit, or push was performed.
- No other Watch Insights modules or global 3D rendering behavior were changed.

Validation:
- Static XAML/C# inspection confirmed that center-card scale and depth now return to the bounded defaults on mouse leave.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side runtime verification is required for card size, edge clipping, fold readability, and hover transitions at the actual application window width.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Statistics Overview and Calendar Spacing Refinement

Stage goal:
- Align the statistics overview metrics and make the calendar layout respond predictably when navigation width changes.

Completed:
- Removed the statistics frequent-tag `Viewbox` scaling layer and switched its canvas, tag-width formula, and anchor positions to the same `386x146` coordinate system used by profile core keywords while retaining count-based ranking and tooltips.
- Centered comparison text in all overview metric cards.
- Vertically centered the watch-duration and watch-day value rows using the same row alignment and top inset.
- Added equal left and right insets to both overview card rows, moving all metric cards toward the module center without changing their relative column proportions.
- Rebuilt the calendar body as three equal flexible gaps around a fixed 588-pixel calendar and a fixed 236-pixel summary-card column. Extra width from navigation collapse is therefore split equally between the module left edge/calendar, calendar/summary cards, and summary cards/module right edge.
- Kept the month-navigation and legend row in its existing position while shifting the calendar right beneath their combined visual center.
- Moved the three calendar summary cards left into the new fixed column and slightly lower relative to the control row.
- Shifted all three summary icons and their text regions right inside their cards.
- Increased calendar-cell horizontal spacing by two pixels per cell.

Explicitly not done:
- No application launch, statistics calculation change, calendar data change, database update, migration, commit, or push was performed.
- No non-Watch-Insights calendar or global metric-card styles were changed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed apart from existing line-ending notices.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for frequent-tag scale parity, comparison alignment, calendar centering, and equal-gap behavior with expanded/collapsed navigation.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Preference Bubble Inertia and Directional Ripple Refinement

Stage goal:
- Reduce excessive fluid resistance, prevent long-lived corner stalls, and make bubble weight and directional water-wave depth physically coherent.

Completed:
- Reduced frame damping from `0.982` to `0.99`, allowing momentum to decay more gradually instead of losing roughly two-thirds of its speed each second.
- Made pointer-velocity sampling more responsive while reducing direct radial, drag, and lateral pointer forces, producing a lighter interaction without binding bubbles too tightly to the pointer.
- Added a phase-shifted corner escape field that combines diagonal inward force with a small tangential component, preventing stable force equilibria from trapping bubbles in corners.
- Added a bounded radius-derived mobility coefficient. Large bubbles now react up to roughly 25 percent less than small bubbles, while the clamp prevents unrealistic weight differences.
- Applied mobility to external acceleration, collision impulses, and overlap correction so smaller bubbles yield more while larger bubbles retain more inertia.
- Changed visual ripple strokes to a movement-aligned gradient with a faint trailing edge and stronger leading edge.
- Added the same forward/backward depth asymmetry to the physical wave shell, making the force stronger ahead of pointer motion and softer in its wake.

Explicitly not done:
- No application launch, preference-bubble sizing formula, bubble persistence key, statistics data, database update, migration, commit, or push was changed.
- No third-party physics or rendering library was added.

Validation:
- Static inspection confirmed that mobility remains constrained to `0.84-1.12`, collision correction remains near full overlap separation, and ripple direction is derived from pointer velocity.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side runtime verification is required for damping feel, long-duration corner escape, large-bubble inertia, directional ripple readability, and frame pacing during rapid pointer movement.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Viewing Rhythm Baseline and Lower-Card Spacing Fix

Stage goal:
- Keep the viewing-time spline above its X-axis baseline and refine the duration and weekday/weekend horizontal layout.

Completed:
- Clamped every natural-spline Bezier control point to the plot's top and bottom bounds, preventing the mathematical curve from dipping below the zero baseline between valid data points.
- Increased the duration-label column from 108 to 154 pixels and removed character ellipsis so labels such as `中等 60-120min` remain fully visible.
- Kept the duration percentage column fixed while moving the progress-track start right and shortening its maximum available width.
- Shifted the weekday/weekend donut 14 pixels right.
- Increased the donut-to-legend spacing and added a separate legend inset, moving the legend substantially farther right than the chart.

Explicitly not done:
- No application launch, viewing-time data, duration-distribution calculation, donut ratio calculation, database update, migration, commit, or push was changed.
- No chart colors, tooltip content, card heights, or rhythm-card width split was changed.

Validation:
- Static inspection confirmed that the spline's Bezier convex hull cannot cross the bottom plot bound.
- `git diff --check` passed apart from existing line-ending notices.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side runtime verification is required for zero-baseline rendering, full duration-label visibility, progress-track length, and donut/legend balance at the actual window width.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Watch-versus-Like Triptych Projection Replacement

Stage goal:
- Eliminate the oversized single-card rendering caused by the `Viewport2DVisual3D` projection and guarantee that all three watch-versus-like cards remain visible.

Root cause:
- The previous implementation mapped three fixed-size WPF visuals onto identical 3D mesh planes. Their on-screen size was controlled by camera projection and mesh units rather than the declared card pixel dimensions, while depth ordering allowed one projected visual to cover the others.
- Reducing `ScaleTransform3D` values did not address that projection mismatch, so the center card could still occupy nearly the complete viewport.

Completed:
- Removed the `Viewport3D`, perspective camera, mesh geometry, 3D transforms, and `Media3D` dependency from the triptych.
- Added a fixed `960x280` stage inside a down-only `Viewbox`: wide layouts never enlarge the cards, while narrow layouts scale the complete three-card composition down uniformly.
- Fixed each card at `320x252` and positioned all three independently, with approximately 18 pixels of controlled overlap between the center and side cards.
- Recreated the folded-screen appearance using edge-based transform origins, horizontal compression, small `SkewTransform.AngleY` values, vertical offsets, dark fold overlays, opacity, shadow depth, and explicit `Panel.ZIndex` ordering.
- Rebuilt hover and reset animations around mutable 2D scale, skew, and translation transforms. The active card rises only slightly and cannot alter the stage or sibling layout dimensions.

Explicitly not done:
- No application launch, card content, insight data, prompt, database update, migration, commit, or push was changed.
- No other 3D or animation component was modified.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Static search confirmed that no `Viewport3D`, `Viewport2DVisual3D`, perspective-camera, or `Media3D` reference remains in the triptych implementation.
- `git diff --check` passed apart from existing line-ending notices.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for three-card visibility, overlap amount, fold-angle readability, hover Z-order, and narrow-window down-scaling.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Loading, Chrome Glass, Palette, and DNA Motion Refinement

Stage goal:
- Align the initial loading indicator with the shared application spinner, replace the solid Watch Insights chrome with continuous cached glass, strengthen the persona-poster palette, and refine the DNA progress/card motion layout.

Completed:
- Removed the page-local loading spinner template and switched both initial loading overlays to the shared `LoadingSpinnerTemplate`, matching the static ring and rotating arc geometry used by other pages.
- Removed the page-local forced 60-fps/cache spinner path; the shared spinner now uses the standard render-transform animation path.
- Added a cached `Glass` backdrop variant rendered as a low-resolution, broad-diffusion local bitmap. The title bar and tab strip reuse the same persona-poster palette without live blur effects.
- Joined the 56px title glass and the tab glass continuously; the tab glass covers its 48px header plus the following 8px down to the scroll-content clipping line.
- Added theme-specific high-transparency glass tint, sheen, and border resources for light and dark themes.
- Increased the Watch Insights `Vivid` palette saturation/brightness and raised its minimum channel floor so extracted colors remain more distinct and avoid overly dark results.
- Rebuilt the DNA progress indicator with an 18px unclipped indicator host, a 9px pale solid endpoint, and a smaller/lighter 16px glow.
- Increased the DNA module's three vertical breathing intervals equally by 18px: title-to-first-row, row-to-row, and second-row-to-card-bottom.
- Replaced the DNA `UniformGrid` with explicit 3-by-2 layouts. Expanded navigation uses 14px horizontal gaps; collapsed navigation uses 104px gaps, making each card 8px narrower while assigning the removed width to the two gaps.
- Replaced the six independent float animations with a planned travelling double-helix cycle: three column phases are separated by 120 degrees, the lower row is phase-inverted, and small horizontal orbit, depth scale, and shadow changes accompany the larger 7.2px vertical motion.

Explicitly not done:
- No application launch, prompt, watch-insight data contract, database update, migration, commit, or push was performed.

Validation:
- An isolated-output `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Focused `git diff --check` passed apart from existing line-ending notices.

Known Issues:
- Blocker: none identified by isolated solution build or static inspection.
- Deferred: user-side runtime verification is required for spinner smoothness under real loading, glass continuity/transparency, extracted palette intensity, DNA endpoint shape, and double-helix motion balance.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Persona Typography, Lead, and Body Overflow Refinement

Stage goal:
- Correct the persona-label gradient and theme shadow, enforce the two-clause lead contract, and keep long persona analysis text scrollable instead of truncating it.

Completed:
- Reversed the persona-label fill to run from a lighter top to a slightly darker bottom in both themes.
- Strengthened and moved the projected label shadow farther below the glyphs; dark theme now uses a light poster-derived shadow so it remains visible.
- Wrapped the lead with the `⌜` and `⌟` corner marks.
- Upgraded the persona prompt version and required a lead of at most 33 characters including punctuation, exactly one Chinese comma, two complete clauses, and a final Chinese period.
- Added local lead normalization so malformed or legacy AI output falls back to a contract-compliant sentence.
- Kept the expanded-navigation lead on one line with down-only scaling, while collapsed navigation displays the two comma-separated clauses on separate lines.
- Moved the persona label upward and the body downward slightly.
- Removed service-layer length truncation from the persona body so long analysis remains complete and is handled by the UI scroll region.
- Preserved the modern auto-reveal scrollbar and half-line overflow cue, increased the viewport to 6.5 text lines, and added bottom content padding so the final text remains reachable.

Explicitly not done:
- No application launch, database update, migration, commit, or push was performed.
- No other Watch Insights card or AI schema was changed.

Validation:
- The fallback lead contains 24 characters, exactly one Chinese comma, and one final Chinese period.
- Focused `git diff --check` passed apart from existing line-ending notices.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side runtime verification is required for theme shadow strength, single-line lead fit, collapsed two-line lead balance, and half-line body overflow behavior with a long AI response.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Taste Quadrant Semantic Axis and Analysis Layout Refinement

Stage goal:
- Align the taste-quadrant card with the shared module header layout, improve semantic readability of the coordinate system, and provide more complete AI analysis text.

Completed:
- Moved the `口味象限` title and subtitle into a full-width top row so their left and top offsets match the other large Watch Insights cards.
- Added four theme-aware semantic colors: blue for `熟悉安全`, teal for `新鲜探索`, amber for `轻松消遣`, and rose for `情绪沉浸`.
- Replaced the neutral axes with semantic gradients: familiar-to-explore on the X axis and emotion-to-relax on the Y axis; endpoint arrows use their corresponding terminal colors.
- Moved the left and right X-axis labels farther outward and expanded the coordinate canvas so neither label relies on drawing outside its bounds.
- Matched the `A标签 x B标签` heading to the persona-lead typography, including its 20px size, bold weight, line height, and local hair-space character spacing.
- Expanded the analysis column from the previous 340px target to a responsive maximum of 400px and moved the heading/body group downward.
- Made the lower layout responsive: the analysis and chart use proportional columns, and the chart scales down only when the available width is insufficient.
- Updated the quadrant prompt to request 110-170 Chinese characters across 2-3 complete sentences, covering both axes and concrete viewing-state evidence.
- Advanced the profile prompt version so cached profiles generated under the shorter quadrant contract are refreshed.

Explicitly not done:
- No application launch, data schema change, database update, migration, commit, or push was performed.
- No quadrant score calculation, tooltip conversion, or point animation logic was changed.

Validation:
- Focused `git diff --check` passed apart from existing line-ending notices.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side runtime verification is required for semantic color balance in both themes, axis-gradient direction, minimum-width chart scaling, text-column width, and the longer AI response after refresh.
- Noise: the running application must be restarted before it can load these changes.

## 2026-06-15 Watch-versus-Like Conclusion Bottom Alignment

Stage goal:
- Move the watch-versus-like conclusion downward and match its bottom spacing to the second DNA card row.

Completed:
- Replaced the card's top-stacked content layout with a four-row grid.
- Added a flexible spacer between the triptych and conclusion so the conclusion is anchored near the bottom instead of leaving unused space below it.
- Increased the large card's bottom padding from 38 to 54 pixels, matching the Watch DNA card's second-row-to-card-bottom spacing.
- Preserved the existing 30-pixel separation between the triptych region and the conclusion card as the minimum gap.

Explicitly not done:
- No application launch, triptych sizing, hover animation, conclusion text, database update, migration, commit, or push was changed.

Validation:
- A single-node isolated-output `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Focused `git diff --check` passed apart from the existing line-ending notice.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side runtime verification is required for the final conclusion position at normal and minimum window heights.
- Noise: the first parallel build hit a transient duplicate `ApiConfigCard.baml` resource-generation key; the immediate single-node rebuild passed. The running application must be restarted before it can load this change.

## 2026-06-15 Viewing Calendar Horizontal Balance and Metric Spacing

Stage goal:
- Refine the relative horizontal alignment of the calendar header, calendar grid, and right-side metric cards while increasing the metric-card breathing room.

Completed:
- Shifted the complete calendar grid 14 pixels left.
- Shifted the first-row month navigation, legend, and current-month action 6 pixels right, intentionally using a smaller movement than the calendar body.
- Shifted the right-side three-card metric group 20 pixels left.
- Moved the metric group's layout origin down by removing the previous 8-pixel negative top margin, then raised each individual card 4 pixels within its slot; the resulting first-card position is 4 pixels lower than before while preserving the requested local upward adjustment.
- Increased both vertical gaps between the three metric cards from 18 to 24 pixels.
- Used wrapper transforms for the local card lift so the existing card hover transform remains untouched.

Explicitly not done:
- No application launch, calendar data, navigation behavior, card content, hover animation, database update, migration, commit, or push was changed.

Validation:
- Focused `git diff --check` passed apart from the existing line-ending notice.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side runtime verification is required for calendar/header balance and the three-card group's final vertical rhythm.
- Noise: the running application must be restarted before it can load this change.

## 2026-06-15 Preference Bubble Boundary Physics, Far Ripples, and Surface Depth

Stage goal:
- Reduce the pointer interaction's damped feel, add physically coherent boundary rebound, extend visible wave propagation, and give the preference bubbles more dimensional surfaces.

Completed:
- Increased pointer-velocity sample responsiveness from an `0.22/0.78` history/current blend to `0.12/0.88` and reduced stale-pointer velocity decay from `0.90` to `0.94` per 60Hz step.
- Reduced particle motion damping from `0.99` to `0.993` per 60Hz step so bubbles preserve slightly more momentum after pointer interaction.
- Expanded the soft boundary spring zone from radius plus 28 pixels to radius plus 34 pixels and increased its force; the existing inverse-mass mobility now makes lighter bubbles respond more and heavier bubbles respond less.
- Replaced the fixed boundary velocity flip with a wall-collision impulse. Impulse magnitude scales with particle mass, restitution remains a shared material property, and a small tangential loss prevents indefinite edge sliding.
- Extended physical wave lifetime from 1.45 to 1.85 seconds, increased maximum travel to the full canvas diagonal, broadened the shell, and slowed far-field decay.
- Added a delayed far-field visual ring behind the near-field directional ripple. It travels substantially farther and is generated every second pointer sample, or every sample during fast motion, limiting visual-tree pressure.
- Rebuilt each bubble surface with a lightweight layered treatment: lower-volume shading, a bright-to-dark inner rim, angled specular highlight, and a local radial depth shadow.
- Kept all new dimensional layers free of live blur or drop-shadow effects and disabled their hit testing so existing bubble hover interaction remains intact.

Explicitly not done:
- No application launch, bubble sizing formula, state-persistence key, statistics data, database update, migration, commit, or push was changed.
- No third-party physics or rendering dependency was added.

Validation:
- A single-node isolated-output `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Focused `git diff --check` passed apart from the existing line-ending notice.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by build or static inspection.
- Deferred: user-side runtime verification is required for pointer responsiveness, light/heavy edge rebound, far-wave readability, bubble depth in both themes, and frame pacing during sustained rapid pointer motion.
- Noise: the running application must be restarted before it can load this change.

## 2026-06-15 Viewing Rhythm Baseline Stroke and Horizontal Insets

Stage goal:
- Prevent zero-level spline segments from being visually clipped and make small horizontal refinements to the duration and weekday/weekend panels.

Completed:
- Moved the spline's mathematical zero baseline 2 pixels above the former clip boundary.
- Updated zero-value points, natural-spline control-point bounds, area closure, and peak clamping to use the same inset baseline, keeping the complete curve mathematically above zero.
- Extended only the drawing clip by the reserved 2-pixel stroke allowance, allowing the full 3-pixel line stroke to render without exposing geometry below the zero domain.
- Shifted the duration bucket's label/time stack 6 pixels right without changing its column width or the progress-track position.
- Shifted the complete weekday/weekend chart-and-legend content group 8 pixels right, preserving their internal spacing.

Explicitly not done:
- No application launch, rhythm data, duration calculations, donut ratios, tooltip content, database update, migration, commit, or push was changed.

Validation:
- Focused `git diff --check` passed apart from existing line-ending notices.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by static inspection.
- Deferred: user-side runtime verification is required for full zero-line visibility, the small duration-label inset, and the final donut/legend horizontal balance.
- Noise: the running application must be restarted before it can load this change.

## 2026-06-15 Watch Insights Spinner Alignment and First-Entry Loading Pacing

Stage goal:
- Align Watch Insights initial-loading spinner with the shared page spinner, remove the Watch Insights title/tab glass overlays, and revert the previous poster-palette softening.

Root-cause findings:
- The shared spinner used a full-size `Ellipse` for the static ring but an unbounded `Path` arc for the animated ring; WPF laid out the path from its geometry bounds instead of the same 34x34 coordinate box, so the animated arc could look off-center against the static ring.
- Local reproduction logs showed first-entry profile loading had one slow frame of about 460 ms while `initialLoading=profile`; profile service plus projection completed in roughly 150-180 ms, so the visible stall was dominated by first WPF page/render work rather than profile projection.
- The page also still queued heavyweight visual-tree layout diagnostics while initial loading could be active; those diagnostics are now skipped during initial loading and run after the content is available.

Completed:
- Fixed `LoadingSpinnerTemplate` so the static ring and animated arc share the same 34x34 coordinate system and center point.
- Restored the Watch Insights initial loading controls to 34x34, matching the common full-page loading size used by other pages.
- Added a render-priority yield immediately after `IsLoadingProfile` / `IsLoadingStatistics` flips true, with `initial-loading-render-yield` diagnostics for future reproduction.
- Skipped visual-tree layout diagnostics while initial loading is visible; the existing slow-frame and frame-summary diagnostics remain active.
- Removed the Watch Insights tab-strip glass bitmap/tint layer.
- Removed the Watch Insights title-bar glass bitmap/tint layer and let the normal poster-backdrop top-bar styling apply.
- Reverted the previous poster palette softening by restoring Watch Insights route palette mode to `Vivid` and restoring the prior vivid-color boost parameters.

Explicitly not done:
- No database schema, data migration, database update, AI prompt, statistics calculation, media-library behavior, commit, or push was changed.
- No WPF window was launched by Codex; runtime visual confirmation is left for manual acceptance.

Validation:
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- `git diff --check` passed apart from existing LF-to-CRLF working-copy warnings.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: user-side WPF reproduction should confirm spinner centering, first-entry animation smoothness, removed title/tab glass overlays, and restored poster color intensity.
- Noise: existing `logs/watch-insights-perf.log` entries before this change will still contain the older slow-frame data; restart the running app before validating the new spinner and render-yield diagnostics.

## 2026-06-15 Watch Insights Persona Palette Resource

Stage goal:
- Remove Watch Insights runtime persona-poster palette extraction by making the fixed persona poster colors a permanent application resource.

Root-cause findings:
- The formal persona poster set is 23 persona folders and 46 gendered poster PNGs.
- `eclectic_omnivore` is the redundant legacy persona folder and is not part of the legal persona set.
- Root-level placeholder files such as `1男.png` / `1女.png` and the `Frames` folder are not formal persona folders.
- The previous Watch Insights backdrop path still decoded the persona poster through a hidden shell image, then extracted a vivid palette at runtime before applying the cached backdrop.

Completed:
- Added a fixed `PersonaPosterPaletteResource` table for the 23 official persona keys and both `female` / `male` poster variants.
- Excluded `eclectic_omnivore` from the fixed palette resource and kept legacy `童心奇想家` fallback mapped to `animation_narrative_fan` only in the ViewModel.
- Added a `PaletteOverride` path to the cached poster backdrop behavior so Watch Insights can apply the fixed palette without waiting for hidden image extraction.
- Changed the shell Watch Insights backdrop source to empty and bound its background to the fixed persona palette override.
- Changed the persona display title to bind directly to the fixed persona palette and removed the page-local 1x1 hidden palette extraction image.
- Added a guard so the generic image palette behavior exits when its target already has a palette override.

Explicitly not done:
- No database schema, data migration, AI prompt, legal persona expansion, media-library behavior, commit, or push was changed.
- No WPF window was launched by Codex; runtime smoothness and final color perception remain manual acceptance items.

Validation:
- Verified the current `Assets/WatchPersonas` formal directory count is 23 excluding `Frames`.
- Verified the current gendered poster PNG count is 46.
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build or static resource count checks.
- Deferred: user-side WPF reproduction should confirm first-entry spinner smoothness and that fixed persona colors match the expected visual tone.
- Noise: older performance log entries still include the runtime palette-extraction path; restart the running app before comparing new logs.

## 2026-06-15 Watch Insights Loading Chrome Effects and Backdrop Pacing

Stage goal:
- Audit the Watch Insights loading spinner differences without changing the spinner body, add the requested title/tab chrome edge effects, and prevent loading from showing the previous poster backdrop color.

Root-cause findings:
- Watch Insights uses the shared `LoadingSpinnerTemplate`, whose ring color comes from `BrushBorder` and arc color comes from `BrushAccent`.
- The loading label uses the shared `InlineLoadingStyle`, whose text color comes from `BrushLoadingForeground`.
- Watch Insights still hosts its initial loading spinner at 38x38, while the Home full-page initial loading and Favorites loading template use 34x34; that host-size difference can make the same shared template look different.
- First-entry repro logs still show a slow frame before ViewModel activation: for example `initialLoading=profile frameMs=435` before `activate-start`; service/cache work then completed around 101 ms and projection around 14 ms, so the first visible stall is dominated by WPF page construction/layout/rendering rather than AI or profile projection.
- After initial loading hides, logs also show large layout/render frames such as 301 ms and 817 ms while content becomes visible, consistent with the heavy Watch Insights visual tree being materialized at once.
- The backdrop could briefly show a previous route/poster color while the new cached backdrop bitmap was generated asynchronously.

Completed:
- Left the spinner template and Watch Insights spinner host size unchanged for this pass.
- Added theme chrome stroke/shadow resources using light-mode white stroke/shadow and dark-mode black stroke/shadow.
- Applied the effect to the collapsed-titlebar subtitle, Watch Insights selected tab text/underline, the tab right-side refresh status text, the shell titlebar separator line, and the Watch Insights tab-bottom divider.
- Added an immediate lightweight palette-gradient fallback before asynchronous cached backdrop bitmap generation, preventing old poster colors from remaining visible during loading.
- Updated the reused profile metadata path so persona poster palette changes still notify the shell backdrop override.

Explicitly not done:
- No spinner animation geometry, spinner size, AI prompt, database schema, database update, migration, commit, or push was changed.
- No WPF window was launched by Codex; runtime visual confirmation remains manual.

Validation:
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: if the spinner still stutters after this pass, the likely next fix is to split Watch Insights content creation so the initial loading overlay is hosted in a lighter shell and profile/statistics content is materialized after the first render.
- Noise: existing performance logs include runs from before this change; restart the running app before comparing backdrop pacing and chrome effects.

## 2026-06-15 Watch Insights Chrome Scope and Backdrop Preflight

Stage goal:
- Narrow the requested chrome edge treatment to the intended text/line targets and make Watch Insights loading use the current fixed persona palette before page activation.

Completed:
- Removed the Watch Insights projection effect from the whole shell titlebar; the titlebar/tab separator now uses a direct theme line color only.
- Kept the collapsed-titlebar subtitle and tab right status text on text-only zero-offset edge treatment, with no directional shadow depth.
- Changed Watch Insights tab buttons to use a local template so both selected and mouse-hover text highlight states receive the same text edge treatment without affecting other pages.
- Reduced the light-theme white edge alpha and added separate Watch Insights separator line colors: whiter in light theme and blacker in dark theme.
- Added `PrepareBackdropPaletteAsync()` to read cached Watch Profile recommendation context before Watch Insights is made current, then apply the permanent persona palette override without decoding the persona poster.
- Added navigation version checks so an older async Watch Insights preflight cannot overwrite a later navigation target.

Explicitly not done:
- No database schema, migration, AI prompt, recommendation logic, commit, or push was changed.
- The whole Watch Insights visual tree was not yet split into deferred profile/statistics templates; that remains a larger refactor because the page has named scroll viewers, bubble canvas, and triptych animation elements referenced by code-behind.

Validation:
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- `git diff --check` passed apart from LF-to-CRLF working-copy warnings.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: user-side WPF reproduction should confirm whether the cache-only backdrop preflight plus initial render yield is enough to remove the visible first-entry spinner hitch; if not, split the statistics tab and bubble graph into deferred content templates next.
- Noise: older `logs/watch-insights-perf.log` entries still contain pre-change slow-frame evidence; restart the app before validating the new preflight logs.

## 2026-06-16 Watch Insights Text Tone and Staged Visual Tree

Stage goal:
- Refine Watch Insights title/tab chrome without outlining the page title and replace the monolithic first-entry visual tree with render-friendly staged module materialization.

Completed:
- Explicitly disabled effects on the shell page title so `观影洞察` never receives the subtitle/tab edge treatment.
- Added dedicated muted-text and accent-text foreground resources: light theme slightly blends the original colors toward white, while dark theme slightly blends them toward black.
- Increased the zero-offset text edge radius while reducing the light-theme white edge alpha.
- Reduced the separator extremes so light-theme lines are less white and dark-theme lines are less black.
- Moved the four profile module groups and five statistics module groups into deferred `DataTemplate` resources.
- Kept the tab shell, scroll containers, and loading overlays lightweight at first render.
- Materialized profile modules in four batches and statistics modules in five batches, yielding to the WPF render dispatcher and adding short non-blocking pauses between batches.
- Kept statistics visuals unmaterialized until the statistics tab is activated.
- Replaced deferred-template generated-field dependencies with cached visual lookup for the preference bubble canvas and watch-vs-like cards; card transforms now come directly from each card's `TransformGroup`.
- Added per-stage diagnostics: `visual-tree-stage-ready` and `visual-tree-ready`.

Explicitly not done:
- No spinner geometry, spinner color, business logic, AI prompt, database schema, database update, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: runtime verification should compare first-entry slow-frame diagnostics before and after staging, especially on systems where bitmap shadow generation is expensive.
- Noise: the first time each deferred module is created it still has a finite UI-thread construction cost; the change bounds that cost to one module group per batch instead of constructing both tabs at once.

## 2026-06-16 Persona Lead Length, Brackets, and Painted Label

Stage goal:
- Extend persona lead generation to 40 characters and refine the persona header with responsive quotation brackets and a poster-palette painted text background.

Completed:
- Bumped the Watch Profile prompt version to `wi-profile-persona-23-parallel-v15-lead-40`.
- Changed the primary persona prompt, type-explorer repair prompt, and local normalization ceiling from 33 to 40 characters.
- Increased both single-line and two-line bracket sizes and darkened their muted-theme brush slightly.
- Adjusted bracket placement so the opening mark sits higher and the closing mark sits lower while still following the measured first and last visible characters.
- Added a code-rendered five-stroke paint layer behind the persona label, dynamically sized from the formatted text instead of using a fixed rectangle.
- Selected the most vivid current persona poster palette color by chroma and derived separate pale light-theme and deep dark-theme variants.
- Kept the label text and its existing outline/projection above the new paint layer.

Explicitly not done:
- No persona taxonomy, recommendation behavior, database schema, database update, migration, bitmap asset, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: verify brush density, bracket offsets, and label readability in both themes and both sidebar states on the running WPF application.
- Noise: the prompt version bump intentionally invalidates old cached profile output so a refreshed profile can use the 40-character lead contract.

## 2026-06-16 Quadrant, Calendar, And Preference Wave Refinement

Stage goal:
- Refine the quadrant and calendar composition, lengthen quadrant analysis, and add a stronger periodic wave to the preference bubble graph.

Completed:
- Changed the Watch Profile prompt version to `wi-profile-persona-23-parallel-v16-quadrant-detail`.
- Increased quadrant description guidance to 150-220 Chinese characters in 3-4 complete sentences, including how multiple behavior signals combine into the final quadrant position.
- Increased the left quadrant content track and maximum body width toward the center without adding a left offset.
- Moved the coordinate view from a -18-pixel to a -32-pixel top margin.
- Moved the calendar navigation row right by another 8 pixels and its legend right by an additional 8 pixels.
- Moved the calendar metric card stack right by 8 pixels and each card upward by another 6 pixels.
- Added an alternating left/right vertical wave front on an approximately eight-second cadence.
- Kept the wave front full-height, moved it across the graph over 4.2 seconds, and faded its three-line visual representation as it travelled.
- Added a matching Gaussian directional force with a decaying strength that is stronger than the pointer force.
- Reduced the mouse ripple ellipse ratio while preserving its direction-aware behavior.
- Kept wave scheduling inside the existing composition-frame physics loop and reset it when the graph is cleared or hidden.

Explicitly not done:
- No statistics calculations, preference taxonomy, database schema, database update, migration, commit, or push was changed.
- No remote or background wave field was added.

Validation:
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: verify final quadrant balance, calendar alignment, wave visibility, wave force, and pointer ripple roundness in the running WPF application.
- Noise: the longer quadrant prompt version intentionally invalidates older cached profile output when a profile refresh is performed.

## 2026-06-16 Flat Watch-vs-Like Focus Cards

Stage goal:
- Replace the watch-versus-like pseudo-perspective interaction with a flat, modern overlapping-card focus treatment.

Completed:
- Simplified the card control template to a plain content presenter and removed the folded shade overlay.
- Removed all watch-versus-like `SkewTransform` instances and angle animation code.
- Positioned three 320-pixel cards on a 936-pixel flat canvas with 24-pixel adjacent overlap.
- Set `经常喜爱` as the default primary card using ZIndex 3, scale 1.04, full opacity, full shadow visibility, and a 10-pixel lift.
- Set default side cards to scale 0.94, opacity 0.68, reduced shadow visibility, and an 8-pixel lower position.
- Added left-primary order `left=3, center=2, right=1` and right-primary order `left=1, center=2, right=3`.
- Used scale 0.96 / opacity 0.74 for the intermediate card and scale 0.93 / opacity 0.62 for the far card.
- Switched ZIndex before starting a 220-millisecond cubic ease-out transition for scale, opacity, shadow visibility, and translation.
- Restored the middle-primary state when the pointer leaves the entire triptych area.

Explicitly not done:
- No ranking content, profile conclusion, AI prompt, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed that no watch-versus-like skew, fold shade, perspective camera, mesh, or 3D surface remains.
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: verify hover stability across overlap boundaries, shadow balance, and all three stacking orders in the running WPF application.
- Noise: ZIndex changes are intentionally immediate at transition start because WPF panel layers are discrete rather than interpolated values.

## 2026-06-16 Taste Combination Graph And Top5 Refinement

Stage goal:
- Simplify taste-combination labels, make link weight data-driven, add node focus interaction, and reduce the ranking to the five strongest combinations.

Completed:
- Removed the graph frequency legend from the lower-left area.
- Replaced the fixed 0.55 link opacity and previous maximum-only thickness formula with visible-set min/max normalization.
- Applied inactive opacity `0.18 + 0.45 * normalizedCount` and thickness `2 * (1 + 1.5 * sqrt(normalizedCount))`, producing exact 0.18-0.63 opacity and 2-5 pixel thickness ranges.
- Used normalized value 1 when all visible link counts are equal, avoiding division by zero while treating equal links consistently.
- Added hover and click-selection focus; connected links render at 0.9 opacity, hover overrides selection temporarily, and clicking the selected node clears it.
- Replaced cached-shadow graph nodes with plain circular borders containing only the label.
- Added `标签：次数` tooltips and increased vertical node gaps to 20 pixels.
- Added separate theme colors for type, emotion, and scene labels in both light and dark themes, without node shadows.
- Changed the right-side heading and projected collection from Top10 to Top5.
- Changed combination labels to rounded rectangles separated by literal `x` text and changed the right occurrence label from `出现 x 次` to `x次`.
- Cleared graph hover/selection state when the page data context changes or the page unloads.

Explicitly not done:
- No combination aggregation, profile prompt, database schema, database update, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed by build.
- Deferred: verify circle text fit, tooltip timing, focus-line visibility, and right-side label wrapping in both themes.
- Noise: WPF line opacity focus is applied directly to realized graph visuals; the base opacity remains data-bound for initial and rebuilt graph states.

## 2026-06-16 Watch Insights Entry Crash Fix

Stage goal:
- Fix the crash observed when clicking into Watch Insights after staged visual-tree loading changes.

Root-cause evidence:
- Windows Application log showed `.NET Runtime` event 1026 for `MediaLibrary.App.exe`.
- The unhandled exception was `System.Windows.Markup.XamlParseException`.
- Inner exception: `Unable to cast object of type 'MS.Internal.NamedObject' to type 'System.Windows.DataTemplate'`.
- The stack failed in `ContentControl.OnContentTemplateChanged` during WPF template measurement, matching the staged `ContentControl.ContentTemplate` default setters.
- After removing the template setters, the next runtime event exposed a second XAML parse failure: `StaticResourceHolder` could not find `WatchInsightsProfileStage3Template`.
- That second failure came from `WatchInsightsProfileStage2Template` referencing profile stage 3/4 templates before those resources are declared in the page resource dictionary.

Completed:
- Removed all `ContentTemplate="{x:Null}"` style setters from Watch Insights staged `ContentControl` placeholders.
- Removed all remaining Style/DataTrigger setters that wrote `ContentTemplate`.
- Moved each stage template reference onto its owning `ContentControl` directly and kept the stage trigger responsible only for `Visibility`.
- Changed the nested profile stage 3 and stage 4 template references inside stage 2 to `DynamicResource`, so WPF resolves them after the later template resources are available.
- Left visibility staging behavior unchanged, so collapsed stages still do not participate in layout or rendering.

Explicitly not done:
- No visual layout, profile/statistics data, AI prompt, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed no `ContentTemplate="{x:Null}"` remains in `WatchInsightsPage.xaml`.
- Static search confirmed no `Setter Property="ContentTemplate"` remains in `WatchInsightsPage.xaml`.
- Static search confirmed nested profile stage 3/4 placeholders use `DynamicResource` for the forward resource references.
- `dotnet build MediaLibrary.sln -m:1`
- Result: 0 warnings, 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none confirmed after the targeted XAML resource fix and solution build.
- Deferred: user-side runtime validation should confirm clicking Watch Insights no longer crashes and staged loading still shows modules.
- Noise: older Windows Event Viewer entries and `watch-insights-perf.log` entries still contain the pre-fix crash/slow-frame evidence.

## 2026-06-16 Watch Insights Loading Prelayout Smoothness

Stage goal:
- Reduce the remaining first-entry loading spinner hitch by moving the heavy first layout/render work behind the loading overlay instead of after it disappears.

Root-cause evidence:
- The latest performance log showed the new profile visual-tree split was active with `stage=1/7` through `stage=7/7`.
- The staged steps completed quickly while loading was visible, but slow frames appeared after `initialLoading=none`, including approximately 522 ms and 1456 ms frames.
- This matched the XAML structure: the profile and statistics `ScrollViewer` bodies were still `Collapsed` during initial loading, so the staged content flags did not force real layout/render work until the overlay was hidden.

Completed:
- Kept the profile and statistics `ScrollViewer` bodies visible during initial loading so they participate in layout/render while the spinner remains on screen.
- Made those bodies nearly transparent during loading and disabled hit testing until initial loading finishes.
- Replaced the previous fixed 34 ms stage gap with per-tab stage pacing: a longer profile interval and a moderate statistics interval.
- Kept the staged template split and final loading-state commit behavior intact.

Explicitly not done:
- No visual template content, AI prompt, profile data, statistics data, database schema, database update, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1 -p:OutDir=<temp>` passed with 0 warnings and 0 errors after clearing prior Codex temp build output directories.
- Pending user-side runtime validation with a fresh `watch-insights-perf.log` sample.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: verify the loading body is not perceptible behind the spinner in both themes.
- Deferred: verify new logs show any remaining expensive frames occurring while loading is still visible rather than immediately after `initial-loading-hidden`.
- Deferred: if a single stage still creates a visible hitch, split the corresponding template content one level deeper instead of only increasing timing.
- Noise: first entry can take slightly longer in wall-clock time because the stage gaps now intentionally give the render loop more chances to draw spinner frames.

## 2026-06-16 Watch Insights Profile Stage2 Persona Split

Stage goal:
- Reduce the remaining mid-loading hitch after the prelayout fix by splitting the heaviest profile visual stage more narrowly.

Root-cause evidence:
- After the prelayout fix, the latest profile trace showed first-entry loading was much smoother, but `profile stage=2/7` still spent about 1265-1478 ms inside the render-yield path.
- The stage-2 template still instantiated both the `观影 DNA` module and the full `你的观影人格` module in one visual-tree burst.
- The persona module includes poster images, cached palette shadow hosts, frame image, painted persona title, bracket positioning behavior, and a scrollable description, making it a likely source for the remaining single-stage hitch.

Completed:
- Split Profile stage 2 so it only materializes the `观影 DNA` module.
- Moved `你的观影人格` into its own Profile stage 3 template without changing its visual content.
- Promoted `口味象限` to Profile stage 4 and the watch-vs-like shell to Profile stage 5.
- Shifted the three watch-vs-like cards to Profile stages 6, 7, and 8.
- Updated the Profile visual-tree ready gate to wait for stage 8 plus the final render commit.

Explicitly not done:
- No persona visual styling, card layout, AI prompt, profile data, statistics data, database schema, database update, migration, commit, or push was changed.

Validation:
- Static inspection confirmed no `ContentTemplate="{x:Null}"` or `Setter Property="ContentTemplate"` was reintroduced.
- Static inspection confirmed profile stage templates are now 1-5 and watch-vs-like cards use stages 6-8.
- `dotnet build MediaLibrary.sln -m:1 -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- Pending user-side runtime validation with a fresh `watch-insights-perf.log` sample.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: verify fresh logs reduce the previous 1265-1478 ms Profile stage-2 render-yield spike.
- Deferred: if the new persona-only stage still has a visible hitch, the next split should separate poster/shadow image materialization from the text/card body.
- Noise: Profile first-entry loading now has one extra stage, so total loading duration may increase slightly while spinner smoothness improves.

## 2026-06-16 Watch Insights Persona Lead And Tag Paint Polish

Stage goal:
- Fix the `你的观影人格` lead-line bracket presentation and persona tag paint backdrop based on visual review feedback.

Completed:
- Replaced the separate lead-line corner bracket positioning behavior with inline `Run` brackets inside the lead `TextBlock`, so expanded and collapsed sidebar layouts no longer rely on overlay alignment.
- Kept the lead text style unchanged while giving the corner brackets their own font, size, weight, and Watch Insights accent brush.
- Fixed the inline lead `Run.Text` bindings to use explicit `Mode=OneWay`; Windows Application events showed the default binding mode tried to update read-only ViewModel properties and caused a `XamlParseException` crash on entering Watch Insights.
- Removed the unused corner bracket behavior helper after the XAML no longer referenced it.
- Reworked the persona type paint backdrop into heavier right-up to left-down diagonal strokes that extend across the label, with higher opacity, taller coverage, and rougher edge strokes.

Explicitly not done:
- No AI prompt, profile data, statistics data, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed `TextCornerBracketBehavior` and the old bracket element names are no longer referenced.
- Static search confirmed the persona lead brackets now use `ProfileLeadBracketRunStyle` and the inline lead text bindings use explicit `Mode=OneWay`.
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify entering Watch Insights no longer crashes, then verify the inline brackets and thicker persona tag paint in both light and dark themes, and in both expanded and collapsed sidebar layouts.
- Noise: visual preference for the exact brush thickness and bracket font may need one more review pass after seeing the running WPF page.

## 2026-06-16 Quadrant Calendar And Preference Wave Follow-up

Stage goal:
- Apply the requested polish pass for `口味象限`, `观影日历`, and `偏好图谱` without changing statistics semantics or database state.

Completed:
- Raised the taste-quadrant axis title/body group, added an extra upward offset when the sidebar is expanded, and moved the coordinate canvas further upward.
- Centered the taste-quadrant `A 标签 x B 标签` line against the body text readable area.
- Shortened the quadrant prompt target from 150-220 Chinese characters across 3-4 sentences to 130-190 Chinese characters across 2-3 sentences.
- Bumped the Watch Profile prompt version to `wi-profile-persona-23-parallel-v17-quadrant-brief`.
- Reworked the viewing-calendar month navigation into a row centered over the calendar grid, with larger spacing between previous month, month text, and next month.
- Moved the calendar heat legend below the calendar grid and centered it on the calendar width; kept `回到当前月` aligned to the right of that calendar width when visible.
- Raised the three calendar metric cards as a group.
- Replaced the preference-bubble full-width left/right sweep with randomized local wave emitters; each wave chooses a random zone, expands from that origin, and applies localized radial/curved force.

Explicitly not done:
- No statistics query, watch-history semantics, recommendation logic, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed the new quadrant prompt version and 130-190 character rule are present.
- Static search confirmed the preference wave now uses randomized emitter origins and no longer references the old left/right sweep toggles.
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify quadrant vertical spacing in expanded and collapsed sidebar modes, especially the axis-title/body alignment.
- Deferred: manually verify the calendar navigation, lower legend, and raised metric cards remain aligned at the running window width.
- Deferred: manually verify the new local preference waves feel random, visible, and not too small without pushing bubbles into edges.
- Noise: the new prompt version intentionally marks older cached profiles as older-rule output until a manual profile refresh regenerates the quadrant copy.

## 2026-06-16 Watch Duration Overcount Diagnosis Follow-up

Stage goal:
- Diagnose and prevent future Watch Insights viewing-time overcounts caused by player persistence, without rewriting existing history rows.

Completed:
- Confirmed the reported movie has many incomplete history rows whose statistic-eligible saved durations sum far above the maximum playback position.
- Updated player persistence so new `DurationWatchedSeconds` values are accumulated from playback-position progress instead of wall-clock session age.
- Kept Watch Statistics query semantics unchanged for existing stored rows.

Explicitly not done:
- No Watch Statistics query rewrite, cache cleanup, database update, migration, commit, or push was performed.
- Existing inflated history rows were not edited.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- Migration diff remained empty.
- Read-only inspection: 28 rows, statistic-eligible saved duration 4505 seconds, maximum playback position 919 seconds.

Known Issues:
- Blocker: none identified by build.
- Deferred: current totals that already include inflated history rows will remain inflated until an explicit data cleanup is approved.
- Noise: Watch Insights cache may need a manual refresh after future playback writes or any approved data cleanup.

## 2026-06-16 Watch Insights Visual Polish Follow-up

Stage goal:
- Apply the requested visual polish pass for Watch Insights DNA, preference graph, watch-vs-like, taste combination, rhythm chart, ranking medals, and persona title rendering without changing statistics semantics.

Completed:
- Shortened the `探索基因` compass needle geometry and increased the scrollable DNA description viewport/padding so visible text lines are not clipped halfway.
- Restored visible preference-graph wave/ripple drawing by raising the runtime wave visuals above bubble hosts while keeping bubble hover state on the top layer.
- Moved the watch-vs-like insight conclusion slightly upward.
- Moved the taste-combination right Top5 card left/down, shifted the `x次` count left, and kept selected graph links at full opacity.
- Rebalanced the viewing-time rhythm chart grid and color stops so the low-to-axis interval matches the low-to-middle interval.
- Removed the acrylic paint backdrop from the persona title text renderer; the persona type now renders as text only.

Explicitly not done:
- No statistics query, AI prompt, profile data, recommendation logic, database schema, database update, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify the running Watch Insights page for DNA card scrolling, preference wave visibility, taste-combination spacing, rhythm chart spacing, and persona title rendering in both themes.
- Noise: the preference wave visual now draws above bubbles at low opacity; if it feels too prominent in the running app, tune opacity rather than returning it behind the bubbles.

## 2026-06-16 Watch Insights Dark Palette And Preference Wave Polish

Stage goal:
- Apply the requested dark-theme fill lightening, taste-combination alignment, and preference-wave behavior changes without changing Watch Insights data semantics.

Completed:
- Lightened dark-theme fill colors used by Watch Insights keyword chips, DNA tags, statistics semantic icons, high-frequency tags, calendar heat cells and legends, calendar side semantic icons, preference bubbles and legends, weekday/weekend icons, and taste-combination nodes/ranks/labels.
- Reworked the preference-graph ambient wave from local circular emitters into a straight wave-front sweep that randomly starts from one of eight fixed edge/corner placements and travels across the content area while fading out.
- Reduced mouse ripple visual size and pointer-force radius, and increased forward/back alpha contrast in the ripple brush.
- Shifted the taste-combination Top5 occurrence count to the right when the sidebar is expanded while keeping the collapsed-sidebar position unchanged.

Explicitly not done:
- No statistics query, watch-history semantics, recommendation logic, AI prompt, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed old sweep curvature/spread fields are no longer referenced.
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify the running Watch Insights page in dark mode for the lighter fills across all listed components.
- Deferred: manually verify the straight preference wave is visible from all eight edge/corner placements and fades before it reaches the opposite side.
- Noise: the exact wave cadence and mouse ripple contrast are still visual-tuning values and may need one more runtime review pass.

## 2026-06-16 Watch Insights Light Tab, Wave Sync, Persona Shadow Polish

Stage goal:
- Apply the follow-up visual polish for Watch Insights dark semantic fills, light-theme tab text, DNA text scrolling, preference-wave sync, taste-combination count placement, medals, and persona title/lead styling.

Completed:
- Further lightened dark-theme semantic fill resources used by statistics overview icons, calendar side icons, preference bubbles and legends, weekday/weekend icons, and taste-combination nodes/tags/ranks.
- Removed the light-theme tab-highlight text outline by making the tab chrome stroke transparent in the light resource dictionary.
- Changed the DNA card scrollable description viewport to an exact three-line height so it does not expose a half line when scrolling is needed.
- Synchronized preference-wave visual travel with particle force travel by removing the visual sweep easing and using linear wave progress for both.
- Slightly reduced mouse ripple frequency while increasing mouse ripple visual/force radius a little.
- Moved the taste-combination Top5 occurrence count farther right in expanded-sidebar mode.
- Restored persona type text shadow; dark mode derives the shadow color from the poster palette light color.
- Removed terminal period display from the persona lead and made the lead brackets larger, darker in light theme, and lighter in dark theme.
- Raised rank medal numbers slightly.

Explicitly not done:
- No statistics query, watch-history semantics, recommendation logic, AI prompt, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed the preference sweep no longer references the previous eased visual travel path.
- Static search confirmed the new lead bracket theme color and home/profile visual resources are wired.
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify the running Watch Insights page in both themes, especially the lighter dark fills, tab highlight, DNA scrolling, preference wave timing, and persona title shadow.
- Noise: exact dark-fill lightness and persona shadow strength remain visual-tuning values that may need one more runtime review pass.

## 2026-06-16 Watch Insights Tab Effect And Persona Lead Follow-up

Stage goal:
- Apply the follow-up corrections for light-theme tab outline removal, dark semantic fills, DNA scroll height, taste-combination count offset, medal digit offsets, and persona lead alignment.

Completed:
- Added a theme-controlled tab text effect opacity resource, with light theme set to zero so selected/hover tab text has no outline effect.
- Matched the dark-theme success background to the preference-graph green fill and lightened the dark-theme warning background further.
- Set DNA analysis text to 3 lines when not scrollable and 3.5 lines when scrollable.
- Moved the taste-combination Top5 occurrence count farther right in expanded-sidebar mode.
- Changed medal digit placement to use rank-specific tiny offsets for gold, silver, bronze, and normal medals.
- Deepened and broadened the persona type shadow.
- Split the two-line persona lead into two independently centered text rows with separate bracket elements, and moved the right bracket down slightly.

Explicitly not done:
- No statistics query, watch-history semantics, recommendation logic, AI prompt, database schema, database update, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify light-theme tab selected/hover text has no visible outline, then verify the two-line persona lead alignment in expanded-sidebar mode.
- Deferred: manually verify the rank-specific medal digit offsets against actual medal art.
- Noise: the persona text shadow is still custom geometry layering rather than a blur shader, so final spread perception depends on rendered scale.

## 2026-06-16 Watch Insights Semantic Border And Persona Prompt Follow-up

Stage goal:
- Apply the requested follow-up for dark semantic icon borders, taste-combination Top5 spacing, persona lead brackets, and longer persona analysis copy.

Completed:
- Matched the dark-theme success border to the preference graph green bubble border and lightened the warning border for yellow semantic icons.
- Reduced the taste-combination Top5 rank-to-combination gap and moved the occurrence count farther right in expanded-sidebar mode.
- Hid the persona lead corner brackets when the sidebar is expanded, while moving the right bracket lower for collapsed/long lead layouts.
- Updated the persona card prompt so `persona.description` asks for 150-220 Chinese characters across 2-3 sentences.
- Bumped the Watch Profile prompt version to `wi-profile-persona-23-parallel-v18-persona-description-longer`.

Explicitly not done:
- No statistics query, watch-history semantics, recommendation logic, database schema, database update, migration, commit, or push was changed.

Validation:
- Static search confirmed the new semantic border colors, Top5 spacing, prompt version, and 150-220 character rule are present.
- `dotnet build MediaLibrary.sln -m:1` passed with 0 warnings and 0 errors.
- Migration diff remained empty.

Known Issues:
- Blocker: none identified by solution build.
- Deferred: manually verify the dark green/yellow semantic icon borders against preference graph bubble borders.
- Deferred: existing cached persona profiles keep their old text length until a profile refresh regenerates with the new prompt version.
- Noise: prompt-version bump intentionally marks old cached profile text as older-rule output.
