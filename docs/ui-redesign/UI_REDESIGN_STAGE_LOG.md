# UI 初级重构阶段日志

## 2026-06-17 - Cross-Page Micro Polish Follow-up

Goal:
- Apply focused UI polish for scan progress metric icons, online-subtitle language filtering, and Home month-over-month trend presentation.
- Keep changes scoped to presentation and existing view-model formatting only.

Completed:
- Reduced the Scan Tasks progress metric icons from 28px to 22px and tightened the icon/text gap.
- Routed the Online Subtitle Search filter context menu, including the language filter, through the existing modern 6px auto-reveal scrollbar style.
- Suppressed the Home library status trend arrow when the month-over-month delta is exactly zero and the text says `较上月无变化`.

Explicitly not done:
- No scan logic, subtitle search/download behavior, Home statistics query, database schema, migration, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:
- Blocker: none.
- Deferred: manually verify the Scan Tasks metric icon scale, online-subtitle language menu scrollbar, and Home unchanged-delta text in the running WPF app.
- Noise: none.

## 2026-06-17 - Cross-Page Icon And Spacing Polish

Goal:
- Apply focused visual polish for icon choices, icon sizing, badge sizing, and compact form alignment across the WPF app.
- Keep the work scoped to UI presentation and existing view-model icon state only.

Completed:
- Updated Home library preview and Watch Insights overview watched status icons to the plain check mark, and reduced the Home AI recommendation rating badge footprint.
- Normalized bottom-left avatar popup menu icon rendering with Phosphor icons and switched the settings entry to `gear-six`.
- Enlarged the AI recommendation preference glyph while preserving the existing button hit area.
- Swapped API/WebDAV sensitive input reveal icons to the Phosphor eye/eye-slash pair.
- Reduced scan progress metric and remove icons without changing the surrounding layout structure.
- Aligned profile edit ComboBox text with the lower profile inputs through a matching compact template.
- Updated detail page watched/not-interested icon states and made filled favorite/rating glyphs use the accent color.
- Added missing Phosphor assets for fullscreen out/in and undo-style actions.

Explicitly not done:
- No database schema, migration, media-library semantics, scan logic, recommendation logic, commit, or push was changed.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Icon cleanup searches found no remaining `corners-out`, `corners-in`, `eye-closed`, or TMDB `funnel-simple` usage in the touched UI surfaces.

Known Issues:
- Blocker: none.
- Deferred: final visual acceptance still needs a manual pass in the running WPF app across light and dark themes.
- Noise: several adjusted icon sizes are optical-size fixes and may need one more pixel-level pass after screenshot review.

## 2026-06-13 - User Profile Dialog Visual Polish Follow-up

Goal:
- Polish the personal profile dialog and related user avatar surfaces based on visual feedback.
- Keep the changes scoped to WPF UI presentation and profile dialog behavior.

Completed:
- Added white outlines to the navigation avatar and expanded user-menu avatar.
- Added a white outline to the personal profile dialog's top-left logo only.
- Matched the personal profile dialog close button hover/pressed colors to the app title-bar close-button danger treatment.
- Enlarged the profile summary user name and moved it slightly to the right.
- Replaced the custom skewed edit pencil path with the existing Segoe MDL2 edit glyph and changed edit-button hover color away from white-on-light emphasis.
- Tightened profile field label/value spacing and allocated more height to the signature value/input area.
- Moved profile toast positioning toward the upper 30% of the screen work area while keeping it inside the dialog window.
- Preloaded the user profile before showing the dialog, avoiding the visible default-profile flash on open.
- Added an avatar-upload success toast immediately after a selected image is processed; save success still remains a separate toast on completion.
- Increased profile edit input height, reduced input corner radius, centered input content vertically, and kept inputs left-aligned with their labels.

Not done:
- No database schema change, migration, database update, media semantics change, commit or push change.
- No runtime screenshot sweep was performed for the WPF dialog in this pass.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check -- src/MediaLibrary.App/Views/MainWindow.xaml src/MediaLibrary.App/Views/Dialogs/UserProfileDialogWindow.xaml src/MediaLibrary.App/Views/Dialogs/UserProfileDialogWindow.xaml.cs src/MediaLibrary.App/ViewModels/Main/MainWindowViewModel.cs` returned no whitespace errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Manual WPF visual acceptance is still needed for exact avatar outline contrast, dialog close hover color, edit input vertical alignment, screen-relative toast placement, and profile-open transition on the user's display scale.
- Noise: Existing adjacent UI and diagnostics changes remain in the working tree.

## 2026-06-13 - Scroll Bottom Padding And User Menu Diagnostics Follow-up

Goal:
- Remove the fixed bottom strip that prevented Library / Movie Discovery search / ranking scroll content from visually reaching the card bottom.
- Remove the excessive bottom whitespace in Library list layout while keeping stable bottom breathing room for Movie Discovery search/ranking at the bottom state.
- Fix the avatar popup reopen-on-second-click issue and add diagnostics for the event sequence.

Completed:
- Set the Library, Movie Discovery search and Movie Discovery ranking content-body cards to keep top/side card padding but remove bottom card padding, so the card surface no longer creates a fixed bottom bar over the scrollable content.
- Removed the Library list `ListBox` bottom padding that created a larger final-row gap than Movie Discovery list layout.
- Added stable bottom content margins inside Movie Discovery search list and ranking scroll content instead of using dynamic card margins.
- Changed the avatar popup to `StaysOpen=True` and added main-window outside-click handling so clicking the avatar again closes the menu instead of being reopened by the old Popup auto-close + Button click order.
- Added `event=user-menu-toggle` diagnostics for avatar preview/click/popup/outside-close phases, logging only state flags and source control type.

Not done:
- No business semantic change, tag refresh change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Use the new `event=user-menu-toggle` diagnostics if the avatar menu still fails to close on the user's machine.
- Noise: Existing unrelated working-tree changes may remain present in this branch.

## 2026-06-13 - Scroll Corner Menu Cursor Follow-up

Goal:
- Stop scroll-driven bottom corner changes from feeding back into page layout and freezing Library / Movie Discovery search / ranking at the bottom edge.
- Make the navigation avatar menu close when clicking the avatar button again, matching the page filter dropdown interaction.
- Ensure Favorites uses a pointer cursor only on the poster/open hit area instead of the whole content surface.

Completed:
- Removed the dynamic bottom-margin mutation from `ScrollDrivenBottomCornerBehavior`; it now only updates bottom corner radius and no longer changes the card's layout size while scrolling.
- Removed stale `AtBottomBottomMargin` usage from Library, Movie Discovery search and Movie Discovery ranking card containers.
- Reworked the avatar menu button handler so the preview click closes an already-open popup and the popup close event no longer suppresses the next avatar click.
- Added explicit Arrow cursor surfaces to Favorites page containers, poster list boxes and list-box items while keeping the poster hit layer as Hand.

Not done:
- No business semantic change, tag refresh change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Manual WPF QA is still needed for bottom-edge scrolling in Library poster/list, Movie Discovery search poster/list, and Movie Discovery ranking.
- Noise: Existing unrelated working-tree changes may remain present in this branch.

## 2026-06-13 - Discovery Ai Tag Diagnostic Cleanup Follow-up

Goal:
- Remove temporary high-frequency diagnostics after confirming external AI tag replacement behavior, so Movie Discovery search/ranking refreshes do not keep synchronously appending per-card cache hit/apply logs.

Completed:
- Disabled the verbose `ai-tag-classification` write path while leaving a small commented placeholder for future targeted debugging.
- Removed external tag cache success logs for load/save/set/hit; failure logs remain for cache persistence problems.
- Removed per-card `discovery-card-external-tags-apply` logging from discovery movie cards.
- Kept the actual persistent cache and initial search/ranking cache application behavior unchanged.

Not done:
- No recommendation algorithm change, scan matching rule change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: If AI tag replacement regresses again, temporarily re-enable the commented classification diagnostic and add scoped cache/apply sampling instead of restoring always-on per-card logs.
- Noise: Existing unrelated working-tree changes may remain present in this branch.

## 2026-06-13 - Discovery External Ai Tag Startup Cache Follow-up

Goal:
- Use the new diagnostics to locate why Movie Discovery search/ranking cards still showed TMDB tags after app restart even though external no-source detail pages could read cached AI tags.
- Apply cached external AI tags during initial search/ranking card loading, not only after returning from detail or later metadata refresh.

Completed:
- Reviewed `ai-perf-debug.log` and confirmed external AI type tags were returned and vocabulary-filtered correctly; cache load/hit also worked.
- Identified the missing path: search and ranking initial page fetch created cards, applied local status, then added cards to the result buffers without calling the external tag cache snapshot path.
- Added external tag cache application immediately after initial search/ranking status resolution and before cards are added to the visible buffers.
- Scoped external cache application to cards without a local `MovieId`, so persisted no-source tags do not override local media-library movie tags.

Not done:
- No recommendation algorithm change, scan matching rule change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Reopen the app and confirm initial Movie Discovery search/ranking cards show cached AI tags before entering detail.
- Noise: Existing unrelated working-tree changes may remain present in this branch.

## 2026-06-12 - Discovery External Ai Tag Diagnostics And Cache Follow-up

Goal:
- Diagnose why Movie Discovery search and ranking cards sometimes do not replace type / emotion / scene tags after external no-source detail AI classification.
- Persist external no-source movie AI tags so app restart does not force the same search/ranking movie to regenerate tags.

Completed:
- Added `event=ai-tag-classification` diagnostics for local detail, scan and external no-source movie tag classification paths.
- The diagnostic records response length, raw AI-returned type / emotion / scene arrays, vocabulary-filtered arrays, fallback type tags, final applied tags and sanitized error text; it does not log full titles, paths, URLs or prompt content.
- Added persistent external movie AI tag cache backed by app data JSON, with `external-ai-tag-cache-load/save/hit/set` diagnostics.
- Search/ranking cards now apply the persistent cache on activation through the existing external tag snapshot path, so no-source external tags can survive app restart.
- Added `event=discovery-card-external-tags-apply` diagnostics to show incoming cached type tags, previous card tags, final card tags and whether the card actually changed.
- Kept the tag fallback rules: type tags fall back to TMDB/source type tags or `-`; emotion and scene tags fall back to `-`; over-limit tags are trimmed by the vocabulary filter and under-limit tags are not filled locally.

Not done:
- No recommendation algorithm change, scan matching rule change, movie-detail tag tooltip change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Use the new diagnostics on a real AI response to confirm whether misses are caused by AI returning out-of-vocabulary type tags, cache lookup misses, or card application being skipped.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Discovery Ai Type Override Follow-up

Goal:
- Ensure AI-generated type tags replace old discovery movie type tags instead of only updating emotion and scene tags.

Completed:
- Split discovery movie card AI type tags into an explicit `AiTagsText` state instead of relying only on the older `DisplayTags` / genre text.
- Updated the visible type-tag group to prefer `AiTagsText` and only fall back to `DisplayTags` when AI type tags are absent.
- Prevented TMDB detail snapshots from resetting `DisplayTags` back to old genre text when AI type tags already exist.
- Kept local detail/status snapshots authoritative, while external no-source AI snapshots no longer clear existing AI type tags when they lack type tags.

Not done:
- No recommendation algorithm change, scan behavior change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Running WPF visual QA is still recommended to confirm search/ranking cards visibly replace the first tag group after AI classification.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Discovery Ai Type Tag Snapshot Follow-up

Goal:
- Ensure Movie Discovery search and ranking cards receive AI-generated type tags together with emotion and scene tags after detail-page classification.

Completed:
- Rechecked the discovery card tag pipeline: the first visible tag group is driven by `DisplayTags`, and AI type tags enter it through `AiTagsText`.
- Kept local movie refresh reading all three fields from local detail/status snapshots.
- Changed external AI tag cache application to run for every cached search/ranking card before local detail override, so type / emotion / scene tags from no-source external detail classification are applied as one snapshot and cannot be skipped by transient local-status state.

Not done:
- No dynamic shadow rollback, movie-detail tag tooltip change, recommendation algorithm change, scan behavior change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Running WPF visual QA is still recommended for the exact search/ranking return-from-detail refresh behavior on real AI responses.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Discovery Star Refresh And Static Glow Follow-up

Goal:
- Fix Movie Discovery search poster star outline color, make search/ranking tag refresh robust after returning from detail, and strengthen static dark-theme glow/shadow effects.

Completed:
- Changed the Movie Discovery search poster want-to-watch star outline to the same white outline tone used by Favorites, including watched/empty star states.
- Confirmed Movie Detail local movie AI classification is awaited inside the detail loading flow and calls `NotifyMetadataChanged` after tags are written; external no-source detail classification writes to `ExternalMovieTagCache` and also notifies metadata changes.
- Made Movie Discovery request a cached movie-card refresh every time the page activates, not only when a data-change event was received while inactive.
- Kept the metadata/collection/library `DataChanged` listener and made the refresh dispatcher-safe for notifications coming from background threads.
- Applied external cached AI tags back to cached search/ranking cards when the card has no local movie record, so no-source external detail tags can refresh after returning.
- Strengthened the static token-level page/card shadow resources while keeping them as static resources, not dynamic resources.

Not done:
- No dynamic shadow rollback, movie-detail tag tooltip change, recommendation algorithm change, scan behavior change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `rg -n "StaticResource Shadow(LargeCard|InlineCard|ScoreBadge|ScanProgressIndicator)|DynamicResource Shadow(LargeCard|InlineCard|ScoreBadge|ScanProgressIndicator)" src/MediaLibrary.App -S` returned no remaining old static/dynamic page-card shadow effect references.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Running WPF visual QA is still recommended for exact star contrast on bright posters and the perceived static glow strength in both themes.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Favorite And Discovery Tag Refresh Follow-up

Goal:
- Fix Favorites poster tag data source, Movie Discovery ranking tag refresh after returning from detail, and static shadow visibility in dark theme without reverting to dynamic shadow resources.

Completed:
- Hydrated Favorites collection movie items from the matching local `Movies` record for `GenresText`, `AiTagsText`, `EmotionTagsText` and `SceneTagsText`, so favorite/want cards no longer display placeholder collection tags when the corresponding movie has real tags.
- Kept Favorites movie poster tag display aligned with Media Library's round-robin type / emotion / scene strategy, and made TV-season favorites use a single tag line like Media Library non-movie cards.
- Added local movie tag fields to discovery status resolution and stopped discovery movie cards from clearing emotion/scene tags when local status is applied.
- Added Movie Discovery `DataChanged` handling for cached search and ranking movie cards; metadata/collection/library changes now re-resolve local status and reload local movie detail tags when returning from the detail page.
- Kept page/card shadows static and repointed static shadow usages to fixed token-level resources so dark theme retains visible glow/shadow without dynamic resource reload cost.

Not done:
- No dynamic shadow rollback, movie-detail tag tooltip change, recommendation algorithm change, scan behavior change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `rg -n "StaticResource Shadow(LargeCard|InlineCard|ScoreBadge|ScanProgressIndicator)|DynamicResource Shadow(LargeCard|InlineCard|ScoreBadge|ScanProgressIndicator)" src/MediaLibrary.App -S` returned no remaining old static/dynamic page-card shadow effect references.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` returned empty.

