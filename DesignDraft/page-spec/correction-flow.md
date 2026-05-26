# 统一识别修正弹窗 UI 实施说明

## 1. 模块名称

电影与电视剧详情统一识别修正弹窗

建议文件名：

- `correction-flow.md`

建议实现对象：

- Movie 详情修正入口
- Season 详情修正入口
- Episode 详情修正入口

关联规格：

- `movie-detail-page.md`
- `tv-detail-page.md`
- `episode-detail-page.md`
- `media-library-special-items.md`
- `global-dialogs.md`

---

## 2. 目标设计与边界

修正流程采用旧设计的弹窗式体验，同时覆盖当前详情链路已经具备的跨类型修正能力。

最终设计要求：

- 从详情页内的修正入口打开 Dialog
- 不采用详情页常驻 Tab 或常驻修正面板作为 Phase 7 最终结构
- 当前若存在页内面板，只视为现有实现事实，不作为目标布局
- 修正弹窗不承担删除记录或移出媒体库功能
- 人工聚合为未识别季属于媒体库批量模式，不并入普通详情修正主入口

---

## 3. 修正范围矩阵

| 当前对象 | 弹窗允许表达的目标范围 | 说明 |
|---|---|---|
| Movie / 未识别电影 | Movie、TV Episode、Unknown Season | 覆盖电影识别与误归类到 TV 的修正 |
| Episode / 未识别单集 | Movie、TV Episode、Unknown Season | 覆盖 Episode 误归类与未识别归组 |
| 已识别 Season | 已识别 Season 或当前能力支持的 Season 映射目标 | 覆盖季级修正和集号映射 |
| 未识别季 / Grouped Placeholder 详情承载 | 正式 Season 或当前能力支持的未识别季修正目标 | 从详情入口完善归属，不否定既有详情链路 |

说明：

- 实际候选和提交能力应复用现有业务实现，不因 UI 规格凭空新增数据能力
- Grouped placeholder 已有详情承载；本弹窗仅定义从该详情链路触发修正的呈现方式

---

## 4. 弹窗结构

```text
CorrectionDialog
├─ DialogHeader
│  ├─ Title: 识别修正
│  └─ CloseButton
├─ CurrentObjectSummary
│  ├─ CurrentType / Status
│  ├─ TitleOrFileName
│  └─ SafeSourceSummary
├─ TargetTypeSelector
├─ SearchPanel
│  ├─ KeywordInput
│  ├─ YearOrSeasonInput（按目标类型出现）
│  ├─ SearchButton
│  └─ AiAssistButton
├─ CandidateList
│  └─ CandidateCard
├─ MappingPanel（Season / Episode 映射时出现）
├─ StatusMessage
└─ Actions
   ├─ Cancel
   └─ ConfirmCorrection
```

---

## 5. 交互规则

- 打开弹窗时显示当前对象摘要和现有归属状态
- 用户先选择目标类型，再搜索或使用 AI 辅助候选
- 选择候选后，弹窗保留在当前上下文中，允许核对后确认
- Season 或 Episode 映射场景显示必要的季号、集号映射字段
- 点击取消或关闭不提交修正
- 修正成功后关闭弹窗并刷新当前详情；如类型已改变，可设计为跳转至修正后的对应详情页
- 成功后的刷新 / 跳转为体验方案，本阶段不改变业务逻辑

---

## 6. 状态

| 状态 | 展示要求 |
|---|---|
| Initial | 显示当前对象摘要与目标类型选择 |
| Loading | 搜索或 AI 处理中显示加载状态，防止重复提交 |
| Empty | 无候选时显示中文空状态，保留重新搜索入口 |
| Error | 显示中文错误提示，并允许重试或取消 |
| Candidate selected | 高亮已选候选，确认按钮可用 |
| Mapping incomplete | 缺少季号 / 集号映射时说明缺项并禁用确认 |
| Running | 修正提交中锁定关键输入和提交按钮 |
| Success | 显示成功反馈，并按设计刷新或跳转 |
| Cancelled | 关闭弹窗，不改变当前归属 |

---

## 7. 与来源操作和确认弹窗的关系

| 操作 | UI 承载 | 风险级别 |
|---|---|---|
| 探测播放源 | 详情来源行直接执行 | 普通操作 |
| 设置默认播放源 | 详情来源行直接执行 | 普通操作 |
| 从当前对象拆分来源 | `global-dialogs.md` 警示确认 | 会改变软件内来源归属 |
| 确认识别修正 | 本弹窗确认动作 | 会改变 metadata / 类型归属的修正操作 |
| 删除记录 / 移出媒体库 | 媒体库规格与全局弹窗 | 不在详情页新增入口 |

所有涉及来源的说明必须保持安全语义：不会因为修正或拆分文案而表示删除真实本地文件或 WebDAV 文件。

---

## 8. 文案规则

- 标题、按钮、状态、提示和错误文案统一使用中文
- 不使用“后续阶段接入”“稍后支持”等与当前真实能力冲突的最终界面文案
- `未识别`、`无播放源`、`仅 metadata`、`修正中` 和 `修正失败` 分别表达不同状态，不互相替代
- 路径仅显示安全摘要，避免暴露完整来源位置

---

## 9. 验收标准

- Movie、Season、Episode 和未识别详情承载均有统一修正弹窗入口设计
- 弹窗覆盖目标类型选择、搜索、候选、AI 辅助、映射、确认、取消及完整状态
- 最终详情结构不依赖常驻修正 Tab / 面板
- Grouped placeholder 从已有详情链路触发修正，而不是被设计为无详情
- 来源拆分与修正动作具有清晰区分，并引用正确确认等级
- 全部面向用户的最终文案使用中文
- 弹窗不新增删除记录、移出媒体库或人工聚合主入口
