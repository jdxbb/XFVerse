# Watch Insights Known Issues

## 2026-06-16 Watch Insights Micro Layout Polish

Blocker:
- None identified by static patch review.

Deferred:
- Verify exploration DNA reads as a compass and all DNA icons remain aligned.
- Verify DNA progress labels are no longer clipped and scrollable DNA analysis text shows complete lines.
- Verify preference waves travel farther, curve less, move slowly, and fade away naturally.
- Verify the watch-vs-like conclusion sits slightly higher without colliding with the triptych.
- Verify selected taste-combination links are fully opaque and the Top5 right card/count placement matches the requested offset.
- Verify the rhythm chart low/middle/high grid lines remain equally spaced with larger separation.
- Verify calendar previous/next month controls align farther left with the calendar grid.
- Verify light-theme selected/hovered tab text has a dark-gray outline.

Noise:
- These are visual-only offsets and animation constants; final acceptability depends on the running WPF window size and active theme.

## 2026-06-16 Profile Gender Validation And Prompt Export

Blocker:
- None identified by solution build.

Deferred:
- Verify the profile gender combo box opens on click, closes on the next click, and opens again on another click.
- Verify the default gender is female for new or previously empty profiles.
- Verify phone, age, and email validation keeps the profile dialog in edit mode and marks only invalid fields.
- Verify red validation text/background/border adapt correctly in light and dark themes.
- Verify editing an invalid phone, age, or email field immediately clears the red validation state.
- Verify Watch Persona poster and palette switch to male/female assets after profile gender changes.

Noise:
- Phone, age, and email remain optional; validation applies only when a value is entered.
- Prompt template exports are local files outside the repository and do not change runtime prompt behavior.

## 2026-06-16 Taste Combination Top5 And Triple-Only Graph Polish

Blocker:
- None identified by solution build.

Deferred:
- Verify the right Top5 card is left-shifted and slightly lower without colliding with the graph.
- Verify Top5 rows show rank, combination, and occurrence count only, with no progress bar and all three aligned vertically.
- Verify rank badge borders are darker than their fill in both themes.
- Verify occurrence counts sit closer to the combination content.
- Verify graph columns show at most four labels and only labels that appear in complete type x emotion x scene combinations.
- Verify columns with fewer than four labels remain evenly distributed across the four-slot height.
- Verify graph links are only generated from visible complete three-category combinations.
- Verify selected-link glow looks soft and not like a hard outline.

Noise:
- The graph visual source is now Top10 complete combinations; the core statistics snapshot still keeps aggregate pair-edge data for other potential consumers.

## 2026-06-16 Tab Gray Outline And Delta Arrow Placement Correction

Blocker:
- None identified by solution build.

Deferred:
- Verify selected and hovered tab labels use light-gray outline in light theme and dark-gray outline in dark theme.
- Verify the status subtitle and tab-right status text outline treatment is unchanged.
- Verify non-zero overview arrows appear immediately after the `compared with last month` delta number for status cards, total watch time, and watch-day count.
- Verify zero and missing previous-month delta rows still hide arrows.

Noise:
- This entry supersedes the earlier black/white tab-outline validation item from the previous chrome-outline pass.

## 2026-06-16 Quadrant Rhythm Calendar Waves And Watch-Like Polish

Blocker:
- None identified by solution build.

Deferred:
- Verify the taste-quadrant `A x B` heading colors match the visible X/Y axis labels, with the `x` separator readable but secondary.
- Verify the coordinate canvas is slightly left-shifted without clipping the right-side axis label or point halo.
- Verify expanded-sidebar layout raises the quadrant heading/body group enough without colliding with the module header.
- Verify the viewing-time curve and fill follow the blue/purple/pink-orange/red height ramp in both themes.
- Verify `<30min` calendar days use the deeper blue and the month navigation is centered on the calendar grid.
- Verify preference waves originate from the content-area edges, travel far enough, and remain visible long enough to read as water waves from multiple directions.
- Verify the Watch-vs-Like cards and conclusion sit slightly higher and the wanted star badge is only subtly raised.

Noise:
- The rhythm chart value colors are local to `SplineAreaChart`; theme resources still control the rest of the module chrome.

## 2026-06-16 Watch DNA Icon And Overflow Height Polish

Blocker:
- None identified by solution build.

Deferred:
- Verify the six Watch DNA icons render as no-background pure icons at the same slot size and position as the previous placeholders.
- Verify type, emotion, scene, and narrative icons match their corresponding chip color family; rhythm is cyan-blue and exploration is purple.
- Verify a long description in any one DNA card makes all six cards slightly taller.
- Verify non-progress DNA cards move their chip row and description down together when the shared overflow state is active.
- Verify rhythm/exploration progress cards keep the progress bar and labels fixed while only the analysis text moves down.

Noise:
- The icons are vector paths embedded in the Watch Insights XAML; no new image assets were added.

## 2026-06-16 Persona Lead Brackets And Acrylic Paint Label

Blocker:
- None identified by solution build.

Deferred:
- Verify persona lead brackets are inline with the lead copy and read as one sentence in both expanded and collapsed sidebar layouts.
- Verify the brackets inherit the lead font while matching the muted taste-summary quotation-mark color/opacity in light and dark themes.
- Verify the persona label text itself has not gained extra projection shadows after the paint backdrop change.
- Verify the acrylic paint backdrop is opaque, connected from top to bottom, visibly textured, and angled right-up to left-down by roughly 30 degrees.
- Verify the paint top/bottom edges are not clipped and no old thin edge-stroke line remains inside the backdrop.