Known Issues:
- Blocker: None.
- Deferred: Running WPF visual QA is still recommended for exact dark-theme shadow strength and the return-from-detail tag refresh path on real local data.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Favorites Tags And Static Shadow Follow-up

Goal:
- Fix favorite-card tag rotation, grouped tag tooltips and page-card shadow resource overhead without changing media-library semantics.

Completed:
- Updated Favorites poster tags to use the same round-robin type / emotion / scene selection strategy as Media Library, so short poster lines give each tag category a chance to appear before overflow.
- Added grouped three-line tooltip text for type / emotion / scene tags in Favorites, Media Library, Movie Discovery, Watch History, Home AI recommendation tags, recommendation cards and Watch Insights taste-combination chips.
- Preserved the trimmed-only tooltip rule for those grouped tag tooltips through `OnlyWhenVisibleTextTruncated`, so the grouped tooltip only appears when the visible tag text is explicitly overflowed or visually ellipsized.
- Kept Movie Detail tag chips without added tooltip behavior per product feedback.
- Changed fixed page/card/component shadow effects from dynamic shadow resources to static shadow resources while retaining theme-specific light and dark shadow resource values.

Not done:
- No movie-detail tag tooltip change, recommendation algorithm change, media-library semantics change, scan behavior change, database schema change, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `rg -n 'DynamicResource Shadow' src/MediaLibrary.App -S` returned no remaining dynamic shadow effect usages.
- Verified by search that Movie Detail tag chip bindings did not receive the grouped tag tooltip behavior.

Known Issues:
- Blocker: None.
- Deferred: Visual QA in the running WPF app is still recommended for exact tooltip trigger behavior on visually ellipsized tag text and perceived dark-theme scroll smoothness.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Tag Color Rollback And Summary Wheel Follow-up

Goal:
- Address the latest narrow UI feedback for movie-detail tag colors, watch-profile summary wheel handling and dark-theme season target highlight tone without changing business behavior.

Completed:
- Restored the movie-detail tag row labels to the original muted label color and changed tag chip text back to the same `BodyTextStyle` color family used by movie information values.
- Kept `IsUnidentifiedMovie` as a read-only UI state and used it to move only unrecognized movie tag rows down by 1px.
- Updated watch-profile summary wheel handling so it forwards the wheel to the outer page when the summary content does not truly need internal scrolling.
- Shifted the dark-theme season target episode highlight back to a brighter non-black rose tone with a softer pink border.

Not done:
- No navigation behavior, scan behavior, media-library semantics, database schema, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:
- Blocker: None.
- Deferred: Visual QA in the running WPF app is still recommended for exact tag contrast, summary wheel pass-through and dark-theme target highlight tone.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Rating Divider, Tooltip And Season Highlight Follow-up

Goal:
- Address the reported detail-page rating divider, movie tag spacing, shell title, tooltip shadow, watch-insight status and season target highlight issues without changing business behavior.

Completed:
- Added theme-specific `BrushRatingCardDivider` and unified movie, series, season and episode detail rating-card vertical dividers through `RatingCardDividerStyle`.
- Tightened movie-detail tag card padding, chip margins and title-to-first-row spacing; tag rows now use equal vertical regions so unrecognized movie tag rows distribute more evenly.
- Moved the expanded shell brand text another 1px left.
- Removed the tooltip drop shadow that produced a square dark corner outside the rounded tooltip border.
- Made Watch Insights profile/statistics refresh status text fixed-width, right-aligned and non-ellipsized with a fixed gap before the refresh button.
- Replaced season-detail target episode highlighting with theme-specific soft background and border colors for both light and dark themes.

Not done:
- No ViewModel behavior, navigation behavior, scan behavior, database schema, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:
- Blocker: None.
- Deferred: Visual QA in the running WPF app is still recommended for exact divider contrast, tag row distribution and target episode highlight tone in both themes.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Episode, Scan Log And Tooltip Correction Follow-up

Goal:
- Correct the latest UI polish feedback for episode detail alignment, scan-log card spacing, tooltips, tag outlines and paragraph line-height rollback without changing business behavior.

Completed:
- Kept the episode-detail information area in its reduced-gap position while aligning the episode poster left edge with the playback-source card left edge.
- Added trimmed-only tooltips to episode single-info fields through the existing trimmed-text tooltip behavior.
- Centered recent scan-log cards horizontally inside the scan-record card, made them slightly wider than the previous asymmetric layout and preserved shadow drawing room.
- Added a theme-specific movie-detail tag border resource so dark theme tag chips use a thin white outline while light theme keeps the standard border color.
- Moved the expanded shell brand text another 1px left.
- Reverted the previous global large-paragraph line-height increase while keeping the watch-profile summary scroll containment behavior.
- Adjusted tooltip theme colors so light theme uses a light tooltip surface, and tightened tooltip vertical padding/line layout for more even top and bottom spacing.

Not done:
- No service behavior, scan behavior, media-library semantics, database schema, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:
- Blocker: None.
- Deferred: Visual QA in the running WPF app is still recommended for exact poster/source-card alignment, scan-log glow clipping and tooltip top/bottom optical spacing.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Home, Filter Menu And Tooltip Polish Follow-up

Goal:
- Address the reported home layout, selected filter menu, paragraph rhythm and tooltip polish issues without changing business behavior.

Completed:
- Added selected-state color feedback to media-library and movie-discovery dropdown menus by marking the currently active menu item as checked after each popup opens.
- Aligned Home card header spacing for library preview, continue watching and recently added, and added poster-style lift feedback to the four library-preview metric cards.
- Nudged the expanded shell brand text left by 1px.
- Increased body and long-summary line height for large paragraphs.
- Reworked the global tooltip surface for theme-aware border/glow, faster 0.2s hover delay and wrapped string content when long text exceeds the tooltip width.

Not done:
- No ViewModel behavior beyond exposing the active movie-search sort label, no service behavior, database schema, migration, database update, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:
- Blocker: None.
- Deferred: Visual QA in the running WPF app is still recommended for exact header alignment, lift distance and tooltip wrapping width on different DPI settings.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Detail, Scan, Search And Insights UI Follow-up

Goal:
- Address the reported UI polish issues in episode detail, scan tasks, movie search, watch insights and movie detail tags without changing business behavior.

Completed:
- Moved the episode detail hero information area left by reducing the poster-side reserved width; the right-side single-episode information card and rightmost correction button stay anchored while the action-button gaps remain equal.
- Added explicit scan-record card glow and extra list gutter so each scan-record card has drawing room for its shadow.
- Updated WebDAV path, local path and scan-record nested wheel handling so an overflowed inner list absorbs wheel events while hovered, including at the top or bottom edge.
- Aligned movie-search list item background with the media-library list color resource.
- Added the same overflow-only wheel containment to the watch-profile summary text.
- Added glow to movie-detail tag chips and vertically centered tag rows against their labels.

Not done:
- No ViewModel, service, database schema, migration, database update, scan semantics, media-library state semantics, commit or push change.

Validation:
- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:
- Blocker: None.
- Deferred: Visual QA in the running WPF app is still recommended for exact spacing and glow strength on different window sizes.
- Noise: Existing unrelated working-tree changes remain present in this branch.

## 2026-06-12 - Scoped Popup, Poster Badge And Scan Tasks Follow-up

Goal:
- Finish the user-reported Phase 7.7g visual fixes without widening the scope to unrelated buttons, popups or business behavior.

Completed:
- Confirmed WebDAV configuration has no standalone vertical divider and kept the local configuration divider unchanged.
- Adjusted the Scan Tasks shadow-safe gutter and column minimum widths so scan-record cards keep symmetric visible spacing and have more glow drawing room.
- Strengthened dark-theme large-card glow with a half-step rollback from the strongest setting, and made dark glass large-card surfaces more opaque to reduce the black rectangular underlay feel.
- Thickened only the poster top-right filled want/favorite outline in Movie Discovery and Favorites.
- Kept the removed-library popup, custom preference popup and correction-dialog masks transparent; disabled clipping on the custom preference panel to avoid rectangular shadow residue.
- Updated correction dialogs with a glass large-card shell, shadow-safe content gutter, unclipped inline content card and shadowed correction source dropdowns.

Not done:
- No scan behavior, WebDAV/local path semantics, correction service behavior, recommendation behavior, database schema, migration, database update, commit, or push change.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gPopupCorrections\"` passed with 0 warnings and 0 errors; temporary build output was removed after success.

## 2026-06-11 - Scan Tasks Shadow-safe Layout Correction

Goal:
- Fix the remaining Scan Tasks clipping and dark-theme large-card glow issues without changing scan semantics.

Completed:
- Removed the WebDAV configuration vertical divider.
- Changed the Scan Tasks root safe area from the previous edge-compensation layout to a more conservative shadow-safe grid with equal visible left/right inset and larger actual glow gutters.
- Relaxed the left/right column minimum widths so the scan-record card is less likely to hit the right clipping edge.
- Strengthened dark-theme `ShadowLargeCard` again using a larger centered white glow.

Not done:
- No scan behavior, WebDAV/local path semantics, Settings/API behavior, database schema, migration, database update, commit, or push change.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gScanSafeGlowFix\"` passed with 0 warnings and 0 errors.

## 2026-06-11 - Scan Tasks Inline Panels And Dark Glow Boost

Goal:
- Align Scan Tasks inner cards with Watch Insights inline glass panels and make dark-theme large-card glow visibly stronger.

