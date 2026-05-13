# Watch Insights Known Issues

## Blocker

- 当前无确认 blocker。

## Deferred

- 人格海报最终视觉 polish、更多个性化资源和用户展示偏好设置后置。
- 用户性别 / 展示偏好设置未接入；WI-8 默认使用 `female` 海报资源。
- 观影历史页面未实现，日历日期点击仍是预留入口，后续进入 UI-5。
- 最终视觉、动效、hover、图表精修后置；当前是功能可用版。
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
- 画像生成已拆为 5 个卡片级并发请求；DeepSeek 官方并发限制为动态策略，当前服务层使用 max concurrency 5，后续如遇 429 需要降低并发或增加重试。
- DeepSeek endpoint 下画像使用 `deepseek-v4-pro` + thinking high；推荐使用 `deepseek-v4-flash`；分类仍使用全局模型配置。非 DeepSeek endpoint 不强制覆盖模型。
- 叙事标签只存在于画像结果中，不写入 `Movie`，也不新增 DB 字段。
- 画像人格类型限定为最终版 23 个；非法类型会回退为“类型探索家”，必要时追加一次 AI 请求生成匹配描述。
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