Noise:
- The acrylic paint is rendered with vector paths and palette-derived colors; no new bitmap paint texture asset was added.

## 2026-06-16 Chrome Outline And Overview Delta Arrows

Blocker:
- None confirmed by temp-output build.

Deferred:
- Verify selected and hovered tab labels use black outline in light theme and white outline in dark theme.
- Verify the collapsed status subtitle and tab-right status text have visibly thicker outlines, with white in light theme and black in dark theme.
- Verify zero month-over-month overview metrics show `较上月无变化`.
- Verify non-zero overview metrics show an up/down arrow immediately to the right of the current value without pushing the unit text awkwardly.
- Verify the same delta treatment works for overview status cards, total watch time, and watch-day count.

Noise:
- Default output build remains blocked while the running app executable is open; temp-output build is the validation path for this pass.

## 2026-06-16 Taste Combination Graph Layout And Bezier Links

Blocker:
- None confirmed by solution build.

Deferred:
- Verify left graph node spacing and the taller module remain balanced at supported window widths.
- Verify the right Top5 card sizes to content, centers vertically against the graph, and has no excessive lower blank area.
- Verify Top5 row spacing, rank-to-content spacing, and `x次` progress-row alignment match the requested visual rhythm.
- Verify selecting a type, emotion, or scene node highlights every pair link belonging to Top10 combinations that include that node.
- Verify node tooltips appear faster and show `标签：次数` plus up to three `组合：次数` lines.
- Verify Bezier links are restrained, do not pass through node interiors, and keep their endpoints on node edges.
- Verify inactive-link opacity, selected-link glow, and source-to-target gradients remain readable in both light and dark themes.

Noise:
- The selected-link glow is implemented as a second thicker low-opacity path rather than a blur effect.

## 2026-06-16 Watch-vs-Like Triptych Size And Shadow Safe Area

Blocker:
- None identified by static inspection.

Deferred:
- Verify the default middle card covers the right card less while still reading as the primary card.
- Verify the `经常想看` star badge is larger but remains centered in the same badge position.
- Verify all three larger cards expand visually downward and keep stable hover hit targets.
- Verify the top and bottom cached shadows are no longer clipped in the running WPF window.
- Verify the conclusion panel sits slightly higher and does not collide with the triptych stage at supported widths.

Noise:
- The triptych stage and Viewbox intentionally allow overflow so cached bitmap shadows can render outside card bounds.

## 2026-06-16 Quadrant Calendar And Local Preference Waves

Blocker:
- None confirmed by solution build.

Deferred:
- Verify the taste-quadrant title/body group is high enough in both expanded and collapsed sidebar layouts, and the `A 标签 x B 标签` line is centered against the body text.
- Verify the coordinate view is raised without clipping the top axis label.
- Verify the calendar month navigation is centered on the calendar, the lower legend is centered below the calendar, and the three metric cards sit higher.
- Verify local preference waves emit from varied random zones, curve more strongly near corners/edges, and apply a visible but not edge-pinning force.

Noise:
- Prompt version `wi-profile-persona-23-parallel-v17-quadrant-brief` intentionally leaves existing cached quadrant text unchanged until manual profile refresh.

## 2026-06-16 Persona Lead And Tag Paint Polish

Blocker:
- None confirmed by solution build.

Deferred:
- Verify entering Watch Insights no longer crashes after the inline lead `Run.Text` bindings were changed to explicit one-way bindings.
- Verify the persona lead inline corner brackets use the intended separate bracket font and stay correctly placed in expanded and collapsed sidebar layouts.
- Verify the persona type paint reads as a thicker right-up to left-down diagonal graffiti stroke in both light and dark themes.

Noise:
- Exact brush thickness and bracket color are visual-review sensitive and may need one more tuning pass after looking at the running WPF page.

## 2026-06-16 Stage4 Visual Tree Split

Blocker:
- None confirmed by temp-output build.

Deferred:
- Verify first entry logs now show profile stages 4/5/6/7 separately instead of one large stage-4 burst.
- Verify `initial-loading-hidden` is recorded only after `visual-tree-ready` for Profile and Statistics.
- Verify the watch-vs-like module looks unchanged after loading completes, including default center focus, hover focus, overlap, opacity, scale, and shadow.

Noise:
- The loading overlay may remain visible slightly longer because it now waits for the final render-commit gate instead of hiding as soon as the final stage flag is set.

## 2026-06-16 Chrome Contrast, Shadow Safe Area, And Loading Diagnostics

Blocker:
- None confirmed after temp-output build validation.

Deferred:
- Verify selected and hovered tab text has a white outline in light theme and a black outline in dark theme, without changing inactive tab text.
- Verify the collapsed titlebar subtitle and tab right status text read as ink black with white outline in light theme and fog white with black outline in dark theme.
- Verify the titlebar-tab separator and tab-bottom separator are still visible after the added transparency.
- Verify Profile Analysis and Watch Statistics module-card right-side glow is no longer clipped at the scroll viewport edge.
- The first-entry spinner hitch remains a performance issue by request: current logs point to late profile stage-4 visual materialization after the loading overlay is hidden, with a 931ms max slow frame in the latest first-entry trace.

