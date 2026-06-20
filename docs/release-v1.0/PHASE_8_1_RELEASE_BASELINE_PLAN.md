# Phase 8.1：发布基线与文档信息架构审计

Last updated: 2026-06-20

## 阶段状态

- 状态：已完成。
- 完成日期：2026-06-20。
- 前置条件：`PHASE_8_PLAN.md` 已建立。
- 完成后下一阶段：Phase 8.2。

## 目标

冻结 XFVerse 1.0 的真实产品范围和发布事实，为代码收口、打包和文档提供单一证据基线。

## 只做

1. 审计代码、项目文件、运行目录、现有阶段文档和测试安装包。
2. 建立用户可见功能矩阵：
   - 页面和入口。
   - 核心动作。
   - 前置配置。
   - 成功、空、错误和降级状态。
   - 数据影响。
   - Movie / TV 边界。
   - 对应代码证据。
3. 冻结 1.0 支持平台、架构和安装模型。
4. 决定 ARM64、数字签名、自动更新是否进入 1.0。
5. 建立文档目录、术语表和交叉链接结构。
6. 建立发布阻断项清单和风险优先级。
7. 建立 RC 验收环境矩阵。

## 不做

- 不修改业务代码。
- 不修改数据库结构。
- 不生成正式安装包。
- 不开始大规模截图。
- 不在证据不足时宣称功能完成或平台受支持。

## 必读与重点文件

- `AGENTS.md`
- `docs/release-v1.0/PHASE_8_PLAN.md`
- Phase 4～7 的计划、日志和 Known Issues。
- `src/MediaLibrary.App/MediaLibrary.App.csproj`
- `src/MediaLibrary.App/App.xaml.cs`
- `src/MediaLibrary.App/ViewModels/Main/MainWindowViewModel.cs`
- 所有用户可见 Page / Dialog / Player ViewModel。
- `src/MediaLibrary.Core/Helpers/AppPaths.cs`
- `src/MediaLibrary.Core/Services/Implementations/DatabaseInitializer.cs`
- `scripts/packaging/*`
- `docs/安装说明.md`
- `docs/使用说明.md`
- `README.md`

## 核心交付物

### 1. 1.0 功能证据矩阵

建议新增 `XFVERSE_1_0_FEATURE_MATRIX.md`，字段至少包括：

| 字段 | 说明 |
| --- | --- |
| 功能域 | 安装、扫描、媒体库、播放器等 |
| 用户入口 | 页面、菜单或按钮 |
| 实际能力 | 代码当前支持的行为 |
| 前置条件 | API、网络、媒体源、数据量等 |
| 数据影响 | 新增、修改、隐藏、删除软件记录或仅清缓存 |
| Movie / TV 边界 | 功能是否支持 Movie、Season、Episode |
| 错误与降级 | 未配置或失败时的表现 |
| 代码证据 | View / ViewModel / Service |
| 文档归属 | 安装说明、使用说明书、帮助文档或 README |
| RC 验证状态 | 未验证 / 通过 / 失败 |

### 2. 发布决策记录

至少明确：

- x64 是否为唯一 GA 架构。
- ARM64 是正式支持、预览还是不发布。
- 自包含或依赖 .NET Desktop Runtime。
- 单架构安装包或多架构安装包。
- 数字签名状态。
- 自动更新状态。
- 安装范围：当前用户或全局。
- 默认安装目录。
- 用户数据目录。
- 升级、修复和卸载的数据保留原则。

### 3. 文档信息架构

建议目标结构：

```text
README.md
docs/
  安装说明.md
  使用说明书.md
  release-v1.0/
    XFVERSE_1_0_SOFTWARE_DESIGN.md
  help/
    帮助中心.md
    安装与启动.md
    扫描与识别.md
    播放与字幕.md
    网络与外部服务.md
    数据缓存与恢复.md
    诊断信息.md
  release/
    XFVerse-1.0.0-发布说明.md
  third-party/
    THIRD_PARTY_NOTICES.md
```

实际目录可根据现有仓库调整，但职责边界不得改变。

## 验收矩阵