Completed:
- Rebased Scan Tasks inner cards, metric cards and empty-state cards on `GlassInlinePanelStyle`.
- Rebased `ScanLogCard` on the same inline glass panel family so scan records match the large card internals.
- Strengthened dark-theme `ShadowLargeCard` with more blur and opacity, centered as an outer glow. Compact cards and poster shadows were left on their existing resources.

Not done:
- No scan behavior, WebDAV/local path semantics, Settings/API behavior, database schema, migration, database update, commit, or push change.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gScanInlineGlowBoost\"` passed with 0 warnings and 0 errors.

## 2026-06-11 - Settings API Card Polish And Dark Large-card Glow

Goal:
- Fix the Settings page regressions from the cross-page card pass and refine dark-theme large-card elevation without changing poster-card shadows.

Completed:
- Restored the General Settings behavior card left/right compensation so it matches the cache settings card width.
- Reworked the API configuration card inner form panel to reuse the Watch Insights inline glass panel family.
- Routed large card styles through the theme-specific `ShadowLargeCard` resource. Dark theme now uses a weak white glow for large cards, while compact cards and poster shadows keep their existing resources.

Not done:
- No Settings/API service behavior, cache behavior, scan behavior, database schema, migration, database update, commit, or push change.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gSettingsApiGlow\"` passed with 0 warnings and 0 errors.

## 2026-06-11 - Cross-page Card Shadow Safe Area Follow-up

Goal:
- Audit and fix page-edge card shadow clipping risks across Home, Library, Watch History, Settings and Scan Tasks, while keeping this as a Phase 7.7g visual follow-up rather than a new stage.

Completed:
- Confirmed Home top/right cards, Library top/main cards, Watch History filter card, Settings content cards and Scan Tasks outer cards could sit against a page or scroll-viewport boundary without enough effect drawing space.
- Applied symmetric shadow-safe expansion and inner compensation so card bodies keep original content-column alignment while shadows can render outside normal card bounds.
- Rebased Settings general/API card surfaces and Scan Tasks outer cards onto `GlassPageCardStyle`.

Not done:
- No Settings or Scan service behavior, scan path semantics, cache behavior, database schema, migration, database update, commit, or push change.

Validation:
- `dotnet build MediaLibrary.sln -v:minimal -p:OutDir="$env:TEMP\XFVerseCodexBuild77gCrossPageShadowSafe\"` passed with 0 warnings and 0 errors.

## UI-0

- 只读审计已完成。
- 确认当前导航、页面模板、DI 注册和重复资源页面引用。

## UI-1

- 删除重复资源页面。
- 主导航改为六个可见项。
- 资源库入口显示名改为媒体库。
- 新增影片发现 / 观影历史最小页面壳。
- 新增个人资料弹窗空壳。
- 扫描任务和设置从主导航移到用户菜单。
- AI 推荐暂时隐藏主导航，后续 UI-4 迁入影片发现。

## UI-2

- 首页已调整为 70% / 30% 两栏结构。
- 左侧为片库预览、继续观看、最近新增。
- 片库预览复用 `IWatchStatisticsService` 的 All 范围状态总览，展示已看 / 喜爱 / 想看 / 不想看。
- 继续观看复用现有最近观看数据源，查看更多跳转观影历史。
- 最近新增复用现有首页入库时间倒序数据源，首页显示数量上限调整为 8。
- 右侧 AI 推荐预览复用现有 `RecommendationsViewModel`，换一批和详情跳转走原推荐逻辑。
- 发现更多影片跳转影片发现页面。
- 未改推荐算法，未新增 DB / migration。

## UI-4

- 影片发现页 AI 推荐 Tab 已承接现有 AI 推荐功能。
- AI 推荐 Tab 复用现有 `RecommendationsViewModel`，通过现有 DataTemplate 渲染 `RecommendationsPage`。
- 影片搜索和榜单继续保持占位。
- `Recommendations` 隐藏路由继续保留。
- 未改推荐算法，未新增 DB / migration。
- 首页接入影片发现已在 UI-2 完成，直接定位 AI 推荐 Tab 留到后续增强。

## UI-3

- 媒体库顶部筛选项已重排为搜索、排序、顺序、范围、标签、年代、收藏状态、识别状态、观看状态和清除筛选。
- 范围筛选已整理为“影片范围 / 影片来源”二级菜单；影片范围接真实筛选，影片来源为占位。
- 标签筛选已整理为固定二维多选菜单；类型、情绪、场景使用固定词表，多个已选标签按全部满足过滤。
- 年代筛选已改为按十年一档生成选项。
- 识别状态文案已改为自动匹配、待人工确认、手动确认、识别失败、未识别。
- 状态栏显示“找到 X 部影片”，右侧提供布局切换占位按钮和批量选择入口。
- 批量已看 / 未看、AI 辅助识别、移出媒体库、删除影片记录和详情跳转保留原命令与业务语义。
- 未新增 DB / migration，未改推荐、播放器、扫描、设置和观影洞察逻辑。

## UI-5

- 观影历史页已从占位壳接入真实观看记录。
- 新增 `WatchHistory` 只读查询，按本地观看日期分组。
- 观影历史页按播放记录流水展示，不套用观影统计的 60 秒有效观看阈值。
- 同一电影同一天多条观看记录只保留最新一条，不同日期分别显示。
- 日期筛选支持全部、今天、本周、本月和指定日期。
- 观影洞察日历点击日期会跳转观影历史，并筛选到对应当天。
- 历史记录显示海报、标题、观看时间、观看时长、播放进度和详情入口。
- 未改播放器、自动已看算法、`WatchHistory` 写入逻辑，未新增 DB / migration。

## UI-6

- 收藏夹页面已改为喜爱 / 想看两个 Tab。
- 移除收藏夹主结构中的全部下拉筛选，不展示不想看 Tab。
- 喜爱 Tab 使用库内 `Movie.IsFavorite` 数据，想看 Tab 使用 `UserMovieCollectionItem.IsWantToWatch` 数据。
- 同一 TMDB ID 去重，库内影片信息优先。
- 取消喜爱和取消想看继续走现有 service，并保留状态历史写入路径。
- 未新增 DB / migration，未改推荐算法和详情页业务逻辑。

## UI-7

- 扫描任务页已重构为 70% / 30% 结构。
- 左侧上方为扫描配置：已添加路径和 WebDAV 配置并列展示。
- 左侧下方为扫描进度：开始扫描、取消扫描、indeterminate 进度条和扫描结果计数。
- 右侧为扫描记录，复用最近 `ScanTaskLogs`，按时间倒序展示。
- WebDAV 配置和扫描路径管理已接入扫描任务页，设置页旧入口已在 UI-8 移除。
- 本阶段只做 WebDAV，不新增本地文件夹扫描。
- 未改扫描识别核心逻辑，未新增 DB / migration。

## UI-8

- 设置页已整理为通用 / API 配置两个 Tab。
- 通用 Tab 包含缓存设置、行为设置和关于卡片。
- 缓存设置复用现有视频缓存设置、刷新占用和清空缓存逻辑。
- 行为设置复用现有主题切换逻辑。
- API 配置 Tab 包含 TMDB、OMDb 和大模型配置。
- WebDAV 配置和扫描路径管理从设置页移除，保留扫描任务页作为主入口。
- 未新增设置项，未改 API 调用逻辑，未新增 DB / migration。

## UI-8 API 配置 Bugfix

- 修复 UI-8 后 API 配置 Tab 中 TMDB / OMDb 保存设置和测试连接按钮被合并的问题。
- TMDB 配置卡片已恢复独立保存设置、测试连接按钮和状态提示。
- OMDb 配置卡片已恢复独立保存设置、测试连接按钮和状态提示。
- 本修复不新增 DB / migration，不改 TMDB / OMDb API 调用逻辑。

## UI-3 媒体库标签筛选菜单微调

### 阶段目标

- 调整媒体库标签筛选二级菜单的标签呈现方式，去掉胶囊视觉，保留现有多选筛选语义。

### 完成内容

- 标签二级菜单项改为纯文本式可点击项，选中态使用文字颜色和字重提示。
- 标签选项按 4 列分行渲染，每行列内文本垂直居中。
- 行分割线只保留在非最后一行，末行底部不再绘制横线。

### 明确未做事项

- 未调整标签词表、筛选条件、筛选语义或按钮文案。
- 未新增数据库字段、migration 或后台查询逻辑。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## Settings API Sensitive Input Icon Follow-up

### Stage Goal

- Correct the API settings sensitive-field reveal button icon semantics.

### Completed

- Replaced the incorrect revealed-state font glyph with explicit vector eye icons.
- Hidden content now shows an open eye icon, and revealed content now shows a closed eye icon.
- Kept the existing reveal/hide binding and tooltip behavior unchanged.

### Explicitly Not Done

- No API save, test, scan, cache, database, or migration behavior was changed.
- No database update, commit, or push was performed.

### Verification

- `dotnet build MediaLibrary.sln`: passed, 0 warnings / 0 errors.
- `git diff --check`: no whitespace errors; existing CRLF conversion warnings only.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: no output.

### Known Issues

- Blocker: None.
- Deferred: Runtime UI confirmation is still needed to verify the closed-eye icon reads clearly at the final rendered size.
- Noise: Existing CRLF conversion warnings may still appear in git checks.

## 详情页与修正弹窗实测回归修复补充 4

- 季详情 Hero 区恢复原始外层几何，不再增加卡片外边框高度；通过对称减小上下内边距扩展可显示内容区域，并让左侧首行、简介底边和右侧单季信息卡外边框使用同一垂直基线。
- 修正弹窗首次打开时增加即时和延迟鼠标子树捕获，并在捕获成功后同步 WPF 鼠标状态；打开期间显式关闭宿主窗口标题栏命中并在关闭后恢复，清理打开前遗留的标题栏悬停视觉和手势。透明背景命中层继续使用中性箭头光标，不覆盖弹窗内输入框和下拉框自身光标；丢失捕获后的重捕获降到后台优先级，避免与下拉 `Popup` 抢占鼠标捕获。
- 电影详情已有未识别季选择器的旧版半透明遮罩同步改为透明命中层，修正弹窗链路不再绘制有色遮罩。
- 电影、单集和单季修正的 AI 辅助识别增加独立取消令牌；顶部关闭会先取消并脱离旧请求，再清理弹窗状态。AI 加载期间禁用下拉框、输入框和弹窗内取消按钮。
- 电视剧集候选和已有未识别季候选的日历、影片图标统一放入固定 `16px` 容器，并针对 Segoe MDL2 字形视觉基线偏低统一上移 `1px`；导演列继续扩宽，类型列整体右移。
- 单集修正弹窗复用电影修正弹窗现代化 UI，标题改为 `单集修正识别`，播放源下拉框保留单集业务绑定和切换状态。
- 新增单季修正弹窗现代化 UI，标题为 `单季修正识别`；移除播放源文件行，仅保留 `修正为季` 和 `加入到已有未识别季` 两种模式。
- 单季修正两种模式统一使用 `目标集号映射` 子弹窗。子弹窗使用透明阻断层并禁用外层表单键盘交互，保留子弹窗自身输入；列表使用现代化滚动条、对齐内容区左右边界的分隔线行、路径溢出省略与按需悬停提示、目标集号输入框，以及水平平均分布的确认 / 取消按钮。
- `目标集号映射` 子弹窗打开时保存映射快照：确认保留修改，取消和右上角关闭恢复打开前映射，避免未确认内容泄漏到季修正事务。
- 季修正每集只投影一个有效默认播放源；不提供逐集播放源选择。单季 AI 忙状态显式通知交互启用属性，确保加载期间实际禁用单季表单。
- AI 取消令牌继续传递到后续 TMDB 搜索和详情补全，并在候选集合写回前再次检查取消状态，防止旧请求回写已关闭或重新打开的弹窗。
- 未新增 DB / migration，未修改扫描识别阈值、媒体库语义或修正事务边界。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 扫描记录与观影历史反馈修复补充

- 扫描记录卡片的 BaseUrl 改为显示配置值；扫描结果统一显示为 `扫描结果：` 后接摘要，并在下一行显示主要原因，不再暴露“原因摘要 / 主要原因”字段名。
- 观影历史海报卡片复用媒体库标签语义：电影显示类型 / 情绪 / 场景三类标签，电视剧显示一类标签。
- 观影历史日期组内海报改为等距换行布局，卡片尺寸和海报 margin 对齐媒体库；收起 / 展开导航栏时保持左右边距和列间距一致。
- 观影历史日期控件取消自定义 DatePicker 模板，继承项目通用 DatePicker 样式，恢复原生点击和弹出日历交互。
- 观影历史滚动偏移改为应用进程内持久保存，并过滤列表刷新 / 卸载时的高度重置事件，避免返回详情页或切换页面时先闪到顶部再跳回。
- 未新增 DB / migration，未修改扫描执行、媒体库状态或播放历史写入语义。