Noise:
- The normal output build is blocked while the running app executable is open; existing `watch-insights-perf.log` lines include pre-fix crash and previous staged-loading runs, so compare by timestamp.

## Blocker

- 当前无确认 blocker。

## Deferred

- 人格海报最终视觉 polish、更多个性化资源和用户展示偏好设置后置。
- 用户性别 / 展示偏好设置未接入；WI-8 默认使用 `female` 海报资源。
- 全局动效系统和其余模块的 hover polish 仍后置；画像关键词、DNA 标签和“看得多 vs 真喜欢”小卡片已完成本阶段轻微上浮反馈。
- 自动已看开关 UI 和持久设置后置；WI-4 只实现算法和写入链路。
- 库外集合项仍缺少 AI 类型 / 情绪 / 场景快照字段；外部项需要标签时只能使用现有字段。
- 后续真实使用中继续观察 AI 文案质量、画像稳定性、统计阈值和自动完成阈值。

## Known Limitations

- `UserMovieStateChangeHistories` 建表前的旧状态变化无法追溯；旧 true 状态计入“全部”，不会被回填成本月新增。
- 状态历史依赖未来状态变更统一经过 `MovieManagementService`、`UserCollectionService` 或自动已看写入链路；未来新增直接写字段路径时必须同步写状态历史。
- 语言分布当前沿用现有 `Language` 字段，尚无独立 TMDB `original_language` 字段。
- AI 画像输入不包含 RF-2 自定义推荐偏好，这是产品规则，不是缺陷。
- AI 画像数据不足阈值为：有效信号影片少于 8、状态 bucket 少于 2，或无可用标签时不调用 AI。
- AI JSON 解析失败或 AI 调用失败时，有旧缓存则保留旧画像；无缓存时显示错误状态。
- 画像缓存使用 `WatchInsightCacheEntries` 的 `kind=profile`、`scopeKey=global`，不随统计时间范围拆分。
- 统计缓存已按时间范围和日历月份拆分；旧的 `statistics/global` 缓存行可能仍留在表中，但当前 range-aware service 不再使用。
- 手动刷新画像在同一 fingerprint + 同一 prompt/schema version 下不会重复请求 AI；如果未来需要“强制重新生成”，应作为高级功能单独设计。
- 新 prompt 要求六个画像关键词按 1/2/3 分各两个返回；旧缓存没有关键词分数时，UI 会按稳定顺序本地补齐分数，直到画像因 prompt version 变化重新生成。
- 画像生成已拆为 5 个卡片级并发请求；DeepSeek 官方并发限制为动态策略，当前服务层使用 max concurrency 5，后续如遇 429 需要降低并发或增加重试。
- DeepSeek endpoint 下画像默认使用 `deepseek-v4-pro` + thinking high；如果设置页将画像模型改为 `deepseek-v4-flash`，运行时会自动关闭 thinking 并继续请求。推荐使用 `deepseek-v4-flash`；非 DeepSeek endpoint 不强制覆盖模型。
- 叙事标签只存在于画像结果中，不写入 `Movie`，也不新增 DB 字段。
- 画像人格类型限定为最终版 23 个；非法类型会回退为“类型探索家”，必要时追加一次 AI 请求生成匹配描述。
- Persona poster resources are limited to 23 official persona folders / 46 gendered poster PNGs; `eclectic_omnivore` is a redundant legacy folder and root placeholders / `Frames` are not formal personas.
- 旧画像缓存可能仍显示已废弃人格（如“多元杂食者”或“童心奇想家”）；页面应保持可显示并提示手动刷新画像，不会自动改写缓存或调用 AI。
- 口味象限 X/Y 来自 AI 输出，服务层只做 -100~100 clamp；新画像缺失或非数字 X/Y 会被视为生成错误。
- 旧 WatchHistory 不做 backfill；自动完成只在后续播放进度保存时评估。
- 缺少有效 duration 的播放不能自动完成；自动已看需要播放器时长、MediaFile 时长或 Movie runtime。
- 手动标记未看后，旧 WatchHistory 仍保留并继续进入统计 / 画像 / 未来观影历史，但不再参与自动已看聚合。
- reset 到未识别不会删除旧 Movie 历史；它只改变被 reset 播放源的当前归属。
- 未识别 / 识别失败 / 无 TMDB 身份的影片不进入观影统计和画像。
- Batch-2 AI 辅助识别是 apply / merge 语义，不套用 reset 到未识别的干净占位规则。
- WI-R 已接入画像驱动推荐，但画像只作为推荐软偏好，不作为硬过滤，也不会在推荐请求中触发画像 AI 生成。
- 无可用画像缓存、画像数据不足、画像解析失败或画像缓存 stale 时，推荐使用 `profile:none` 并回退现有推荐逻辑。

## Noise

- Watch Insights 缓存 JSON 必须存放在 `WatchInsightCacheEntries`，不能放进 `ApplicationSetting`。
- `WatchHistory.IsCompleted=0` 不能等同于“无观影数据”；统计仍以有效观看时长、开始时间和进度相关字段为准。
- 统计自动刷新只允许刷新本地统计，不能触发 AI 画像。
- 画像自动刷新只在画像加载路径由 profile service 判断，不能由统计 Tab 刷新或播放历史实时变化直接触发。
- 推荐请求可以读取画像缓存摘要，但不能触发画像 AI 生成。
- 播放器、推荐、Library Batch Ops、扫描主流程的问题应回到各自阶段文档，不混入 Watch Insights 收口文档。

