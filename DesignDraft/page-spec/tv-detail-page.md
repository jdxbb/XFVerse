# 电视剧详情链路 UI 实施说明

## 1. 模块名称

电视剧剧详情页与季详情页

建议文件名：

- `tv-detail-page.md`

建议实现对象：

- `SeriesOverviewPage`
- `TvSeasonDetailPage`

关联规格：

- `media-library-page.md`
- `media-library-special-items.md`
- `episode-detail-page.md`
- `correction-flow.md`
- `global-dialogs.md`

---

## 2. 模块用途与范围

本规格定义电视剧从 Series 总览进入 Season 详情的页面结构，并覆盖已识别、未识别、metadata-only 与无播放源状态。

核心边界：

- 影片发现的 TV 搜索卡片和 TV 榜单卡片进入 Series 剧详情页
- 普通模式中的 Series 先进入剧详情页，再进入季详情页
- 已识别季和未识别季共用同一套基础详情布局
- Grouped placeholder 已有详情承载，进入未识别剧详情页后可继续进入未识别季详情页
- 修正使用统一弹窗，不在详情页中设置常驻修正 Tab / 面板
- 详情页不新增字幕入口，不新增删除记录或移出媒体库入口
- TV 用户状态按当前 Season 级语义表达，不将 TV 接入电影专属的 Watch Insights / AI 推荐语义

---

## 3. 入口与层级

```text
MovieDiscovery TV Search / Ranking Card
└─ SeriesOverviewPage（可为 metadata-only）
   └─ Season Card
      └─ TvSeasonDetailPage

Library Series Card
└─ SeriesOverviewPage
   └─ Season Card
      └─ TvSeasonDetailPage
         └─ Episode Row
            └─ EpisodeDetailPage

Favorites Season Card
└─ TvSeasonDetailPage

WatchHistory Episode Item / Home Continue Watching Episode
└─ TvSeasonDetailPage（定位 Episode）

Grouped Placeholder
└─ 未识别剧详情
   └─ 未识别季详情
      └─ EpisodeDetailPage（存在可定位 Episode 时）
```

说明：

- 上述 grouped placeholder 链路以用户已验证的现有能力为事实基础
- 收藏夹 Season 当前直接进入季详情；观影历史 Episode 与首页剧集继续观看当前进入季详情并定位 Episode
- Discovery 中的 metadata-only TV 仍可进入剧详情；若 metadata 暂不可用，默认在发现页结果区域显示页面内提示，不使用打断式信息弹窗
- 如果某个对象无法解析到目标详情而显示提示，该提示只作为 fallback / 异常状态，不改变正常详情入口定义

---

## 3.1 统一返回入口与当前审计事实

Phase 6.0i 审计事实：

| 页面 | 当前成品返回入口 | 当前行为 |
|---|---|---|
| `SeriesOverviewPage` | 无可见返回按钮 | 只能通过其它导航重新离开详情 |
| `TvSeasonDetailPage` | 操作区文字按钮 `返回剧页` | 调用当前 `_seriesId` 返回剧详情，不处理真实来源页 |

Phase 7 目标：

- `SeriesOverviewPage` 与 `TvSeasonDetailPage` 均显示统一详情返回按钮。
- 按钮为内容 Hero 左上角的图标按钮，视觉、尺寸、hover、pressed、focus、disabled 与 Tooltip 均引用统一基线。
- 当前季页的 `返回剧页` 文字按钮不作为最终主入口；由统一图标按钮取代。需要强调层级时，可使用辅助面包屑或 Tooltip，不额外制造第二个主返回按钮。
- metadata-only、未识别剧、未识别季和 grouped placeholder 的详情承载均显示相同返回按钮。

返回行为优先级：

1. 有可靠上一界面时，返回实际来源并恢复可保存的来源状态，例如媒体库、影片发现、收藏夹或观影历史。
2. `TvSeasonDetailPage` 无可靠来源时，fallback 返回当前 `SeriesOverviewPage`。
3. `SeriesOverviewPage` 无可靠来源时，如可判断来自影片发现则返回影片发现，否则 fallback 返回媒体库。
4. 从观影历史进入 Season 并定位 Episode 的链路，有可靠来源时返回观影历史并保留日期筛选与定位状态。
5. 从收藏夹进入 Season 的链路，有可靠来源时返回收藏夹并保留当前 Tab。

当前实现差距：

- 当前 `NavigationStateService` 没有来源路由、页面状态快照或通用返回栈；季页仅具备固定返回剧详情能力。
- 以上“返回真实来源并恢复状态”为 Phase 7 导航能力目标，本阶段不修改实现。

---

## 4. 剧详情页结构

```text
SeriesOverviewPage
├─ UnifiedDetailBackButton
├─ SeriesHero
│  ├─ Poster
│  ├─ SeriesTitle / OriginalTitle
│  ├─ FirstAirYear / Year
│  ├─ Genre / Rating
│  └─ SourceSummary
├─ Overview
├─ SeasonUserStateSummary
├─ ExistingAvailabilityAction（如当前已有加入或恢复）
└─ SeasonList
   └─ SeasonCard
      ├─ SeasonNumber / SeasonTitle
      ├─ EpisodeCount
      ├─ SourceStatus
      ├─ UserStateSummary
      └─ DetailEntry
```

展示规则：

