# XFVerse Agent Instructions

本文件是 XFVerse 项目的项目级 Codex / coding agent 稳定工作规则。
后续所有 Codex / coding agent 任务必须先阅读并遵守本文件。
本文件不替代阶段文档；每个阶段的计划、日志、验收和 Known Issues 仍维护在 `docs/` 下。

## 1. 项目概览

- 项目名：XFVerse。
- 产品形态：Windows 桌面媒体库 / 播放器 / 扫描识别 / 推荐 / 观影洞察项目。
- 解决方案：`MediaLibrary.sln`。
- 主要技术栈以仓库项目文件为准：
  - .NET 8。
  - WPF Windows 桌面应用：`src/MediaLibrary.App`，目标框架 `net8.0-windows`。
  - 核心类库：`src/MediaLibrary.Core`，目标框架 `net8.0`。
  - 工具项目：`src/MediaLibrary.Tools`，目标框架 `net8.0`。
  - Entity Framework Core 8 + SQLite。
  - Microsoft.Extensions.DependencyInjection。
  - 仓库包含 mpv / ffmpeg 相关本地资源拷贝配置。
- 当前核心目录：
  - `src/MediaLibrary.Core`：数据模型、EF Core、扫描识别、TMDB/OMDb/WebDAV/AI、推荐、观影统计等核心服务。
  - `src/MediaLibrary.App`：WPF UI、ViewModels、Views、导航和桌面应用组合根。
  - `src/MediaLibrary.Tools`：命令行/诊断工具入口。
  - `docs`：阶段计划、阶段日志、Known Issues、安装和使用说明。

## 2. 常用命令

- Build：`dotnet build MediaLibrary.sln`。
- Git 基线检查：
  - `git status --short --branch`
  - `git diff --stat`
  - `git diff --name-only`
  - `git diff --name-only -- src/MediaLibrary.Core/Data/Migrations`
- 当前仓库未发现默认 test 项目；默认验证以 `dotnet build MediaLibrary.sln` 为主，不要编造测试命令。
- 只改 Markdown 的文档阶段，build 可选；若执行 build，必须记录结果。

## 3. Git / 数据库 / Migration 安全规则

- 默认不 commit。
- 默认不 push。
- 默认不执行 database update。
- 默认不新增 migration，除非用户明确允许。
- 每次最终报告必须说明 migrations diff 是否为空。
- 如果任务似乎需要 migration，先报告原因、影响面和最小方案，不要擅自新增。
- 不要用 `git reset --hard`、`git checkout --` 等方式回滚用户或其他 agent 的改动，除非用户明确要求。
- 工作区可能已有他人改动；只处理本次目标范围内的文件。

## 4. 阶段基线开发模式

- 每次只做当前阶段目标。
- 阶段外问题记录为 Deferred，不顺手大改。
- 遇到证据不足的问题，先输出证据和最小补丁计划。
- 不以 bound 数量作为扫描成功唯一标准。
- 不追求“看起来识别更多”而牺牲准确率。
- 用户要求不要无脑附和；如果任务假设有风险，报告风险和可验证方案。
- 保持补丁小而可解释，优先沿用现有架构、命名、服务边界和文档格式。

## 5. 日志与隐私

- 日志必须脱敏。
- 不输出完整本地路径。
- 不输出完整 WebDAV URL。
- 不输出账号、token、password、API key。
- 文档和最终报告也遵守脱敏规则。
- 诊断可以输出安全的文件名样本、脱敏路径、计数、hash 或截断信息。
- 不把用户本地目录、远端目录、密钥或私有样本写入长期文档。

## 6. 媒体库语义红线

- 删除记录：清软件记录，不删除本地文件 / WebDAV 文件。
- 移出媒体库：只影响可见性 / hide。
- 移出媒体库不清除想看、喜爱、不想看、已看等用户状态。
- 移出媒体库不删除 metadata。
- 只有“删除记录”才清除相关软件记录。
- 不创建不存在的 `MediaFile`。
- 不删除本地文件。
- 不删除 WebDAV 文件。
- 任何批量操作都必须保留上述语义，不得用 UI 文案绕开。