| ID | 检查项 | 结果与证据 |
| --- | --- | --- |
| 8.1-A01 | 所有主导航、隐藏详情页、对话框和播放器入口已列入功能矩阵。 | 通过。功能矩阵覆盖六个主导航、详情/扫描/推荐/设置隐藏路由、三个对话框、播放器和在线字幕窗口。 |
| 8.1-A02 | 本地目录与 WebDAV 的配置、扫描和删除语义已分别记录。 | 通过。见 F-030～F-035、RC-S01～RC-S08。 |
| 8.1-A03 | Movie、TV、Season、Episode 的支持边界已记录。 | 通过。功能矩阵逐项标注边界，并明确 TV 不进入 1.0 洞察、AI 推荐画像和 fingerprint。 |
| 8.1-A04 | TMDB、OMDb、OpenSubtitles、AI 的用途和可选性已记录。 | 通过。见功能矩阵“外部依赖与降级基线”。 |
| 8.1-A05 | 数据目录、数据库初始化和 migration 行为已记录。 | 通过。见 F-075、F-076、发布决策 RD-012～RD-016。 |
| 8.1-A06 | x64、ARM64、自包含、签名和更新策略已有明确决策。 | 通过。x64 self-contained 为 GA；ARM64/X86/自动更新不进入 1.0；签名不作为功能承诺。 |
| 8.1-A07 | README、软件设计说明书、安装说明、使用说明书、帮助文档的目录和边界已冻结。 | 通过。见 `XFVERSE_1_0_DOCUMENTATION_ARCHITECTURE.md`。 |
| 8.1-A08 | 所有发布 Blocker 都有目标阶段和验证方式。 | 通过。P8-B01～P8-B06 均有要求和目标阶段，RC 矩阵包含对应验证项。 |
| 8.1-A09 | 未发现把历史计划当作当前功能的无证据结论。 | 通过。矩阵区分“代码已审计”“RC 待验证”“Deferred”“Blocked”。 |
| 8.1-A10 | migration diff 为空。 | 通过。`git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 无输出。 |

## 完成时维护

- 本文件：状态、实际完成、验证结果、未做事项。
- `PHASE_8_STAGE_LOG.md`
- `PHASE_8_KNOWN_ISSUES.md`
- 新增的功能矩阵和发布决策记录。

## 阶段执行记录

- 完成内容：
  - 只读审计主导航、隐藏详情页、弹窗、播放器、扫描与识别、外部服务、设置、用户资料、数据目录、数据库初始化和现有测试打包链路。
  - 新增 `XFVERSE_1_0_FEATURE_MATRIX.md`，建立代码—UI—文档—RC 证据矩阵。
  - 新增 `XFVERSE_1_0_RELEASE_DECISIONS.md`，冻结 1.0 平台、架构、运行时、安装、升级、卸载、签名和更新策略。
  - 新增 `XFVERSE_1_0_DOCUMENTATION_ARCHITECTURE.md`，区分 README、安装说明、软件使用说明书、帮助文档、软件设计说明书、发布说明和第三方声明。
  - 新增 `XFVERSE_1_0_RC_ENVIRONMENT_MATRIX.md`，定义 Windows、安装生命周期、扫描、播放、数据、安全和合规的 RC 验收环境。
  - 冻结 Windows 10/11 x64 为 1.0 GA 目标；ARM64、X86、Portable 和自动更新不进入 1.0。
  - 冻结 x64 self-contained、当前用户安装、默认卸载保留用户数据和正式/测试安装器隔离策略。
  - 确认 Watch Insights、AI 推荐画像和 fingerprint 保持 Movie-only。
  - 确认所有删除、移出媒体库、扫描路径删除和缓存清理的数据语义。
- 修改文件：
  - `PHASE_8_1_RELEASE_BASELINE_PLAN.md`
  - `PHASE_8_STAGE_LOG.md`
  - `PHASE_8_KNOWN_ISSUES.md`
- 新增文件：
  - `XFVERSE_1_0_FEATURE_MATRIX.md`
  - `XFVERSE_1_0_RELEASE_DECISIONS.md`
  - `XFVERSE_1_0_DOCUMENTATION_ARCHITECTURE.md`
  - `XFVERSE_1_0_RC_ENVIRONMENT_MATRIX.md`
- 删除文件：无。
- 明确未做事项：
  - 未修改业务代码、项目文件、资源、脚本或安装器。
  - 未修改数据库结构，未新增 migration，未执行 database update。
  - 未执行 build、publish、installer 或正式 RC 人工验收。
  - 未生成正式包、截图、README、安装说明、软件使用说明书、帮助文档或软件设计说明书正文。
  - 未 commit，未 push。
- build / publish / installer：均未执行；本阶段为纯文档和只读审计。
- 关键验证结果：
  - 所有主导航、隐藏详情页、三个对话框、播放器和在线字幕窗口均已纳入矩阵。
  - 本地目录与 WebDAV 的配置、扫描、重扫、播放和删除边界已分别记录。
  - Movie、Series、Season、Episode 的功能边界以及 TV 的 1.0 排除项已记录。
  - TMDB、OMDb、OpenSubtitles、AI、mpv 和 ffprobe 的用途、可选性与降级方式已记录。
  - 用户数据目录、数据库启动迁移和正式安装数据保留原则已记录。
  - 现有测试安装器被明确判定为不可发布，并要求正式链路使用新 AppId 和空 staging。
- migration 状态：`git diff --name-only -- src/MediaLibrary.Core/Data/Migrations` 为空。
- Known Issues：
  - Blocker：P8-B01～P8-B06，分别涉及测试包用户数据、危险覆盖、版本源、凭据保护、第三方许可和安装器身份隔离。
  - Deferred：ARM64、数字签名承诺、包体积优化、自动更新、日志目录统一等按 `PHASE_8_KNOWN_ISSUES.md` 处理。
  - Noise：历史测试产物和本地打包工具准备不作为正式功能缺陷。
- `git diff --stat`：当前 Phase 8 文档目录均为未跟踪文件，标准 `git diff --stat` 不显示未跟踪文件；以 `git status --short` 和文件清单为准。
- 是否建议进入 Phase 8.2：建议。8.1 已提供软件设计说明书所需的产品、数据、模块、UI 流程和发布边界基线；发布 Blocker 在 8.3/8.4 处理，不阻断 8.2 文档工作。

## 建议 commit message

`docs(release): define XFVerse 1.0 release baseline`