- 海报、剧名、原名、年份、简介、类型与评分沿用详情页信息层级
- 来源摘要说明可播放季数、无播放源或仅 metadata 状态
- Season 卡片始终提供进入季详情的入口
- 若当前存在加入媒体库或恢复入口，可记录并保留其位置；不以本规格新增生命周期功能

---

## 5. 剧详情页状态

| 状态 | 展示规则 | 动作规则 |
|---|---|---|
| Loading | 保持 Hero 和 SeasonList 容器稳定，显示轻量加载状态 | 禁用依赖数据的动作 |
| Error | 显示中文错误提示和当前已有的重试入口 | 不展示伪造季数据 |
| metadata-only | 展示剧资料，明确 `已有资料，暂无播放源` | 可浏览季信息，不提供可播放假象 |
| 无播放源 | 显示来源状态提示 | 播放相关动作 disabled |
| 无 Season | 显示 `暂无可展示的季` 空状态 | 不新增扫描或识别功能 |
| 未识别剧 | 展示分组名或候选剧名、识别状态和可用来源摘要 | 可进入未识别季；修正通过弹窗触发 |
| Disabled | 说明不可操作原因 | 不仅依赖颜色表达 |

---

## 6. 季详情页基础结构

```text
TvSeasonDetailPage
├─ UnifiedDetailBackButton
├─ SeasonHero
│  ├─ Poster
│  ├─ SeriesTitle
│  ├─ SeasonNumber / SeasonTitle
│  ├─ Year / FirstAirDate
│  ├─ RecognitionStatus
│  └─ SourceSummary
├─ Overview
├─ SeasonUserStateActions
├─ ExistingAvailabilityAction（如当前已有加入或恢复）
├─ CorrectionDialogEntry
└─ EpisodeList
   └─ EpisodeRow
      ├─ EpisodeNumber / EpisodeTitle
      ├─ AirDate / Overview
      ├─ WatchedState
      ├─ SourceState
      ├─ PlayAction
      └─ DetailEntry
```

---

## 7. 已识别季与未识别季

已识别季和未识别季使用相同布局骨架，差异只落在信息完整度和提示区域。

| 区域 | 已识别季 | 未识别季 / Grouped Placeholder 承载 |
|---|---|---|
| 标题 | 正式剧名、季名和季号 | 候选剧名或分组名、推测季号 |
| 识别状态 | 可隐藏或显示已识别状态 | 显示 `未识别季` / `待修正` |
| Metadata | 完整简介、海报、首播信息（有数据时） | 仅展示已有字段，缺失区域保持轻量空态 |
| 来源摘要 | 活动来源与无播放源状态 | 文件数、集号覆盖、活动来源与无播放源状态 |
| 修正提示 | 可提供统一修正入口 | 提示可通过统一修正弹窗完善归属 |
| Episode 信息 | 正式集名与播出信息（有数据时） | 集号和来源优先，标题 / 简介缺失时允许空态 |

要求：

- 未识别季同样可进入详情，并可继续打开可定位的单集详情
- Grouped placeholder 不写成无详情项目，也不写成 Phase 7 才需新增入口
- 人工聚合仍属于媒体库批量模式，不作为本页普通主动作

---

## 8. Episode 列表

每一行可包含：

- 集号
- 集名
- 播出日期
- 简介摘要
- 已看 / 未看状态
- 有播放源 / 无播放源状态
- 播放按钮
- 详情入口

交互：

- 有可播放默认源时允许播放
- 无播放源或无默认源时播放 disabled，并说明原因
- 点击详情入口进入 `episode-detail-page.md` 定义的单集详情
- 本列表不展示字幕状态，也不新增在线字幕入口

---

## 9. 用户状态与生命周期边界

- Season 可展示当前已有的已看、未看、喜爱、想看、不想看等状态动作或摘要
- Series 总览只汇总 Season 级状态，不扩展电影洞察或推荐业务
- 删除记录和移出媒体库仍以媒体库页为主，不在剧或季详情中新增入口
- 已有的加入或恢复动作只按当前事实保留，并使用自然语言说明当前承载状态

---

## 10. 修正与状态文案

- 剧或季的修正操作从详情内入口打开 `correction-flow.md` 的统一弹窗
- 最终设计不保留常驻修正 Tab / 面板
- 页面状态文案统一使用中文
- 不使用“后续阶段接入”等与当前真实能力冲突的最终界面文案

---

## 11. 验收标准

- Series 从媒体库进入剧详情，并可从季列表进入季详情
- 剧详情与季详情均在 Hero 左上角显示统一返回图标按钮
- 季详情不再以操作区文字按钮 `返回剧页` 作为最终主返回入口
- 有来源导航状态时返回实际来源；缺少来源时季详情 fallback 至剧详情，剧详情 fallback 至媒体库或已知影片发现来源
- 剧详情覆盖 metadata-only、无播放源、无 Season、loading、error 与 disabled 状态
- 已识别季和未识别季使用同一基础布局，并表达必要差异
- Grouped placeholder 按已确认事实进入未识别剧详情，并可继续进入未识别季详情
- Episode 行提供详情入口，播放不可用时说明原因
- 剧与季详情不新增字幕、在线字幕、删除记录或移出媒体库入口
- 修正入口打开统一弹窗式体验，文案全部为中文
