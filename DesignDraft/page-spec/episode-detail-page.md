# 单集详情页 UI 实施说明

## 1. 模块名称

电视剧单集详情页

建议文件名：

- `episode-detail-page.md`

建议实现对象：

- `EpisodeDetailPage`

关联规格：

- `tv-detail-page.md`
- `correction-flow.md`
- `global-dialogs.md`

---

## 2. 页面用途与边界

单集详情页展示某一 Episode 的资料、播放状态和播放源，并提供当前已有的状态动作和统一修正入口。

边界：

- 单集详情来自季详情的 Episode 列表入口
- 修正使用弹窗式统一流程
- 字幕选择与在线字幕搜索属于播放器，不是单集详情组件
- 删除记录和移出媒体库仍以媒体库为主，本页不新增这类入口

---

## 3. 页面结构

```text
EpisodeDetailPage
├─ BackButton
├─ EpisodeHero
│  ├─ Still / Poster
│  ├─ SeriesTitle
│  ├─ SeasonNumber / EpisodeNumber
│  ├─ EpisodeTitle
│  └─ AirDate
├─ Overview
├─ EpisodeActionBar
│  ├─ PlayDefaultSource
│  ├─ WatchedState
│  └─ CorrectionDialogEntry
└─ PlaySourcePanel
   ├─ SourceSummary
   └─ PlaySourceRow
      ├─ FileName / SafePathSummary / SourceType
      ├─ Resolution / Duration / FileSize
      ├─ DefaultSource / ProbeStatus
      └─ Play / Probe / SetDefault / SplitSource / Correct
```

---

## 4. 基础字段

| 区域 | 字段 | 缺失处理 |
|---|---|---|
| 标题 | 剧名、季号、集号、集名 | 集名缺失时以集号为主标题 |
| 时间 | 播出日期 | 缺失时隐藏字段行 |
| 图片 | still 或海报 | 缺失时使用统一占位资源 |
| 简介 | Episode 简介 | 缺失时显示轻量空态 |
| 状态 | 已看 / 未看 | 按当前用户状态展示 |

---

## 5. 播放源与默认源

播放源列表采用与电影详情一致的基础结构。

字段可包括：

- 文件名
- 安全路径摘要
- 来源类型：本地 / 网盘等
- 分辨率
- 时长
- 文件大小
- 默认源标记
- 探测状态

动作分级：

| 动作 | 设计行为 |
|---|---|
| 播放默认源 | 有默认可播放源时执行播放 |
| 播放此源 | 仅对可播放行启用 |
| 立即探测 | 普通操作，不弹确认 |
| 设为默认 | 普通操作，不弹确认 |
| 从当前对象拆分来源 | 引用 `global-dialogs.md` 的来源拆分警示确认 |
| 修正信息 | 打开 `correction-flow.md` 统一修正弹窗 |

安全语义：

- 拆分来源改变的是软件中的来源归属
- 文案不得表示会删除真实本地文件或 WebDAV 文件
- 删除真实文件不是本规格设计能力

---

## 6. 状态矩阵

| 状态 | 页面展示 | 动作 |
|---|---|---|
| Loading | 保持 Hero 和来源容器稳定，显示加载状态 | 禁用依赖数据的动作 |
| Error | 中文错误提示与当前已有的重试入口 | 不显示虚假来源 |
| 无播放源 | 展示 Episode 资料与 `暂无播放源` 状态 | 播放 disabled |
| 有来源但无默认源 | 展示来源列表并提示选择默认源 | 主播放 disabled，可设置默认 |
| metadata-only | 展示已有 Episode metadata，提示暂不可播放 | 播放 disabled |
| 未识别单集 | 展示文件 / 来源摘要与待修正状态 | 可打开修正弹窗 |
| 探测中 / 探测失败 | 来源行显示对应状态 | 不阻塞浏览详情 |
| Correction running | 修正弹窗展示处理中状态 | 页面避免重复提交 |
| Disabled | 提示不可操作原因 | 不仅使用颜色表达 |

---

## 7. 修正入口

- 单集修正入口打开统一修正弹窗，不使用常驻详情页 Tab / 面板
- 弹窗可支持 Episode 到 Movie、TV Episode 或 Unknown Season 的当前真实修正范围
- 拆分来源与修正是不同动作：拆分使用警示确认，修正使用修正弹窗内确认
- 成功后的刷新或导航只定义体验目标，不在本阶段实现功能

---

## 8. 不纳入详情页的组件

- 本地字幕状态
- 在线字幕搜索入口
- 字幕切换入口
- 新增删除记录入口
- 新增移出媒体库入口

字幕交互统一进入播放器链路；生命周期危险动作仍以媒体库页规格为主。

---

## 9. 验收标准

- 季详情可进入单集详情页
- 页面展示 Episode 标题信息、still / 海报、简介、状态和播放源
- 无播放源、无默认源、metadata-only、loading、error、disabled 均有明确设计状态
- 来源探测和设置默认源为普通操作，来源拆分使用警示确认
- 单集修正通过统一弹窗打开
- 页面不新增字幕、在线字幕、删除记录或移出媒体库入口
- 所有面向用户的状态和修正文案均使用中文