## Maintenance Rules

- 只记录有代码、migration、build、日志或人工验收依据的问题。
- 已修复问题归档到 `WATCH_INSIGHTS_STAGE_LOG.md`，不要继续留在 Known Issues。
- Watch Insights 文档必须独立于播放器、Library Batch Ops、Recommendation Feedback。

## 2026-06-14 Profile Refresh Failure Status

Blocker:
- A new profile still cannot be generated while the configured AI provider returns HTTP 402; the application now reports the quota/billing problem explicitly and retains the previous cache.

Deferred:
- Manually verify the cached-fallback status and full tooltip text in the WPF window for 402, 401/403, 429, timeout, and 5xx responses.

Noise:
- Restoring the previous profile after an AI request failure is intentional and does not indicate that the refresh succeeded.

## 2026-06-14 Watch Insights Performance Diagnostics

Blocker:
- None confirmed by build or static review after adding performance diagnostics.

Deferred:
- User-side WPF reproduction is required. Inspect `logs/watch-insights-perf.log` for `serviceMs`, `projectionMs`, `layout-idle idleMs`, `visuals`, `dropShadows`, `slow-frame`, and `frame-summary` entries to decide whether the remaining bottleneck is data loading, ViewModel projection, or WPF layout/rendering.

Noise:
- The diagnostics intentionally log visual-tree counts and slow-frame summaries while Watch Insights is visible; this is temporary investigation instrumentation and should be removed or gated after the performance issue is resolved.

## 2026-06-14 Static Cached Shadow Migration

Blocker:
- None confirmed by build after replacing card/tag static glow effects with cached bitmap shadows.

Deferred:
- Re-run Watch Insights performance reproduction and compare `effects`, `dropShadows`, and slow-frame summaries before deciding whether animation or visual-tree virtualization work is still required.

Noise:
- `ShadowStatic*` resources still exist as unused compatibility tokens; remove them only in a broader style-token cleanup.

## 2026-06-15 Spinner Alignment and First-Entry Loading Pacing

Blocker:
- None confirmed by build after aligning the shared spinner template and removing Watch Insights title/tab glass overlays.

Deferred:
- Reproduce the first Watch Insights entry in the WPF window and inspect new `initial-loading-render-yield`, `initial-loading-hidden`, `slow-frame`, and `frame-summary` entries in `logs/watch-insights-perf.log`.
- Watch Insights still uses a 38x38 loading spinner host while several full-page loading states use 34x34; if the spinner must exactly match those pages, align the host size in a separate spinner-only pass.
- If first-entry stutter remains, split Watch Insights content creation so the loading overlay is hosted by a lightweight shell before the heavy profile/statistics visual tree is materialized.

Noise:
- Older log entries still reflect the previous spinner/layout behavior; restart the running app before comparing the new diagnostics.

## 2026-06-14 Light Theme Cached Shadow Parity

Blocker:
- None confirmed by build after moving cached card shadows to theme-level color, opacity, and depth tokens.

Deferred:
- Manually verify light-theme visibility against dark-theme glow for Watch Insights cards/tags, Home poster base shadows, Movie Discovery ranking badges, scan progress glow, movie detail tags, and watch-history filters.

Noise:
- Poster palette glow remains image-color driven, and transient popup/icon effects remain outside the cached-card migration.

## 2026-06-14 Cached Shadow Hit-Test Boundary Fix

Blocker:
- None confirmed by build after constraining `CachedShadowBorder` hit testing to its actual render bounds.

Deferred:
- Manually verify that buttons around cached-shadow cards are clickable after entering Watch Insights and other migrated pages.

Noise:
- Cached shadows still draw outside the card bounds for visual depth; only the input hit area is bounded.

## 2026-06-14 Restore, Backdrop and DNA Polish

Blocker:
- None confirmed by solution build after the restore, scrollbar, cached backdrop, keyword, and DNA changes.

Deferred:
- Manually verify that returning to either saved Watch Insights tab shows the saved offset without a first-frame flash.
- Manually verify that no-op profile refreshes do not flash, both main scrollbars reveal on hover and can be dragged, and overflowing summary/DNA text still captures the wheel only while it can scroll.
- Confirm the personality-poster backdrop and dedicated keyword/DNA tag palettes remain readable in both themes.
- Confirm the six DNA cards remain subtle at rest and that card/tag hover feedback does not feel stronger than the requested physical lift.

Noise:
- The keyword shadow breathing animates only cached-bitmap draw opacity and a tiny draw scale; it does not regenerate blur assets per frame.
- Narrative-tag prompt changes require a manual profile refresh before old cached DNA content is regenerated.

## 2026-06-14 Persona and Taste Quadrant Follow-up

Blocker:
- None confirmed by solution build after adding `persona.lead` and restructuring the persona/quadrant cards.

Deferred:
- Manually verify that all supported persona names remain readable at 50px with the added visual character spacing.
- Verify the enlarged `334x444` poster and right-side analysis columns at the minimum supported window width.
- Verify the persona and quadrant scrollbars reveal, drag, and consume wheel input only while their text actually overflows.
- Verify AI coordinates near `-100/100` remain fully inside the enlarged axis and the breathing point does not obscure axis labels.