## 7. 扫描 / AI / 识别规则红线

- 不允许在生产代码中硬编码具体电影名、剧名、动漫名、文件夹名、文件名前缀、TMDB ID 特例。
- 示例只能用于验收，不得作为生产分支条件。
- 默认扫描宁可 placeholder / NeedsReview / ai-candidate，也不要高置信错绑。
- TV 在 Phase 4 内不进入 Watch Insights / AI 推荐 / fingerprint，除非用户另开阶段。
- AI 返回内容只能作为 hint，最终仍需走本地规则 / TMDB / 用户确认等阶段定义的安全门。
- 不把日志字段反向变成隐藏业务规则。
- 不用“提高识别率”作为牺牲可解释性、可回滚性或准确率的理由。

## 8. 当前长期产品语义摘要

- 本地文件夹支持已完成。
- 电视剧支持处于 Phase 4 收口阶段。
- 媒体库内容分类包含：全部 / 电影 / 电视剧 / 其他。
- 识别状态筛选 UI 已隐藏或计划隐藏，但后端能力保留。
- grouped TV-like placeholder / 未识别季可复用 Season 相关 UI，但必须标注未识别 / 待修正。
- 后续计划包括：Episode 详情页 + 多播放源、统一修正入口、在线字幕搜索、数据健康检查、最终 UI、发布打包。
- 本摘要只记录稳定方向；具体阶段状态以对应 `docs/` 文档为准。

## 9. 文档维护规则

- AGENTS.md 不替代阶段文档。
- 实现阶段必须更新相关 docs。
- TV / 电视剧相关优先更新 `docs/tv-support/*`。
- 电影发现 / 媒体库 / 推荐相关优先更新 `docs/movie-discovery/*`。
- 其他主题按现有目录维护，例如 `docs/player`、`docs/local-folder`、`docs/watch-insights`、`docs/ui-redesign`。
- 文档至少记录：
  - 阶段目标。
  - 完成内容。
  - 明确未做事项。
  - 验证结果。
  - Known Issues。
- Known Issues 固定分类：
  - Blocker
  - Deferred
  - Noise
- 不要把阶段文档写成无关流水账。
- 不要把具体私有样本、完整本地路径或账号信息写入 docs。

## 10. 最终报告格式

每次最终报告必须包含：

- 当前分支和工作区状态。
- 修改文件。
- 新增文件。
- 删除文件。
- migration 状态。
- 完成内容。
- 明确未做事项。
- build 结果。
- 关键验证结果。
- 人工验收矩阵，通常 8～12 条即可；高风险阶段可更多。
- Known Issues：Blocker / Deferred / Noise。
- `git diff --stat`。
- 是否建议进入下一阶段。
- 建议 commit message。

## 11. 不同任务类型的推荐工作流

- 只读审计：不改文件，必须给日志/代码证据。
- 小修 / Bugfix：只修目标问题，保持范围小，避免阶段外重构。
- 新功能：先明确首版范围和不做范围，再实现最小闭环。
- UI / 体验：不改变业务语义，重点验证筛选、批量操作、空状态和错误态。
- 大阶段：先 plan / 审计，再实现；必要时分批提交补丁建议。
- 文档阶段：只改指定文档，不借机修改业务代码。

## 12. Prompt 使用建议

后续任务 prompt 不需要重复本文件中的固定规则，只需要写：

- 阶段名。
- 本次目标。
- 直接背景证据。
- 只做 / 不做的阶段特有内容。
- 重点文件。
- 是否允许 migration / database update / commit / push；默认不允许。

如果 prompt 与 AGENTS.md 冲突，先报告冲突并请求澄清；若用户明确覆盖某条规则，以用户当前明确授权为准。