验证：

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 观影历史海报卡片与滚动体验补充

- 观影历史海报卡片补齐媒体库同款字段结构：左上角显示电影 / 电视剧，右上角显示本地 / 网盘来源摘要，移除“有进度”类顶部状态标签，日期行改为优先显示完整发布日期。
- 海报卡片 hover 上移改为媒体库同款样式触发；导航栏展开时按媒体库海报列宽节奏调整间距，并为左侧发光阴影预留安全空间。
- 顶部工具栏移除刷新按钮；指定日期控件移动到“指定日期”按钮右侧，并改成本页现代化弹出日历样式。
- 空状态移除背景卡片，仅保留居中的纯文本提示。
- 观影日历跳转到观影历史后仅定位日期，不再绘制红色边框高亮。
- 观影历史滚动位置改为应用生命周期内持久化：离开详情页返回、切换页面再回来都会恢复到原滚动进度，恢复期间隐藏瞬时跳转。
- 观影历史列表查询补充发布日期和可用播放源来源摘要，仅扩展只读投影和 read model，不修改数据库结构。
- 未新增 DB / migration，未修改播放、扫描识别或媒体库删除语义。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 扫描任务配置区与 API 语言显示修补

### 阶段目标

- 修复 API 配置页 OpenSubtitles 默认搜索语言选中态显示 record 对象字符串的问题。
- 修复扫描任务页左侧配置区鼠标悬停在输入框、路径列表或本地配置内容区时页面滚动被局部控件阻断的问题。
- 对齐 WebDAV 配置卡片的路径添加、账号状态、按钮密度和空状态表现。

### 完成内容

- OpenSubtitles 语言选项显式返回中文显示名，避免 ComboBox 选中态显示 `{ Code, Name }` record 结构。
- 扫描任务页新增外层 / 内层滚轮路由：路径列表和记录列表可滚时保留内层滚动，不可滚或鼠标位于输入框时转交页面滚动。
- WebDAV 添加路径行移除白色背景边框，替换为轻量行布局，并缩小选择路径按钮。
- WebDAV 已添加路径列表补充滚轮处理和高度约束，保留独立滚动能力。
- WebDAV 账号配置卡标题右侧改为 API 配置页同款状态徽章，包含呼吸圆点、状态文本和圆角背景。
- WebDAV 账号配置三行输入之间的间距加大；保存配置、测试连接和选择路径按钮统一为紧凑尺寸。
- WebDAV 账号配置按钮区左侧增加连接状态文本。
- WebDAV / 本地 / 扫描记录空状态文本改为内容区居中，并加大字号和字重。

### 明确未做事项

- 未修改 WebDAV 保存、测试连接、路径保存、扫描启动或扫描识别业务语义。
- 未修改 OpenSubtitles API 调用、搜索、下载或播放器字幕流程。
- 未新增数据库字段、migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 LF/CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：仍需运行时人工确认扫描任务页在目标窗口宽度、DPI、浅色 / 深色主题下的滚轮转交、路径列表滚动和状态徽章视觉。
- Noise：本次仅调整软件内配置 UI，路径移除仍只删除软件内路径配置，不删除本地文件或 WebDAV 文件。

## 季详情集简介与扫描配置 Follow-up

### 完成内容

- 季详情集列表简介继续使用三行半高度的现代化纵向滚动区域，显式禁用横向滚动和文本省略，并按可用宽度约束简介文本换行。
- WebDAV 添加路径行修正为文件夹图标，保存配置 / 测试连接按钮改为按文字自适应宽度。
- WebDAV 账号状态区改为未测试黄色、测试通过绿色、测试失败红色；保存配置成功后自动执行一次现有 WebDAV 连接测试。
- WebDAV 添加路径卡片和账号配置卡片移除星号行撑高造成的底部空白。
- 本地路径列表改为固定宽度 WrapPanel，路径框不再随路径数量拉伸；本地目录选择器默认回到用户目录 / 文档目录，不沿用系统上次选择。

### 明确未做事项

- 未修改扫描识别算法、WebDAV / 本地路径保存语义、路径移除语义、播放器或媒体库业务语义。
- 未新增数据库字段、migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\xfv-b\"`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：未执行运行时人工 UI 点击验收；仍需确认扫描任务页状态徽章颜色、保存后自动测试、路径列表固定高度和本地目录选择器起始目录。
- Noise：WebDAV 保存后的自动测试会发起一次真实网络连接探测，结果仍取决于用户配置和当前网络。

## 季详情滚轮与扫描进度布局 Follow-up

### 完成内容

- 季详情集列表简介不可滚动时，将鼠标滚轮转交给外层集列表；只有简介本身内容溢出时才锁定滚动到简介区域。
- WebDAV 添加路径行改为矢量文件夹图标，避免字体 glyph 码位差异。
- WebDAV 保存配置按钮改为只有连接字段相对已保存值发生变化时才可用。
- 本地多选目录 picker 增加 `FolderName` fallback，不检查目录内容，空文件夹也可作为本地扫描路径配置保存。
- 扫描进度卡片恢复标题下方先显示进度条、当前阶段、当前文件，再显示统计卡片和操作按钮；统计卡片缩窄、加大中间列间隔，操作按钮整体垂直居中两行统计卡片。
- 进度条底轨改为灰色系；已扫描统计图标改为媒体语义图标并放大、按固定行高垂直居中。

### 明确未做事项

- 未修改扫描识别算法、WebDAV / 本地路径保存语义、路径移除语义、数据库结构或 migration。
- 未执行 database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln -p:OutDir="$env:TEMP\xfv-b\"`：通过，0 warning / 0 error。

### Known Issues

- Blocker：无。
- Deferred：未执行运行时人工 UI 点击验收；仍需确认空目录选择、保存按钮禁用态、进度卡片视觉和季详情滚轮转交。
- Noise：保存配置按钮的禁用态基于当前 App 会话内的已保存快照；外部并发修改配置后需刷新页面重新加载基线。

## 扫描任务页本地路径、进度与记录卡补充

### 阶段目标

- 修复本地配置卡片只能单选目录、目录列表填充顺序和左右滚动割裂问题。
- 收口扫描进度卡片的统计布局、按钮位置和进度条可见性。
- 将扫描记录卡片改为面向用户阅读的纯文字结构，同时继续隐藏 WebDAV URL。

### 完成内容

- 本地目录选择器支持一次选择多个目录；编辑单个本地目录时仍保留单选。
- 批量添加本地目录时逐项保存，通过现有重复 / 包含关系 / 路径格式安全门；状态文本汇总成功添加数量和跳过原因。
- 本地目录列表按更新时间倒序显示，最新添加项固定在左上角；单个共享滚动区内使用两列行优先填充。
- 本地配置空状态文本继续居中、加大、加粗。
- 扫描进度统计改为两行三列；已扫描图标改为打勾，图标与两行文字在卡片内垂直居中。
- 扫描进度按钮移到统计区右侧竖排，复用紧凑按钮尺寸；进度条改用更明显的信息色。
- 扫描记录卡片减小圆角，标题改为路径文本并缩小字号，右侧加入带呼吸圆点和状态标签的状态徽章。
- 扫描记录卡片信息区改为纯文字：BaseUrl、用户名、扫描状态、开始时间、结束时间、扫描 / 新增 / 更新、忽略 / 错误 / 耗时、扫描结果。
- 扫描结果文本改为“原因摘要 + 主要原因”的用户可读格式；WebDAV URL 和敏感键值仍隐藏，本地路径和 WebDAV 虚拟路径不再被替换为占位文本。

### 明确未做事项

- 未修改扫描识别算法、扫描统计来源、WebDAV / 本地扫描执行语义或删除路径语义。
- 未新增数据库字段、migration、database update、commit 或 push。
- 未执行真实多选目录和运行时视觉验收。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 LF/CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：仍需运行时人工确认本地目录多选对话框、两列共享滚动、紧凑按钮文字宽度、扫描记录长路径换行 / Tooltip 和浅色 / 深色主题下的状态徽章。
- Noise：批量添加本地目录时，重复目录或父子包含目录会被现有安全门跳过，并在状态文本中说明原因。

## 扫描任务页布局重构补充

### 阶段目标

- 按扫描任务页草图重构页面信息架构，形成左侧三张配置 / 进度卡片与右侧扫描记录列表。

### 完成内容

- 扫描任务页外层改为可纵向滚动，页面内滚动条统一为自动显隐的现代化细滚动条。
- 页面副标题改为 `基于WebDAV协议进行扫描`。
- 左侧改为三张大卡片：`WebDAV配置`、`本地配置`、`扫描进度`。
- `WebDAV配置` 内部分为左右两张卡片：左侧提供选择并立即添加 WebDAV 路径，右侧提供 BaseUrl、用户名、密码、保存配置和测试连接，并复用 API 配置页呼吸状态灯风格。
- WebDAV / 本地路径列表行改为路径 + 纯图标移除按钮；路径仅在实际截断时显示完整悬停提示。
- 本地配置卡片改为左右两列路径列表，中间用竖线分隔，标题右侧提供选择路径入口。
- 扫描进度卡片改为图标化统计卡片，统计项为已扫描、新增、更新、忽略、错误和耗时；进度条保留 indeterminate 动画，不再显示百分比；当前扫描文件上方保留当前阶段文本。
- 右侧扫描记录卡片高度绑定左侧内容高度，记录列表内部独立滚动。

### 明确未做事项

- 未修改扫描识别、TMDB / AI 匹配、扫描统计字段来源或批量操作语义。
- 未新增数据库字段、migration、database update、commit 或 push。
- 未执行真实 WebDAV / 本地目录选择器的人工点击验收。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：仍需运行时人工确认扫描任务页在目标 DPI / 窗口宽度下的左右栏高度、路径选择弹窗体验和状态灯颜色感知。
- Noise：路径移除按钮为直接配置移除入口，仅删除软件内路径配置，不删除真实本地文件或 WebDAV 文件。

## 扫描任务页记录列表排版补充

### 阶段目标

- 去掉扫描任务页内容区重复标题，并按真实扫描日志字段优化右侧记录列表。

### 完成内容

- 移除扫描任务页内容区内重复的 `扫描任务` 标题、副标题和刷新按钮，保留主窗口标题栏作为唯一页面标题来源。
- 右侧扫描记录卡片重排为路径名称 + 状态、目标来源、开始 / 结束 / 耗时、扫描 / 新增 / 更新 / 忽略 / 错误统计、可选原因摘要和错误信息。
- 记录列表继续复用真实 `ScanTaskLogItem` / `ScanTaskLogViewModel` 字段，不新增假字段或服务层计算。

### 明确未做事项

- 未修改扫描日志写入、扫描统计含义、路径脱敏规则或扫描执行逻辑。
- 未新增数据库字段、migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：仍需运行时人工确认右侧记录卡在真实长标题、长错误信息和多条原因摘要下的高度与滚动体验。
- Noise：扫描记录仍按最近日志倒序展示，未引入筛选、分页或清空记录入口。

## Settings API Cursor Polish Follow-up

### Stage Goal

- Fix the API settings tab cursor state so the full panel no longer appears clickable.

### Completed

- Set the API settings content `ScrollViewer` cursor to `Arrow`, overriding the hidden tab item's inherited hand cursor.
- Kept button styles unchanged so real actions still use the hand cursor.

### Explicitly Not Done

- No API save, test, scan, cache, database, or migration behavior was changed.
- No database update, commit, or push was performed.

### Verification

- `dotnet build MediaLibrary.sln`: passed, 0 warnings / 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: no output.

### Known Issues

- Blocker: None.
- Deferred: Runtime UI confirmation is still needed to verify text inputs keep their normal editing cursor and buttons keep the hand cursor.
- Noise: Existing CRLF conversion warnings may still appear in git checks.

## AI 推荐加载与媒体库播放源默认值 Follow-up

### 阶段目标

- 修正 AI 推荐加载动画中静态环与动态弧线尺寸不一致的问题。
- 将媒体库播放源状态默认值改为 `有播放源`，并同步清空筛选和默认状态判定。