Noise:
- Existing cached profiles do not contain `persona.lead`; normalization supplies a neutral local lead until the user manually refreshes the profile.
- The new lead field is display-only for this change and is intentionally not added to recommendation prompt context.

## 2026-06-14 Watch-vs-Like Triptych Follow-up

Blocker:
- None confirmed by solution build after the three-card triptych change.

Deferred:
- Manually verify that the raised center card and overlapping side cards remain fully readable at the minimum supported window width.
- Verify that visible outer portions of the recessed cards remain easy to target and that moving directly between cards does not produce distracting reset flicker.
- Confirm the `♡` and `☆` rank markers render consistently and the darker light-theme progress track has sufficient contrast.

Noise:
- Z-order changes are immediate by design; scale, translation, opacity, and cached-shadow draw opacity are animated over 190ms.

## 2026-06-14 Statistics Overview and Calendar Redesign

Blocker:
- None confirmed by solution build after the viewing-day snapshot and complete calendar-grid changes.

Deferred:
- Verify the reduced overview-card widths and enlarged inter-card gaps at the minimum supported window width.
- Verify all six tag-cloud positions remain readable for 2-6 character labels and tied occurrence counts.
- Verify five-row and six-row months, adjacent-month disabled cells, two-line tooltips, month navigation, and right-side summary alignment in the running WPF app.

Noise:
- The fixed calendar content width is 560px to implement the requested narrower cells; visual acceptance remains user-owned because the application was not launched.
- Existing statistics caches are intentionally invalidated by the new logic version and will be recomputed on first access.

## 2026-06-14 DNA Card Frozen-Transform Crash Fix

Blocker:
- None confirmed after cloning frozen DNA-card transforms before starting WPF animations.

Deferred:
- Re-enter Watch Insights and verify the page no longer terminates the process while the six DNA cards retain ambient float and hover lift.

Noise:
- The root cause was confirmed from the Windows `.NET Runtime` event at `2026-06-14 15:14:58`; no application launch was required for diagnosis.

## 2026-06-14 DNA Progress Indicator Name-Scope Crash Fix

Blocker:
- None confirmed after moving the DNA progress-indicator breathing Storyboard into the ellipse's own name scope.

Deferred:
- Re-enter Watch Insights and verify that both DNA ambient-card animation and progress-indicator breathing remain stable.

Noise:
- The second root cause was confirmed from the Windows `.NET Runtime` event at `2026-06-14 15:23:24`.

## 2026-06-14 Window-Level Persona Poster Backdrop Fix

Blocker:
- None identified by static inspection after routing Watch Insights through the existing window-level cached poster backdrop.

Deferred:
- Verify that the personality-poster palette covers the full window root, including the shell area outside the page content margins, in both themes.
- Verify that switching away from Watch Insights clears the palette background and that returning restores the correct persona palette.

Noise:
- Watch Insights intentionally shares the Movie Detail backdrop cache and palette extraction pipeline; it does not create a second page-local bitmap effect.

## 2026-06-14 Vivid Persona Backdrop Palette

Blocker:
- None identified by static inspection after introducing the Watch Insights-only vivid palette mode.

Deferred:
- Verify that vivid-mode colors remain readable behind cards and shell text in dark and light themes.
- Compare several personality posters to ensure the stronger saturation does not clip bright reds, blues, or yellows excessively.

Noise:
- Movie Detail, series, season, and episode backgrounds intentionally continue using the unchanged standard palette mode.

## 2026-06-14 Force-Directed Preference Bubble Graph

Blocker:
- None identified by build or static inspection after replacing fixed bubble coordinates with the bounded physics canvas.

Deferred:
- Verify that all three category colors remain distinguishable in both themes and that long labels stay readable inside the smallest bubbles.
- Verify collision separation, boundary response, perpetual drift, and hover expansion at minimum and maximum supported window widths.
- Confirm that scrolling the bubble canvas out of view pauses its motion cost and that returning to it resumes smoothly.

Noise:
- The dynamic bubbles intentionally have no shadow/glow while their radius changes; no radius-specific shadow pre-cache was added.
- Pairwise collision work is capped by the 18-bubble display limit.

## 2026-06-14 Viewing Rhythm Chart Redesign

Blocker:
- None identified by build or static inspection after introducing the custom spline, donut, and semantic-icon controls.

Deferred:
- Verify the spline-area gradient, peak glow, dashed grid lines, and time-bucket captions in both themes.
- Verify the donut proportions and office-computer/popcorn icon readability at minimum supported width.
- Confirm actual watched-time duration percentages against a known history sample containing multiple runtime buckets.

Noise:
- Duration rows exclude histories whose movie runtime cannot be resolved; percentages are normalized across the remaining eligible watched seconds.
- The statistics logic-version change intentionally recomputes cached statistics on first load.

## 2026-06-14 Watch Insights Transition and Motion Polish

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify that returning to either persisted tab no longer produces a background-only frame and that the saved offset appears without a visible scroll animation.
- Verify that manual refresh with unchanged profile or statistics data leaves all existing cards continuously visible.
- Verify that the persona palette covers the full window root from first entry and updates to the resolved local persona poster after profile projection.
- Verify that hovering either tab's page ScrollViewer reveals its scrollbar and that the thumb remains visible and draggable while captured.
- Verify keyword breathing, DNA progress widths and endpoint indicators, and the stronger asynchronous DNA-card drift in both themes.
- Verify DNA cards no longer add a separate hover lift while their child tags retain hover feedback.