### 完成内容

- 共享 `LoadingSpinnerTemplate` 的静态环固定到与动态弧线一致的 `34px` 坐标尺寸，避免 AI 推荐加载时静态圈视觉偏大。
- 媒体库播放源状态新增默认常量，初始进入和清空筛选后均回到 `有播放源`。
- `CanClearFilters` / 默认筛选判定同步改为以 `有播放源` 为默认值；实际查询仍沿用现有 `HasActiveSource` 过滤，不改变媒体库删除、移出或播放源语义。

### 明确未做事项

- 未修改推荐算法、AI 请求、候选生成、扫描识别、播放器播放源选择或媒体库记录语义。
- 未新增数据库字段、migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：未执行运行时人工 UI 验收；建议确认 AI 推荐空结果加载层的环线尺寸，以及媒体库首次进入 / 清空筛选后按钮显示 `播放源：有播放源`。
- Noise：共享 `LoadingSpinnerTemplate` 的尺寸修正会同步影响其他使用该模板的加载层。

## AI 推荐加载 Spinner 对齐 Follow-up

### 阶段目标

- 修正 AI 推荐页空结果加载时橙色动态弧线与灰色静态环中心不重合的问题。

### 完成内容

- `LoadingSpinnerTemplate` 改为外层跟随调用方尺寸、内层固定 `34px` 并居中。
- 灰色静态环和橙色动态弧线现在共用同一个 `34px` 内层坐标系，避免 AI 推荐页 `38px` 调用尺寸下橙色弧线偏向左上。

### 明确未做事项

- 未修改推荐算法、AI 请求、媒体库筛选语义、播放器或扫描识别逻辑。
- 未新增数据库字段、migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：仍建议运行时人工确认 AI 推荐加载层视觉完全重合。
- Noise：共享 Spinner 模板调整会同步影响其他使用该模板的加载控件，但坐标系保持一致。

Known Issues：

- Blocker：静态检查和构建检查未发现阻塞问题。
- Deferred：像素级对齐、弹窗首帧标题栏悬停阻断、AI 请求中关闭后立即重开仍需人工运行时验收。
- Noise：Windows 换行转换提示仍存在。

### Known Issues

#### Blocker

- 无。

#### Deferred

- 标签筛选仍沿用 UI-3 固定词表和全部满足过滤逻辑。

#### Noise

- 本次仅为媒体库标签筛选二级菜单视觉微调，不处理最终 UI 统一阶段中的整体菜单动效。

## UI-9

- 完成 UI-1 到 UI-8 初级 UI 重构静态回归审计。
- 确认主导航只保留首页、媒体库、影片发现、观影历史、收藏夹、观影洞察六个可见入口。
- 确认 MovieDetail、ScanTasks、Settings、Recommendations 作为隐藏路由保留并有 DataTemplate / DI 注册。
- 确认重复资源页面和 `Duplicates` 有效引用已清理。
- 确认首页、媒体库、影片发现、观影历史、收藏夹、观影洞察、扫描任务、设置页关键命令绑定未发现阻塞回归。
- 确认 UI-8 后 TMDB / OMDb 保存设置和测试连接为独立按钮，状态提示为独立字段。
- 整理 `UI_REDESIGN_PLAN.md`，标记 UI-0 到 UI-9 完成。
- 重写 `UI_REDESIGN_KNOWN_ISSUES.md`，按 Blocker、Deferred、Known Limitation、Noise 收口。
- 本阶段未新增功能，未新增 DB / migration，未改推荐算法、播放器、扫描识别核心或 API 调用逻辑。

## 详情页体验回归修复

- 统一输入框占位提示进入 `TextBox` 模板，与实际输入内容复用同一 `Padding`，并在 IME 首次组合输入时立即隐藏。
- 媒体库搜索和修正窗口输入框已接入统一占位提示；修正窗口空输入时恢复提示显示。
- 电影、剧、季、集简介以及修正候选简介的纯文本滚动区已接入半行截断提示；表格和列表滚动区不应用该规则。
- 剧详情 Season 列表、季详情 Episode 列表和集详情信息卡完成针对性间距与对齐调整。
- 未新增 DB / migration，未改媒体库语义或扫描识别规则。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 修正弹窗模态化与详情页闪退修复

- 修正弹窗遮罩改为独立 `Popup`，打开后覆盖整个宿主窗口并拦截下层交互；窗口移动、缩放和状态切换时同步刷新遮罩尺寸。
- 修正弹窗下拉框改为现代化圆角样式，候选列表补充分隔线、层级间距、文本对齐和滚动提示。
- 电影详情页进入即闪退的根因是 `CorrectionDialogComboBoxStyle` 在 `Controls.xaml` 中提前引用尚未合并的 `ComboBoxStyle`。该样式已移动到 `Inputs.xaml`，确保基础样式先加载再继承。
- 未新增 DB / migration，未修改识别、修正事务或媒体库语义。

验证：

- `dotnet build MediaLibrary.sln -p:UseAppHost=false`：通过，运行中的应用实例锁定旧 `.exe`，产生 1 条删除 apphost 警告。
- STA 运行时实例化：`MovieDetailPage`、`SeriesOverviewPage`、`TvSeasonDetailPage`、`EpisodeDetailPage` 均通过。
- STA 模态遮罩验证：`CorrectionDialogShell` 打开后覆盖宿主窗口，通过。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 详情页 UI 实测回归修复

- 撤回修正弹窗的独立 `Popup` 遮罩实现，恢复为详情页内稳定遮罩。修正窗口继续拦截当前详情页下层交互，不再创建容易错位的额外窗口层。
- 半行截断提示行为在布局更新后持续同步，避免剧、季详情简介首次显示时尚未拿到稳定可滚动高度。
- 单季信息、剧信息、单集信息和首页 AI 推荐简介补齐半行截断提示接入。
- 剧详情 Season 行海报使用圆角遮罩裁剪实际图片和占位图，左侧扩展阴影安全区但保持海报本体与 `Season` 标题左边缘对齐。
- 集详情播出日期行改为底边对齐布局；日历图标在与日期同高的容器中垂直居中，图标和日期颜色与剧名一致。
- 未新增 DB / migration，未改业务语义。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## Settings API Config Polish Follow-up

### 阶段目标

- 收口 API 配置页的输入密度、状态提示、保存/测试交互和大模型路由测试能力。

### 完成内容

- API 配置输入框字体缩小，`ApiConfigCard` 标题和右上状态呼吸灯放大，状态圆点颜色加深。
- 在线字幕配置移除启用开关 UI，设置模型与保存/读取路径固定视为启用；右上状态只展示配置/缺少 API Key/测试结果，不再展示启用或停用状态。
- 大模型高级路由移除原“模型 / 超时”表头行，每行改为输入框左侧内联显示“模型”和“超时（s）”，并将模型输入框缩短为固定宽度。
- 大模型配置新增测试按钮，会从默认模型和全部高级路由模型中提取去重模型，并逐个发送轻量连接探测；任一失败会同步右上状态为失败。
- API 配置页的 TMDB、OMDb、OpenSubtitles 和大模型保存按钮改为仅在输入相对已保存值发生变化时可用。
- API 配置保存成功后自动执行对应测试；单独点击测试不会保存设置或 token 变更。

### 明确未做事项

- 未修改 TMDB、OMDb、OpenSubtitles 或 AI 生产调用语义。
- 未新增数据库字段、migration、database update、commit 或 push。
- 该次 API 配置 follow-up 当时未扩展到扫描任务页或非 API 配置表单的保存按钮；扫描任务页 WebDAV 保存后自动测试已在后续 follow-up 单独记录。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：仍建议进行运行时人工 UI 验收，确认浅色/深色主题下状态灯、按钮禁用态和高级路由布局与草图密度一致。
- Noise：大模型测试会对每个去重模型发送一次轻量请求，实际可用性仍取决于用户配置的服务端模型权限和网络状态。

## Settings API And General Tab Polish Follow-up

### 阶段目标

- 微调 API 配置高级路由区视觉密度，并减少通用设置首次打开时字段占位值到真实值的闪烁感。

### 完成内容

- 高级路由设置标题字号调大、字重加重，并整体下移 2px。
- 高级路由模型输入列由半宽进一步缩短为约三分之一宽度。
- 通用设置 Tab 首次激活时在应用设置、行为偏好和缓存状态加载完成前保持内容 Hidden；加载完成或加载失败需要展示错误时再显示，避免默认字段先渲染后刷新。

### 明确未做事项

- 未修改 API 测试请求、保存语义、缓存清理语义、扫描逻辑或数据库结构。
- 未新增 migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。

### Known Issues

- Blocker：无。
- Deferred：仍需运行时人工确认首次打开设置页时通用 Tab 不再出现字段闪烁。
- Noise：通用 Tab 首次加载期间会短暂隐藏内容，等待缓存状态加载完成后一次性显示。
- 最终补丁未执行应用运行时 UI 验收；早期遮罩诊断入口已停止并清理。

## 详情页 UI 实测回归修复补充

- 半行截断提示改为按文本行高对齐到半行边界；剧、季详情简介在静止状态下也会保留明确的半行截断提示。
- 单季信息继续接入半行截断提示；该规则只应用于纯文本滚动区域，不扩展到表格或列表。
- 修正弹窗外层遮罩改为透明命中层：继续禁止弹窗外按钮交互，不再绘制有色背景。
- 输入框提示层与实际输入内容复用同一个内边距容器，提示文字字首与输入光标起点对齐；IME 组合输入开始时仍立即隐藏提示。
- 修正弹窗下拉框的点击层改为透明模板，避免悬停时出现系统默认蓝色矩形。
- 电影修正结果海报补充真实圆角裁剪；三类修正结果列表统一顶部、底部分隔线和右移滚动条布局。
- 修正弹窗切换播放源时只更新当前来源和预览上下文，不清空电影、电视剧集或已有未识别季结果，不切换当前修正模式。
- 未新增 DB / migration，未修改媒体库语义、扫描识别规则或修正事务。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 详情页与修正弹窗实测回归修复补充 3

- 季详情 Hero 区改为按完整信息行增量同步加高：右侧单季信息卡和左侧简介区同时增加 `20px`，整体向上移动 `10px`；左侧标题、评分与简介保持内部结构不变，简介继续保留半行滚动提示。
- 修正弹窗打开期间增加鼠标子树捕获，并继续在宿主窗口级拦截弹窗外鼠标移动、按下、抬起和触摸输入；标题栏按钮不再响应悬停、手势变化或点击，弹窗背景仍保持透明。
- 修正弹窗下拉框和选项补充手型光标；电影详情播放源下拉框使用显式模板显示当前播放源文本，不再回退显示对象类型名。
- 电影修正候选海报补充左侧阴影安全区：列表向左扩展并等量内缩海报内容，保持海报本体原位置不变。质感优化策略统一要求海报四周预留阴影或发光空间，扩展安全区时不得移动海报本体的既有对齐基线。
- 电视剧集修正候选的日历和影片图标统一进入固定 `16px` 容器，在保留文字底边对齐的前提下进行视觉居中；导演列和类型列扩宽，使国家/地区与语言继续右移。
- `修正到剧`、`修正到季`、`选择该季` 候选按钮统一为 `36px` 高度；展开季行右侧增加箭头槽位补偿，使 `修正到剧` 与 `修正到季` 的右边缘对齐。
- 未新增 DB / migration，未修改识别、修正事务或媒体库语义。

## 详情页与修正弹窗实测回归修复补充 2

- 季详情单季信息卡不再应用半行截断；上方 Hero 区增加一行高度并整体向上移动半个增量，左侧简介同步增高且继续保留半行截断提示。
- 修正弹窗打开期间在宿主窗口级拦截弹窗外鼠标和触摸输入；透明遮罩继续无色显示，同时阻断标题栏按钮和拖动区域交互。
- 输入框提示文字改为读取实际首个光标矩形后校准水平起点，不再依赖固定内边距推断。
- 修正弹窗下拉框点击后不再改变外框颜色；切换播放源继续保留搜索结果、当前修正模式和原状态文本。
- 修正弹窗搜索按钮明确覆盖继承的最小高度，固定为 `36px`；电影候选海报补充调色阴影、圆角遮罩和高光层。
- 修正结果列表改为逐行绘制上下分隔线；列表始终预留透明滚动条槽位，避免剧分组和未识别季分组展开前后箭头、按钮水平位移。
- 剧集修正与已有未识别季分组图标改为固定高度容器内垂直居中；分组按钮和季按钮复用同一条右侧基线。
- 未新增 DB / migration，未修改媒体库语义、扫描识别规则或修正事务。