Noise:
- The normal output build was blocked by the already-running application executable; isolated compilation succeeded without terminating that user process.
- The running application must be restarted to load the updated assemblies and XAML.

## 2026-06-14 Persona and Taste Quadrant Refinement

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify that short and long persona labels remain on one line and that the body column expands or contracts without overlap.
- Verify that the lead stays below the persona label and that overflowing persona body text exposes the final half-line cue and draggable scrollbar.
- Verify the stronger persona-poster glow in both themes without clipping or overpowering the adjacent card.
- Verify that both horizontal-axis captions sit outside the axis endpoints at supported window widths.
- Verify that the quadrant point halo now pulses smoothly without the previous cached-shadow redraw stutter.

Noise:
- The running application must be restarted before the updated XAML and assemblies can be observed.

## 2026-06-14 Watch-vs-Like Folded Triptych Refinement

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify that the left and right cards visually fold backward from their inner edges instead of appearing as simple diagonal distortion.
- Verify that hovering each card flattens and brightens it while both inactive cards fold, darken, and remain behind it.
- Verify smooth handoff when moving the pointer directly between overlapping cards.
- Verify that numeric ranks remain centered inside the outline heart and star at 100%, 125%, and 150% Windows scaling.
- Verify that shared Watch Insights progress tracks remain visible without appearing too heavy in both themes.
- Verify the shortened triptych bars and added space below the conclusion panel at the minimum supported window width.

Noise:
- Historical note: this stage used `SkewTransform` as a perspective approximation; the current implementation no longer uses it.
- The running application must be restarted before it can load these changes.

## 2026-06-14 Statistics Overview and Calendar Polish

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify frequent-tag cloud placement, label truncation, hover lift, breathing strength, and count tooltips in both themes.
- Verify the filled favorite and wanted icons remain clear at 100%, 125%, and 150% Windows scaling.
- Compare month and all-range overview cards to confirm count, duration, and day values use the same size and vertical position.
- Verify that the calendar legend remains on one line at the minimum supported window width without colliding with the month controls.
- Verify current-month and adjacent-month date colors in both themes and all heat levels.
- Verify the wider calendar grid, larger right-side values, reduced captions, expanded icon spacing, and added bottom padding visually.

Noise:
- Frequent-tag occurrence counts are intentionally available through tooltips rather than rendered beside the label so the chips match the profile core-keyword appearance.
- The running application must be restarted before it can load these changes.

## 2026-06-14 Preference Bubble Physics and Session Persistence

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify that switching tabs, navigating away and back, and resizing the window restore the existing bubble positions instead of replaying the initial grid motion.
- Verify that month and all-range bubble layouts maintain separate process-lifetime states.
- Observe the simulation for several minutes to confirm bubbles remain separated and do not gradually collect at the center or become trapped at an edge.
- Verify radial pointer repulsion and directional fluid drag at slow, medium, and fast mouse speeds.
- Verify ripple spacing, size, fade duration, and cleanup during sustained mouse movement.
- Verify that bubble hover growth still pushes neighboring bubbles away without overlap.

Noise:
- Bubble state is intentionally held only in process memory and resets on application restart.
- Ripple ellipses use local WPF vector animation and are removed after completion; they are not included in the persisted particle state.

## 2026-06-15 Viewing Rhythm Chart and Layout Refinement

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify that every zero-minute bucket sits exactly on the bottom baseline and that both spline ends return naturally to the baseline.
- Verify that the peak marker follows the mathematical highest point between data buckets rather than only a discrete bucket center.
- Verify that the peak halo pulse is visible but subtle in both themes.
- Verify that 2%/98%, 0%/100%, and 50%/50% donut splits use flat joins without one segment covering another.
- Verify each donut arc opens the correct three-line tooltip and that the central hole does not trigger an arc tooltip.
- Verify the full-width spline card and equal-height lower cards remain balanced at the minimum supported window width.
- Verify duration percentages are vertically centered with their progress bars and that the added bottom spacing is visually appropriate.

Noise:
- The running application must be restarted before the updated chart controls and XAML can be observed.

## 2026-06-15 Taste Combination Graph Spacing Refinement

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify the frequency legend remains fully below the graph and does not overlap the fourth node row.
- Verify removing the nested graph card leaves sufficient visual separation from the outer module background in both themes.
- Verify the added bottom padding creates balanced spacing below the high-frequency ranking card.

Noise:
- The running application must be restarted before the updated XAML can be observed.

## 2026-06-15 Initial Loading, Keyword Breathing, and DNA Indicator Refinement

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify the first entry into either tab shows only the centered spinner and status text, with no module cards visible underneath.
- Verify returning to an already loaded tab still restores its content and saved scroll position without a blank frame.
- Verify the 18-pixel scrollbar hit area is easier to acquire while the visible thumb remains 6 pixels wide.
- Verify all six keyword chips visibly breathe through colored cached-shadow opacity/radius changes and subtle card-opacity changes.
- Verify the stronger keyword breathing remains restrained in both themes and does not clip at the canvas edges.
- Verify the larger accent-colored DNA endpoint indicator remains attached to the current progress endpoint and is distinct from the blue fill.
- Verify the additional taste-summary bottom padding remains balanced at the minimum supported window width.

Noise:
- Keyword breathing redraws cached bitmap shadows but does not regenerate shadow resources per animation frame.
- The running application must be restarted before the updated XAML can be observed.

## 2026-06-15 Persona Typography and Quadrant Detail Refinement

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify the persona label remains mostly deep ink while visibly inheriting a small amount of the current poster palette.
- Verify the vertical gradient, one-pixel light outline, and palette projection remain readable without looking embossed or over-sharpened.
- Verify long persona labels remain on one line and centered at supported window sizes.
- Verify the centered lead uses visibly less character spacing than the persona label.
- Verify the centered 70%-width body preserves indentation, half-line overflow, and draggable scrolling.
- Verify the stronger, wider persona-poster glow does not clip or overpower the analysis card.
- Verify the quadrant title reflects the active X/Y directions and the point tooltip shows the matching two scores.
- Verify horizontal captions align vertically with the X axis and all four captions have the requested inward/outward offsets.
- Verify the wider, right-shifted quadrant explanation remains balanced against the coordinate canvas.

Noise:
- Persona title projection uses local vector layers driven by the cached poster palette; it does not use a runtime `DropShadowEffect`.
- The running application must be restarted before the updated XAML and control can be observed.

## 2026-06-15 Watch-vs-Like True 3D Triptych Refinement

Blocker:
- None identified by isolated-output solution build or static inspection.

Deferred:
- Verify the watched ranks use an unfilled accent-colored circle matching the heart and star outline weight.
- Verify the side cards visibly recede through perspective and do not appear as simple skewed parallelograms.
- Verify all three cards retain visible separation in the default state and during each hover state.
- Verify hovering a tilted side card reliably activates it and keeps its content readable while it moves forward and flattens.
- Verify inactive cards remain behind the active card without covering its text or controls.
- Verify the 320-pixel triptych area and 30-pixel conclusion gap eliminate all overlap at supported window sizes and Windows scaling values.
- Verify the increased outer-card height and bottom padding remain visually balanced.

Noise:
- Historical note: this stage used `Viewport2DVisual3D`; that rendering path was removed by later revisions.
- The running application must be restarted before the updated XAML and animation logic can be observed.

## 2026-06-15 Watch Insights Chrome Scope and Loading Preflight

Blocker:
- None identified by solution build or static inspection.

Deferred:
- Verify the collapsed titlebar subtitle is the only shell title text receiving the edge treatment, and the shell titlebar itself has no visible glow/shadow overlay.
- Verify tab selected text and tab hover text use the same subtle edge treatment, while unhovered inactive tabs remain plain.
- Verify the tab right status text remains readable without a directional projection in both themes.
- Verify the titlebar-tab separator and tab-bottom separator read as direct line colors only: whiter in light theme and blacker in dark theme.
- Verify first entry into Watch Insights applies the current cached persona palette during loading rather than showing a previous page/poster color.
- If first-entry spinner still visibly hitches, split the heavy Watch Insights content into deferred templates so the lightweight loading overlay can render and animate before profile/statistics visuals are materialized.

Noise:
- The new backdrop preflight reads cached Watch Profile recommendation context only; if no valid cache exists, loading still starts from the fixed fallback persona palette until the profile load completes.

## 2026-06-16 Staged Visual Tree and Chrome Tone

Blocker:
- None identified by solution build.

Deferred:
- Verify the `观影洞察` page title has no outline in expanded and collapsed sidebar states.
- Verify the subtitle and tab right status text remain close to their original muted color, with only a small white blend in light theme and black blend in dark theme.
- Verify selected and hovered tab text use the lightly adjusted accent foreground and the slightly thicker edge.
- Verify the reduced light/dark separator contrast remains visible against every current persona backdrop.
- Verify the spinner rotates continuously while the four profile stages are materialized.
- Verify the statistics tab first activation rotates continuously while its five stages are materialized.
- Compare new `visual-tree-stage-ready`, `visual-tree-ready`, and slow-frame log entries against the previous monolithic construction run.
- Verify bubble interaction and watch-vs-like hover animation after their deferred templates are materialized.

Noise:
- Deferred templates intentionally appear only after their stage flag is raised; this is expected while the loading overlay remains visible.

## 2026-06-16 Persona Lead And Painted Label

Blocker:
- None identified by solution build or static inspection.

Deferred:
- Verify persona leads up to 40 characters preserve the intended two-clause structure and do not overflow the two-line layout.
- Verify the larger opening bracket sits slightly above the first visible character and the larger closing bracket sits slightly below the final visible character.
- Verify bracket placement remains correct when the sidebar switches between expanded and collapsed layouts.
- Verify the painted background tracks short and long persona labels with visible side padding and no rectangular or capsule silhouette.
- Verify the most vivid fixed poster palette color remains restrained in light theme and sufficiently deep in dark theme.
- Verify the persona label text remains clearer than the paint layer and its existing projection is not visually muddied.

Noise:
- The paint texture is vector-rendered from five cached-palette strokes; no poster decode or new bitmap resource is involved.

## 2026-06-16 Quadrant, Calendar, And Preference Wave

Blocker:
- None identified by solution build or static inspection.