验证：

- `dotnet build MediaLibrary.sln`：通过，0 warning / 0 error。
- `git diff --check`：无空白错误，仅有既有 CRLF 转换提示。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

## 观影历史与收藏夹 UI Follow-up

### 阶段目标

- 收口观影历史海报网格、日期控件、滚动恢复和收藏夹海报布局。

### 完成内容

- 观影历史海报卡片统一使用固定 `EqualSpacingWrapPanel` 间距，不再随导航栏展开/收起切换 `ItemWidth`，避免不同时间筛选下首列位置轻微偏移。
- 观影历史和收藏夹海报卡片左右间距统一增大。
- 观影历史原生 `DatePicker` 替换为本地适配的 SmartDate 风格控件：保留 `Control + Popup + UniformGrid` 日历结构，颜色和状态使用 XFVerse 动态资源。
- 修正观影历史内容重建时 `ScrollChanged` 把已保存滚动偏移覆盖为 `0` 的问题；切换导航页再回来应继续恢复滚动位置。
- 混合来源显示文案统一从 `本地 + 网盘` 改为 `本地/网盘`。
- 收藏夹 Tab 栏使用与观影发现一致的顶部偏移和内容区 Padding。
- 收藏夹海报区改为观影历史同款等间距布局，并将卡片内部替换为影片搜索海报结构。
- 喜爱 tab 的右上角操作改为无背景纯心形图标：已喜爱显示实心渐变爱心；想看 tab 继续使用无背景实心星形取消想看入口。
- 新增 SmartDate 第三方说明文件，记录 MIT 来源和本地适配范围。

### 明确未做事项

- 未引入外部二进制依赖或 NuGet 包。
- 未修改收藏夹、想看、喜爱、观看历史的业务语义。
- 未新增 migration、database update、commit 或 push。

### 验证结果

- `dotnet build MediaLibrary.sln`：因正在运行的桌面应用锁定默认 Debug 输出 DLL，默认输出路径 build 失败于文件复制阶段。
- `dotnet build MediaLibrary.sln -p:BaseOutputPath=<temp-build-output>`：通过，0 warning / 0 error。
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`：无输出。

### Known Issues

- Blocker：无。
- Deferred：需要在真实窗口尺寸下人工确认收藏夹海报 hover、右上角按钮和空状态。
- Noise：SmartDate 控件是本地适配版本，不是直接引用上游项目程序集。

## Watch History / Favorites / Scan Dialog Polish Follow-up

### Completed

- Movie-search poster want action now uses backgroundless vector star icons. Empty state uses a translucent frosted white star; selected state uses a metallic gold gradient while keeping the previous right-edge alignment.
- Favorites poster actions now use backgroundless vector heart/star icons copied into the movie-search-style poster surface.
- Watch history and favorites poster grids use stable spacing and visible vertical scrollbars to avoid horizontal drift when changing time filters or item count.
- Watch history scroll offset is persisted through navigation state, with an unload/layout guard that avoids overwriting a saved non-zero offset with a transient zero.
- Movie and Season detail not-interested actions now render vector exclamation / undo-arrow icons instead of font glyph strings.
- WebDAV path picker was restyled as a compact dialog with icon-only parent/forward/refresh controls, two-line directory rows, no subtitle, no bottom hint, and two evenly distributed action buttons.
- Scan progress metric icons now all use the same `Border + Viewbox + Path` structure as the checked scanned icon.

### Not Done

- No database schema, migration, database update, commit, or push was added.
- Runtime visual QA on the user's real display scale is still manual.

### Verification

- `dotnet build MediaLibrary.sln`: passed with 0 warnings and 0 errors.
- `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`: no output.

### Known Issues

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for collapsed/expanded sidebar spacing and WebDAV picker row density.
- Noise: Existing line-ending normalization warnings may still appear in Git output.

## Discovery And Favorites Poster Icon Cleanup

Completed:

- Replaced the Search and Favorites poster action Path stacks with single Segoe MDL2 glyph icons.
- Removed the extra shadow/highlight layers that made star and heart actions look like overlapping icons.
- Kept poster action buttons backgroundless and right-aligned, with reduced 30px hit targets and 20px glyphs.
- Added centered retry buttons to Discovery search/ranking error overlays using the shared secondary button style.

Not done:

- No broader theme, typography, card layout, or navigation redesign was changed.
- No database update, commit, or push was run.

Validation:

- `dotnet build MediaLibrary.sln -p:BaseOutputPath=<temp-build-output>` passed with 0 warnings and 0 errors after migration generation.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for icon perceived size on the user's display scale.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Scan Task Picker Polish Follow-up

Completed:

- Updated the WebDAV path picker to match the preference dialog's single glass-panel treatment instead of nesting a rounded dialog surface inside another rounded shell.
- Replaced the close action with an explicit Segoe MDL2 close glyph.
- Added a directory-content loading overlay with centered spinner and text.
- Reduced the directory list height, applied a modern auto-reveal scrollbar, and routed mouse-wheel scrolling over the list.
- Changed footer actions to auto-width buttons distributed across equal columns.
- Adjusted scan progress action-button placement so its left and right gaps stay equal around the button group as the page width changes.

Not done:

- No broader scan page redesign, theme overhaul, database update, commit, or push was run.

Validation:

- `dotnet build MediaLibrary.sln -p:BaseOutputPath=<temp-build-output>` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed in the real WPF window for the exact perceived spacing.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Poster Theme Icon Follow-up

Completed:

- Movie-search poster selected star now uses the active theme accent brush instead of a fixed gold treatment.
- Favorites poster selected heart and selected want-to-watch star now use the active theme accent brush instead of fixed red / gold treatments.
- The icons remain single Segoe MDL2 glyphs with transparent 30px hit targets.

Not done:

- No broader theme palette, layout, media-library semantics, database update, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for icon contrast on both light and dark themes.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Scan Task Configuration Layout Follow-up

Completed:

- Removed the framed card treatment from the local-configuration empty state text.
- Reworked the WebDAV path-add area into a title row with the path picker button, a one-line status row, and a four-row path list.
- Matched WebDAV path status truncation and tooltip behavior to the local-configuration status display.
- Reduced WebDAV path picker vertical density, including the current-directory card, directory list height, row spacing, and wheel-scroll step.
- Fixed WebDAV path picker confirmation so selecting a child directory returns that selected directory instead of the current parent directory.
- Increased scan progress metric card spacing only when the navigation sidebar is collapsed.

Not done:

- No scanning semantics, delete semantics, database update, migration, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for real WebDAV directory density, tooltip timing, and collapsed-sidebar card spacing.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Watch History And Favorites Poster Layout Rework

Completed:

- Watch history poster card layout now follows the media-library poster grid width rules: 194px item width by default, 201px when the navigation sidebar is expanded, 180px content width, and 288px item height.
- Watch history poster card margin now matches the media-library poster card spacing.
- Favorites poster lists now use the movie-search/media-library style `ListBox` plus `VirtualizingWrapPanel` instead of the previous `ScrollViewer` plus `ItemsControl` layout.
- Favorites poster list item sizing now follows the same collapsed/expanded sidebar width rules as media-library and movie-search posters.
- Favorites poster cards keep the movie-search poster visual structure, with the top-right icon action mapped to favorite removal on the favorite tab and want-to-watch removal on the want tab.
- Favorites selected tab is now stored through navigation state and is no longer reset to the favorite tab on page activation.

Not done:

- No collection semantics, media-library deletion semantics, database update, migration, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for exact poster spacing at the user's desktop scale and for favorite-tab persistence through the full navigation flow.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Scan Task WebDAV And Favorites Persistence Follow-up

Completed:

- Increased the WebDAV path picker dialog height by roughly one third and widened it slightly.
- Changed WebDAV directory list wheel scrolling to pixel-based scrolling with a smaller step so wheel movement is less jumpy.
- When adding or saving a WebDAV parent path, existing child paths are automatically removed and the status text reports the removed count and reason.
- Applied the same parent-path cleanup and removal-count status to local scan path add/save flows, including same-batch local child selection filtering.
- Matched the WebDAV account configuration card height to the add-path card and distributed the added space across the three input rows.
- Favorites now persists each tab's poster-list scroll offset through tab switches, detail navigation, and page reactivation.
- Favorites rating text now uses the same weighted TMDB/OMDb display rule as movie search, and collection list loading hydrates missing rating snapshots from the current movie records.

Not done:

- No database update, new migration, commit, push, scan matching rule, or media file deletion behavior was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed in the real WPF window for the WebDAV dialog dimensions, wheel feel, and favorites scroll restoration timing.
- Noise: Existing adjacent UI and migration follow-up changes remain in the working tree.

## Scan Task Multi-select And Rating Edge-case Follow-up

Completed:

- Fixed the WebDAV add-path card to a stable maximum height so the card no longer changes height as path count changes.
- Removed the rounded empty-state frame from the WebDAV add-path empty text.
- Kept the WebDAV account card at the same fixed height as the add-path card and redistributed the extra vertical space across the three input rows.
- Added multi-select support to the WebDAV path picker for add-path flow while keeping edit-path selection single-value.
- Added a hollow selection dot to every WebDAV directory row; selected rows fill the dot with the active theme accent.
- WebDAV batch add now filters same-batch child paths covered by a selected parent path and still reports skipped/removed counts.
- Favorites now hydrates TV season poster ratings after load using the same season TMDB plus series IMDb sources used by the media-library list.
- Poster rating rules now explicitly display a single available rating even without votes, and average TMDB/IMDb when both ratings have no vote counts.

Not done:

- No database update, new migration, commit, push, scanner matching rule, or media file deletion behavior was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for the fixed WebDAV card height, selection-dot contrast, and multi-select interaction feel.
- Noise: Existing adjacent UI and migration follow-up changes remain in the working tree.

## Scan Task Path Picker And Discovery Paging Retry Follow-up

Completed:

- WebDAV directory rows now only toggle selected state when the hollow selection dot is clicked; double-clicking the row body still enters the directory.
- Local folder picking now falls back to the Windows desktop path before user profile and documents when no initial path is available.
- Moved the WebDAV account card action row upward to avoid clipping in the fixed-height card.
- Movie search, TV search, movie ranking, and TV ranking page loading now use internal retry with rollback to the previous page state after repeated failures.
- Paging failure handling restores previous rows, page index, totals, and status text instead of leaving the screen stuck in a loading message.

Not done:

- No visible retry button was added to the UI; retry is an internal paging safeguard.
- No database update, new migration, commit, push, scanner matching rule, or media file deletion behavior was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for WebDAV dot-only selection, desktop default picker behavior, account-card row spacing, and transient network retry timing.
- Noise: Existing adjacent UI and migration follow-up changes remain in the working tree.

## Scan Account Layout And Ranking Pager Diagnostics Follow-up

Completed:

- Moved the WebDAV account connection status text from the bottom action row to the row directly under the account title, matching the add-path card status row alignment.
- Changed the WebDAV account card bottom action row to two evenly distributed columns while keeping each button width content-sized.
- Aligned ranking previous/next command availability with the actual active ranking pager state, instead of only checking whether ranking loading is idle.
- Added ranking next-page diagnostics at the UI pointer layer and ViewModel command layer to distinguish missed UI events, disabled command state, and ViewModel rejection.
- Added TV ranking diagnostics for display-page load, source fetch, status resolution, stale requests, cancellation, and failures to match the movie ranking logging coverage.

Not done:

- No database update, new migration, commit, push, scanner matching rule, or media file deletion behavior was changed.
- No new visible ranking retry control was added.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for account-card row spacing and for reproducing the first-ranking-next-click issue with the added diagnostics.
- Noise: Existing adjacent UI and migration follow-up changes remain in the working tree.

## Ranking Paging Cancellation And Timeout Follow-up

Completed:

- Diagnosed the first ranking next-page no-op from the ranking diagnostics: the UI click and command were both reached, but the source fetch was immediately canceled because paging reused an already canceled ranking cancellation token.
- Movie search, TV search, movie ranking, and TV ranking paging now recreate canceled page-load cancellation token sources before starting a new page request.
- Interactive page loading now applies a short per-attempt timeout before retrying, so disconnected-network paging does not wait for the full lower-level TMDB timeout cycle.
- Paging failure rollback messages now include a clear "loading failed, please retry later" prompt, with timeout failures displayed as request timeouts.
- Confirmed favorites posters use the shared poster cache image pipeline, which already retries remote poster downloads with bounded per-attempt timeouts and cooldowns.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media file deletion behavior, or visible retry button was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for the new paging timeout duration and failure text under disconnected-network conditions.
- Noise: Existing adjacent UI and migration follow-up changes remain in the working tree.

## Bottom Edge And Scroll-Driven Corner Follow-up

Completed:

- Removed the fixed bottom viewport edge on watch history, watch insights, favorites, scan tasks, and API configuration by extending each affected scroll root to the page bottom instead of stopping at the shell content margin.
- Added reusable `ScrollDrivenBottomCornerBehavior` as a WPF attached behavior for large content cards.
- Applied the scroll-driven bottom-corner behavior to movie discovery search results, movie discovery rankings, and the media library content body.
- Split correction dialog combo box styling so correction-target dropdowns stay flat while playback-source dropdowns keep the elevated shadow.
- Removed the always-visible media library batch toolbar cancel button; the running AI identification cancel action remains on the batch-selection toggle while the operation is cancellable.
- Added theme-specific selected-dot brushes so selected batch dots are light in light theme and dark in dark theme.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, or business-state semantics were changed.
- No full runtime screenshot sweep was performed in this pass; visual acceptance is still manual.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff was checked and remained empty.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for the bottom-edge removal and scroll-driven corner transition on real long lists.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Pointer Cursors And Tooltip Follow-up

Completed:

- Added hand cursors to shell navigation items, the sidebar collapse button, and the account menu button.
- Narrowed ranking detail navigation so only posters and primary titles open movie or TV details; ranking date, director, cast, and overview text no longer trigger detail navigation.
- Removed the broad hand cursor from favorites poster cards while keeping the existing card click behavior.
- Added hand cursors to media-library poster cards and list rows to match their existing open or batch-select click behavior.
- Expanded the about row hover background to the full row width, aligned its hover color with the accent theme brush, and removed its tooltip.
- Removed tooltips from movie discovery search and ranking pager buttons, removed the movie search input tooltip, and changed the home AI status tooltip to only appear on visually trimmed status lines.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, or business-state semantics were changed.
- No runtime screenshot sweep was performed in this pass; cursor and hover acceptance remains manual.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Targeted `rg` checks confirmed the removed pager/search/about/home status tooltips no longer exist at the requested call sites.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for pointer cursors, ranking click targets, and about-row hover width in both themes.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Dropdown Poster Progress And Scroll Clipping Follow-up

Completed:

- Removed elevated popup shadows from media-library filter menus, AI recommendation filter menus, and correction-target combo dropdown popups to match the flat movie-search dropdown visual.
- Kept the correction-dialog playback-source combo elevation behavior unchanged.
- Aligned favorites poster bottom metadata with the movie-search poster tag grouping: genre/display tags, emotion tags, and scene tags now drive the three visible poster tag groups.
- Added TV season air date propagation into collection favorites items so favorite season cards can show a season date when movie-search-style fields do not exist.
- Aligned favorites poster rating badge placement with the movie-search rating badge by right-aligning the badge inside the poster overlay.
- Added theme-aware stroke and glow to ranking score badges.
- Added theme-aware track stroke plus animated indicator stroke/glow to the scan-task top scan progress bar only; realtime progress bars were not changed.
- Increased scan progress animation speed and cached the animated indicator to reduce the low-frame-rate feel while scanning.
- Fixed scan-task and API configuration scroll roots so scrolled cards are clipped below the title or tab area instead of drawing through it.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, or business-state semantics were changed.
- No runtime screenshot sweep was performed in this pass; dropdown, poster, glow, and clipping acceptance remains manual.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Targeted `rg` checks confirmed the removed popup shadow bindings and old favorites tag helper references are absent at the requested call sites.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for dropdown corners, favorites poster metadata, scan progress smoothness, and scroll clipping in both light and dark themes.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Home Continue Watching Spacing Follow-up

Completed:

- Reduced the visible gap between the home "Continue Watching" title row and poster top from 3 to 2 layout pixels, using `x = 1`.
- Reduced the home "Continue Watching" row height by `y = 12` and moved that height to the "Recently Added" row so the lower section grows upward while its bottom edge stays fixed.
- Re-anchored the "Continue Watching" right-side navigation button to the poster content row with a fixed top offset so it remains vertically centered against the 237px posters after the row-height adjustment.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, or business-state semantics were changed.
- No runtime screenshot sweep was performed in this pass; home-page spacing acceptance remains manual.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for the home "Continue Watching" and "Recently Added" spacing on the target window sizes.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## User Profile Dialog Refresh Follow-up

Completed:

- Reworked the user profile dialog as a transparent rounded dialog surface so the old rectangular outside background no longer appears.
- Added shadow-safe inner profile cards with inline glow/shadow and enough outer drawing room to avoid clipping.
- Replaced the placeholder `XF` mark with the same vector logo geometry used by the app shell.
- Enlarged and right-aligned the close button, matching the header title vertical center and the inner-card right edge.
- Split the body into an avatar card and a basic-information card; removed the local-profile badges and promoted the user name to the dialog title size.
- Replaced the text edit/save button with a pure icon edit button that switches to a pure check icon while editing.
- Removed the bottom status banner plus cancel/done buttons, making the dialog shorter and content-driven.
- Added local persistent user-profile storage for name, account, phone, email, gender, age, signature, and avatar path through a JSON-backed app profile service; no database schema was changed.
- Wired the main shell account area to the same user-profile service so saved profile name/avatar data is reflected outside the dialog.
- Swapped email and signature positions, tightened input heights, constrained signature editing to the two-line display budget, and added avatar remove/add editing with desktop-start file picker and center circular crop.
- Added bottom-centered transient profile toasts aligned with the movie-search toast pattern: green for saved/no-change success, yellow for local warnings, red for save failures.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, login backend, cloud account, or sync behavior was changed.
- No runtime screenshot sweep was performed in this pass; profile dialog visual acceptance remains manual.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Targeted `rg` checks confirmed the old profile draft class, bottom button row, cancel/done handlers, and bottom status banner are absent from the profile dialog.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for profile dialog shadows, avatar editing, toast placement, and exact compact dialog height in both themes.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Scroll Corner And Shell UI Follow-up

Completed:

- Updated the scroll-driven bottom-corner behavior to refresh synchronously on load, avoiding the visible full-corner to square-corner transition when entering media-library, movie-search, and ranking surfaces.
- Added bottom breathing room to the media-library, movie-search, and ranking content cards so restored bottom corners are not clipped at the page edge when scrolled to the end.
- Brightened the dark-theme media-library batch-selection selected dot.
- Simplified the shell avatar popup toggle path so clicking the avatar button again closes the popup, and removed the popup shadow that produced dark corner artifacts.
- Limited the favorites poster hand cursor and open command to the poster visual layer instead of the outer item surface.
- Constrained the settings about row to the content column so its hover highlight and top divider no longer extend toward the sidebar.
- Increased the visible text area inside movie-search and media-library search boxes without increasing the input height.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, or business-state semantics were changed.
- No runtime screenshot sweep was performed in this pass; visual acceptance remains manual.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- Migration diff was checked and remained empty.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for the scroll-corner bottom state, dark-theme selection dot contrast, avatar popup corners/toggle, favorites cursor hit area, settings about row clipping, and search input descenders in both themes.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## AI Tag Refresh And Fallback Follow-up

Completed:

- Changed movie AI tag prompts to request 2 to 4 tags per type, emotion, and scene category while still accepting fewer without local supplementation.
- Centralized AI tag result handling so extra tags are trimmed, missing emotion/scene results become `-`, and missing type results fall back to TMDB/source type tags before `-`.
- Removed the old local overview-based emotion/scene backfill from movie classification and recommendation candidate tag handling.
- Moved detail-page auto classification to background work that survives navigating back to movie search or rankings, then notifies metadata refresh when tags are written.
- Relaxed and corrected external movie tag caching so completed placeholder results are cached and partial old cache entries can still trigger a new classification.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, or business-state semantics were changed.
- No live AI request was executed in this pass; AI response behavior remains dependent on runtime provider availability.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only existing LF-to-CRLF warnings were reported.
- Migration diff was checked and remained empty.

Known Issues:

- Blocker: None.
- Deferred: Manual runtime acceptance is still needed for search/ranking return flows after a real AI classification completes.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Scroll Corner And Menu Polish Follow-up

Completed:

- Changed the scroll-driven bottom-corner behavior to keep its visual state across page unload/reload and to interpolate bottom radius and bottom margin based on distance from the scroll bottom.
- Treated no-scroll/empty states as bottom states so media-library, movie-search, and ranking cards keep bottom rounding and breathing room when no content is present.
- Added bottom scroll padding for media-library poster/list layouts and movie-search poster results so card bottoms and search pagers no longer cover the final content row.
- Hardened the shell avatar popup toggle so a button click that closes the popup is not immediately interpreted as a new open action.
- Limited the favorites card click hand area to the poster hit region and made the bottom metadata area use the normal arrow cursor.
- Shifted the settings about-row chrome left edge inward by 0.5px.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, or business-state semantics were changed.
- No runtime screenshot sweep was performed in this pass; visual acceptance remains manual.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for bottom-corner transitions, search pager spacing, avatar menu toggle behavior, favorites cursor hit area, and the about-row 0.5px inset.
- Noise: Existing adjacent UI follow-up changes remain in the working tree.

## Scroll Corner External Bottom Gap Follow-up

Completed:

- Reverted the bottom gap overlay approach because the fake bottom edge could not reliably match page background color, card corner masking, and static glow/shadow in dark theme.
- Restored dynamic bottom margin for the bottom-state external gap, with a release threshold so the margin is not immediately removed by its own viewport-size change.
- Locked the bottom margin to the scroll offset where bottom state is first reached, so poster and ranking layouts do not oscillate when layout recalculates scrollable height at the bottom.
- Removed bottom clipping and overlay layers from the main cards so the restored bottom border, glow/shadow, and search/ranking pager controls remain native card content.
- Added bottom spacing to movie-search pagers and slightly larger bottom spacing to ranking pagers so they sit above the bottom-state gap.
- Treated `CanContentScroll` list scroll viewers as logical-scroll surfaces, so media-library list layout no longer starts restoring bottom corners many items before the actual bottom.
- Applied the external bottom gap behavior to media-library, movie-search, and ranking content cards without moving their top edge.
- Kept the main content cards' bottom padding at zero so the card chrome does not form a fixed bottom strip over scrolling content.
- Reverted the movie-search and ranking list/grid bottom spacing that incorrectly put the requested gap inside the card.
- Kept the media-library list `ListBox` bottom padding removal so its list layout no longer has an extra internal bottom gap compared with movie-search list layout.

Not done:

- No database update, new migration, commit, push, scanner matching rule, media deletion behavior, or business-state semantics were changed.
- No runtime screenshot sweep was performed in this pass; visual acceptance remains manual.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None.
- Deferred: Manual visual acceptance is still needed for bottom-corner transitions and external bottom gap behavior in media-library, movie-search, and ranking poster/list layouts.
- Noise: Existing adjacent UI and AI-tag follow-up changes remain in the working tree.

## 2026-06-13 - Watch History Theme Refresh Follow-up

Completed:

- Watch History page header, status, and empty-state text now use dynamic theme brushes instead of static theme brush references.
- Active date-filter button accent brushes also use dynamic resources, reducing stale colors after switching between light and dark themes without leaving the page.
- A focused scan of page/control theme brush references found the same stale theme-brush pattern only on Watch History; poster overlay brushes on other pages were left unchanged because they are local fixed overlay colors.

Not done:

- No layout, navigation, data query, scan behavior, database update, migration, commit, or push was changed for this UI fix.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only existing LF-to-CRLF conversion warnings were reported.
- Migration diff was checked and remained empty.

Known Issues:

- Blocker: None found in code inspection.
- Deferred: Manual WPF acceptance is still needed for live theme switching while staying on Watch History.
- Noise: Existing adjacent UI and profile-dialog changes remain in the working tree.

## 2026-06-13 - Profile Dialog Logo And Toast Follow-up

Completed:

- Updated the sidebar navigation logo to use a white outline with the same visual weight as the profile-dialog logo, drawn directly on the black rounded logo edge so no transparent gap remains.
- Changed the profile signature edit box to a top-aligned two-line input shape so the caret starts at the first line instead of the vertical center.
- Moved profile toast placement much higher by targeting the upper screen area and falling back closer to the dialog top.

Not done:

- No business behavior, profile persistence schema, database update, migration, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None found in code inspection.
- Deferred: Manual visual acceptance is still needed for the sidebar logo outline, signature caret position, and higher save toast position.
- Noise: Existing adjacent UI and feature follow-up changes remain in the working tree.

## 2026-06-14 - Cached Shadow And Scroll Corner Follow-up

Completed:

- Disabled cached shadows on transparent Movie Discovery ranking item wrappers, so the overview/info area no longer receives a full-card shadow behind the text.
- Added theme-level cached-shadow blur-radius tokens and routed shared glass-card, Watch Insights, scan, profile, movie-detail, watch-history, discovery score badge, Home poster base, and scan-progress cached shadows through them.
- Light theme cached card shadows now use blur radii aligned with the old light-theme `DropShadowEffect` values; dark theme keeps the stronger glow-style radii.
- Fixed `ScrollDrivenBottomCornerBehavior` so it no longer assumes bottom state before an active scroll viewer is available, and releases a stale bottom state when content becomes scrollable after initial layout.

Not done:

- Poster palette shadows remain image-color driven and keep their poster-specific blur values.
- No database update, migration, ranking/search/recommendation logic, scan behavior, media-library semantics, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln` passed with 0 warnings and 0 errors.