Deferred:
- Verify the quadrant body expands only toward the center and its left edge remains aligned with the previous layout.
- Verify the raised coordinate view does not collide with the card subtitle or clip its top axis caption.
- Verify refreshed quadrant descriptions are visibly longer while remaining readable in the internal scroll area.
- Verify calendar navigation, legend, and metric cards move in the requested directions without reducing calendar-grid space.
- Verify the periodic wave alternates left and right and begins roughly every eight seconds while the graph is visible.
- Verify its full-height visual reads as one travelling wave rather than several distant ripples.
- Verify the wave pushes bubbles more strongly than pointer movement but does not pin them against the boundary.
- Verify mouse-generated ripples are moderately rounder without losing directionality.

Noise:
- Periodic wave timing pauses while the preference graph is outside the active statistics viewport; this avoids hidden animation and physics work.

## 2026-06-16 Flat Watch-vs-Like Focus Cards

Blocker:
- None identified by solution build or static inspection.

Deferred:
- Verify the middle `经常喜爱` card is primary before any pointer interaction.
- Verify the default center card overlaps both side cards by a restrained amount without clipping their important content.
- Verify hovering the left card creates the exact layer order left, center, right from front to back.
- Verify hovering the right card creates the exact layer order right, center, left from front to back.
- Verify primary, intermediate, and far cards have clearly distinct but restrained opacity and shadow levels.
- Verify transitions remain stable when moving directly across overlapping card boundaries.
- Verify leaving the complete card region restores the middle-primary state.
- Verify no rotation, skew, trapezoid, perspective distortion, or folded dark overlay remains in either theme.

Noise:
- WPF ZIndex is discrete, so the new layer order is applied at animation start while scale, opacity, shadow, and translation animate over 220 milliseconds.

## 2026-06-16 Taste Combination Graph And Top5

Blocker:
- None identified by solution build or static inspection.

Deferred:
- Verify all graph nodes are circular, shadow-free, and show only the tag text.
- Verify node tooltips use the exact `标签：次数` format.
- Verify the larger vertical spacing keeps all four possible nodes per column inside the graph canvas.
- Verify inactive line opacity and thickness visibly follow occurrence count without making low-frequency links disappear.
- Verify hovered and selected nodes raise only their connected links to 90% opacity.
- Verify clicking an already selected node clears selection and mouse leave restores the prior selected-node focus.
- Verify mist blue, rose pink, and mint green remain restrained and readable in both themes.
- Verify the right panel displays no more than five combinations and each row uses `标签 x 标签 x 标签` with rounded rectangles.
- Verify the occurrence label reads `x次` without the previous `出现` prefix or spacing.

Noise:
- When all visible links have the same count, normalization intentionally assigns all of them the maximum normalized value.

## 2026-06-16 Watch Insights Entry Crash Fix

Blocker:
- None identified after removing staged `ContentTemplate` Style setters, switching the profile stage 3/4 forward template references to `DynamicResource`, and rebuilding the solution.

Deferred:
- Verify clicking into Watch Insights no longer exits the application.
- Verify profile and statistics staged modules still appear after their visual-stage flags turn true.
- Verify no new `.NET Runtime` event 1026 is produced during Watch Insights entry.
- If a new crash still appears, inspect the latest Windows Application event first; the original `MS.Internal.NamedObject` and `StaticResourceHolder` failures should no longer recur.

Noise:
- Existing Event Viewer and performance-log records from before this fix remain on disk until aged out or cleared.

## 2026-06-16 Watch Insights Loading Prelayout Smoothness

Blocker:
- None identified by static inspection.

Deferred:
- Verify first-entry spinner animation remains visually continuous while profile content is staged behind the loading overlay.
- Verify the nearly transparent prelayout body is not noticeable behind the loading spinner in both light and dark themes.
- Verify fresh `watch-insights-perf.log` entries no longer show the largest first-entry slow frames immediately after `initial-loading-hidden`.
- If one stage still creates a visible hitch, split that stage's template content deeper rather than relying only on longer stage delays.

Noise:
- Loading can remain visible slightly longer because stage pacing now intentionally gives WPF extra render turns between heavy visual-tree additions.

## 2026-06-16 Watch Duration Overcount Diagnosis Follow-up

Blocker:

- None identified by build.

Deferred:

- Existing inflated watch-history rows are not automatically corrected; approve a separate database cleanup if current historical Watch Insights totals should be rewritten.
- Manually verify future player sessions no longer count pause, buffering, or idle window time as watched duration.

Noise:

- The fix prevents future overcount at the player persistence layer; Watch Insights still reads stored history values as its source of truth.

## 2026-06-16 Watch Insights Profile Stage2 Persona Split

Blocker:
- None identified by solution build.

Deferred:
- Verify Profile stage 2 no longer carries the persona poster/card work and the previous 1265-1478 ms stage-2 spike is gone.
- Verify the persona module appears unchanged after it is materialized as stage 3.
- Verify the quadrant module, watch-vs-like shell, and three watch-vs-like cards still appear in the correct order after being shifted to stages 4-8.
- If the persona-only stage remains visibly heavy, split poster/shadow image creation from persona text/card content in a follow-up.

Noise:
- Profile initial loading now uses eight visual stages; this is intentional pacing to trade a little wall-clock time for smoother spinner motion.