Known Issues:

- Blocker: None confirmed by build.
- Deferred: Manual WPF validation is still needed for Movie Discovery ranking info shadows, light/dark cached-shadow density, and initial bottom-corner state in media-library, movie-search, and ranking non-empty views.
- Noise: Existing adjacent UI and feature follow-up changes remain in the working tree.

## 2026-06-14 - Cached Shadow Tightening And Empty-State Corner Fix

Completed:

- Reduced cached-shadow blur radii slightly again so large, inline, compact, score-badge and scan-progress shadows look more concentrated in both light and dark themes.
- Restored empty / non-scrollable surfaces to the bottom state for scroll-driven bottom corners.
- Added scrollable-height tracking for the bottom-margin state, so a stale initial bottom state can be released after content becomes scrollable without causing a real bottom-state round/flat loop.

Not done:

- No page data, ranking/search/recommendation logic, scan behavior, database update, migration, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- Normal output build was blocked by a running `MediaLibrary.App` process holding the app exe.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual WPF validation is still needed for empty-state bottom corners, true bottom-state stability, and the updated shadow density.
- Noise: Existing adjacent UI and feature follow-up changes remain in the working tree.

## 2026-06-14 - Stronger Concentrated Cached Shadows

Completed:

- Tightened cached-shadow blur radii again and increased cached-shadow opacity for large, inline, compact, score-badge, and scan-progress surfaces in both light and dark themes.
- Kept the cached bitmap strategy, theme color split, depth values, poster palette shadows, bottom-corner behavior, and page layouts unchanged.

Not done:

- No database update, migration, ranking/search/recommendation logic, scan behavior, media-library semantics, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual WPF validation is still needed for the final perceived shadow/glow strength in light and dark themes.
- Noise: Existing adjacent UI and feature follow-up changes remain in the working tree.

## 2026-06-14 - Poster Shadow Strength And Edge Safe Area Follow-up

Completed:

- Restored poster base shadows that had been routed through generic cached-shadow compact tokens back to the original poster-specific black shadow parameters while keeping the cached bitmap strategy.
- Restored the Home poster base shadow to the original `BlurRadius=14`, `Opacity=0.28`, `ShadowDepth=3`, `#000000` visual parameters.
- Made `VirtualizingWrapPanel.ViewportPadding.Left` participate in width and arrangement calculations, so poster grids can reserve real left-edge shadow space.
- Added left/right/top safe padding for Media Library and Movie Discovery search poster grids to avoid clipping edge poster glow.
- Slightly strengthened light-theme cached shadow opacity without widening the blur radii.

Not done:

- No database update, migration, ranking/search/recommendation logic, scan behavior, media-library semantics, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual WPF validation should confirm Media Library and Movie Discovery search edge posters are no longer clipped, and light-theme cached shadows are strong enough without becoming diffuse.
- Noise: Normal output build may still be blocked if the app process is running; temp-output build is acceptable for this visual-only pass.

## 2026-06-14 - Phase 7 Poster Viewport Compensation Correction

Completed:

- Superseded the previous direct left/right `ViewportPadding` and `VirtualizingWrapPanel` width-calculation change because it altered available layout width and could change poster columns.
- Restored `VirtualizingWrapPanel` to the accepted Phase 7 behavior where poster paint padding is not subtracted symmetrically from the virtualized column width.
- Reapplied the Phase 7 shadow-safe model to Media Library and Movie Discovery search: expand the ListBox viewport by 34px on the clipped sides, then compensate top position and column-count width through the existing panel padding so poster coordinates and column count stay unchanged.
- Reapplied the existing Movie Discovery ranking `ScrollViewer` compensation model on the right side: expand the viewport by 24px and add the same 24px to the inner root Grid, preserving all content coordinates while exposing score-badge glow.
- Increased palette/base shadow opacity for non-detail posters on Home, Media Library, Favorites, Watch History, Movie Discovery, AI Recommendations, and Watch Insights. Blur radius, shadow bitmap padding, poster dimensions, and detail-page poster opacity remain unchanged.
- Increased the expanded-sidebar account avatar from 32px to 38px inside a fixed 32px layout column, so its right edge stays fixed and the extra size grows leftward. Collapsed-sidebar avatar size remains 32px.

Not done:

- No detail-page poster glow strength was changed.
- No database update, migration, search/ranking/recommendation logic, scan behavior, media-library semantics, commit, or push was changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.
- `git diff -- src/MediaLibrary.App/Controls/VirtualizingWrapPanel.cs` returned empty after restoring the Phase 7 layout baseline.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual WPF validation should confirm poster coordinates and column counts are unchanged, all non-detail poster glows are stronger, edge glows are visible, and the expanded avatar grows only to the left.
- Noise: The previous edge-safe log entry is retained as history but its direct panel-width approach is superseded by this correction.

## 2026-06-14 - Poster Top Gutter And Avatar Clipping Correction

Completed:

- Removed negative top margins from Media Library and Movie Discovery poster ListBoxes. Top shadow space is now real scroll-content padding, so poster bodies cannot cross above the content viewport while scrolling.
- Kept the horizontal viewport expansion and right-side panel compensation, preserving virtualized poster column count and horizontal coordinates.
- Added 36px of real top content gutter to Movie/TV ranking ScrollViewers so the first hero poster glow is not clipped.
- Strengthened all non-detail cached poster glows again while leaving Movie, Series, Season, and Episode detail-page values unchanged.
- Changed the expanded account avatar from overflow inside a 32px column to a 38px layout column plus a 6px left render translation. The right edge and text start remain fixed; collapsed size remains 32px.

Not done:

- No application launch or manual visual verification was performed.
- No service, navigation, search, ranking, recommendation, media-library, database, migration, commit, or push behavior was changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual WPF validation should confirm scroll content stays below the Media Library/Search content boundary, ranking first-row glow is complete, and the expanded avatar is no longer clipped.
- Noise: The real top gutter intentionally adds paint room before the first poster row instead of moving the ScrollViewer above its content boundary.

## 2026-06-14 - Poster Glow Midpoint And First-row Spacing Restoration

Completed:

- Reduced the latest non-detail poster glow increase by half. Palette/base values now use the midpoint between the previous and latest versions.
- Restored Media Library and Discovery search top viewport padding to 10px, removing the added gap above the first poster row.
- Retained normal vertical viewport boundaries, horizontal edge paint room, ranking hero top-gutter correction, and expanded-avatar clipping correction.

Not done:

- No detail-page poster values or application behavior changed.
- No application launch, database update, migration, commit, or push was performed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual validation should confirm the restored first-row spacing and midpoint glow strength.
- Noise: Horizontal paint compensation remains intentionally independent of vertical first-row spacing.

## 2026-06-14 - Poster Scrollbar Alignment Follow-up

Completed:

- Moved the Media Library poster-view scrollbar 34px left with a render-only transform, returning it inside the original content boundary without affecting virtualized layout width.
- Applied the same 34px correction to Discovery Movie/TV poster search scrollbars.
- Moved ranking scrollbars by the smaller 20px amount and reduced ranking first-row top gutter from 36px to 18px.

Not done:

- No poster grid, shadow resource, search/ranking data, database, migration, commit, or push behavior changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors after removing verified stale Codex temp build outputs that had exhausted temporary disk space.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.
- `VirtualizingWrapPanel` remained unchanged.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual validation should confirm scrollbar positions and ranking first-row spacing.
- Noise: The first build attempt failed only because the temporary volume was full; the retry passed after scoped temp-output cleanup.

## 2026-06-14 - Correction Playback-source Selector Shadow

Completed:

- Added a `CachedShadowBorder` only around the playback-source selector in the Season correction episode-mapping panel.
- Used cached inline theme tokens, preserving the static local-resource shadow strategy.
- Left ordinary correction ComboBoxes and detail-page playback-source selectors unchanged.

Not done:

- No other dropdown, correction field, mapping behavior, database, migration, commit, or push behavior changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual validation should confirm the playback-source selector shadow in both themes.
- Noise: The scope is intentionally one correction-dialog control instance.

## 2026-06-14 - Correction Playback-source Shadow Paint-box Correction

Completed:

- Fixed the invisible cached shadow by moving its card boundary inside a real `10,6` paint gutter within the existing 48px mapping row.
- Kept row height and surrounding controls unchanged.
- Raised only the playback-source selector to ensure its cached shadow is not covered by sibling content.
- Used opacity `0.40` with the existing theme-specific cached inline color/blur/depth resources.

Not done:

- No other dropdown, popup, detail-page selector, correction behavior, database, migration, commit, or push behavior changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual validation should confirm the closed playback-source selector now has visible shadow/glow in both themes.
- Noise: The paint gutter reduces only the selector's internal usable width by 20px.

## 2026-06-14 - Correction Playback-source Target-instance Correction

Root cause:

- The prior implementation modified the Season episode-mapping source selector, not the actual Movie/Episode correction-dialog header selector reported by the user.

Completed:

- Reverted the incorrect Season mapping wrapper.
- Added cached shadow hosts only to the Movie and Episode correction HeaderContent playback-source selectors.
- Preserved the original selector bounds through equal negative-margin and padding compensation.
- Applied local opacity `0.40` with theme-specific cached inline shadow color, blur and depth.

Not done:

- No ordinary ComboBox, Season mapping selector, normal detail-page playback selector, correction behavior, database, migration, commit, or push behavior changed.

Validation:

- `dotnet build MediaLibrary.sln -p:OutDir=<temp>` passed with 0 warnings and 0 errors.
- `git diff --check` passed; only LF-to-CRLF working-copy warnings were reported.

Known Issues:

- Blocker: None confirmed by temp-output build.
- Deferred: Manual validation should confirm Movie and Episode correction-header playback-source selectors in both themes.
- Noise: The previous Season mapping shadow entries are retained as superseded implementation history.

## 2026-06-15 - Home Library Preview Card Layout Refinement

Completed:

- Reduced the four library-preview metric cards from the bottom by 8px while preserving their top position.
- Centered each comparison text against its metric card independently of the trailing trend arrow.
- Replaced the uniform four-column panel with four equal card columns and three explicit gap columns.
- Preserved the expanded-sidebar 12px gaps; when the sidebar collapses, each gap grows by 26px to 38px. The remaining 78px of the 156px content-width increase is shared equally by the four cards.

Not done:

- No home data, navigation behavior, other home sections, database, migration, commit, or push behavior changed.

Validation:

- `dotnet build MediaLibrary.sln -p:UseAppHost=false -p:OutputPath=<temp>` passed with 0 warnings and 0 errors.
- Focused XAML inspection confirmed the 12px/38px gap templates, four equal card columns, 8px bottom reduction, and symmetric trend-arrow spacer.

Known Issues:

- Blocker: None identified during implementation.
- Deferred: Manual validation should confirm the expanded/collapsed spacing and comparison-text centering at the target window size.
- Noise: None.
