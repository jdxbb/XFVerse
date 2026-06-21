# Phase 8.8：README、发布说明与合规文档

Last updated: 2026-06-21

## 阶段状态

- 状态：已完成；P8-B05 已关闭。
- 前置条件：Phase 8.5～8.7 主体完成，Phase 8.4 第三方清单可用。
- 完成后下一阶段：Phase 8.9。

## 目标

完成正式项目入口、1.0.0 发布说明和第三方合规材料，使用户和开发者能快速理解、安装、使用和审计 XFVerse。

## 1. 根 README

### 定位

README 是项目入口和文档导航，不是完整说明书。

### 建议目录

1. XFVerse 名称、Logo 和一句准确简介。
2. 当前稳定版本和发布状态。
3. 核心能力。
4. 截图或简洁产品预览。
5. 系统要求。
6. 快速开始。
7. 文档导航：
   - 软件设计说明书。
   - 安装说明。
   - 软件使用说明书。
   - 帮助中心。
   - 发布说明。
   - 第三方声明。
8. 开发环境和构建。
9. 正式打包。
10. 数据、隐私和非破坏性文件语义。
11. 外部服务说明。
12. Known Issues / 支持边界。
13. 许可证和第三方组件。

### README 规则

- 不把 WebDAV 写成唯一媒体来源，本地目录已支持。
- 不把 AI 写成核心启动必需项。
- 可说明存在独立 ARM64 正式候选安装包，但在 Phase 8.9 应用级 RC 完成前，不宣称未经验证的具体 Windows ARM64 系统版本支持。
- 下载区必须分别列出 x64 与 ARM64 文件名、适用架构和 SHA-256，不能只写“Windows 版”。
- 下载链接只指向真实 GA 产物。
- 构建命令以仓库实际命令为准。
- README 中不放完整故障排查。

## 2. 1.0.0 发布说明

建议新增 `docs/release/XFVerse-1.0.0-发布说明.md`。

至少包括：

- 版本和发布日期。
- 支持平台和架构。
- 安装包文件名、大小和 SHA-256。
- 1.0 核心能力。
- 与测试版相比的安装和数据变化。
- 升级注意事项。
- 已知限制。
- 外部服务和网络要求。
- 数字签名状态。
- 安装、使用和帮助文档链接。
- 回滚到上一版本的安全原则。

## 3. 第三方声明

建议新增或汇总：

- `docs/third-party/THIRD_PARTY_NOTICES.md`
- 随安装包分发的 `THIRD-PARTY-NOTICES.txt`
- 必需的 LICENSE 文件目录

每项必须对应 Phase 8.4 的真实产物清单。

## 4. 项目自身许可证

Phase 8.1 应确认项目是否有明确的开源或专有许可证。

- 如果没有项目 LICENSE，不得在 README 中暗示可自由复制或再分发。
- 如用户希望公开开源，需要单独选择许可证并确认第三方兼容性。
- 本阶段不自行替用户决定项目许可证。

## 验收矩阵

| ID | 检查项 |
| --- | --- |
| 8.8-A01 | README 可在两次点击内到达软件设计、安装、使用、帮助和发布说明。 |
| 8.8-A02 | README 核心能力与 1.0 功能矩阵一致。 |
| 8.8-A03 | README 的构建和打包命令真实可用。 |
| 8.8-A04 | README 不复制完整安装说明或说明书。 |
| 8.8-A05 | 发布说明中的版本、架构、大小和 SHA-256 与 GA 候选产物一致。 |
| 8.8-A06 | 发布说明披露签名状态和 Known Issues。 |
| 8.8-A07 | 第三方声明覆盖实际随包组件。 |
| 8.8-A08 | 所有文档链接有效。 |
| 8.8-A09 | README 和发布说明不含私有路径、账号、URL 或密钥。 |
| 8.8-A10 | 项目自身许可证状态说明准确。 |
| 8.8-A11 | README 和发布说明分别列出 x64/ARM64 产物，不混淆架构或校验值。 |

## 完成时维护

- 本文件。
- 根 `README.md`
- `docs/release/*`
- `docs/third-party/*`
- `PHASE_8_STAGE_LOG.md`
- `PHASE_8_KNOWN_ISSUES.md`

## 阶段执行记录

- 完成日期：2026-06-21。
- 完成内容：
  - 重写根 `README.md`，建立产品简介、双架构候选包、快速开始、文档导航、开发构建、正式打包、数据语义、隐私和许可证入口。
  - 新增 `docs/release/XFVerse-1.0.0-发布说明.md`，记录候选状态、x64/ARM64 文件名、大小、SHA-256、签名、升级、回滚和已知限制。
  - 新增 `docs/third-party/THIRD_PARTY_NOTICES.md` 和 `CORRESPONDING_SOURCE.md`。
  - 确认 libmpv 来自 `shinchiro/mpv-winbuild-cmake` 20260419 发布。
  - 将 x64/ARM64 ffprobe 统一替换为 BtbN FFmpeg 8.1.2 固定发布。
  - 生成双架构对应源代码包、对象映射和 SHA-256。
  - 正式打包脚本增加第三方声明、对应源代码状态和源码包校验清单复制；安装生命周期脚本增加安装后文件存在性检查。
- 修改文件：
  - `README.md`
  - `tools/ffmpeg/win-x64/ffprobe.exe`
  - `tools/ffmpeg/win-arm64/ffprobe.exe`
  - `tools/ffmpeg/win-x64/README.md`
  - `tools/ffmpeg/win-arm64/README.md`
  - `scripts/packaging/Build-ReleaseInstaller.ps1`
  - `scripts/packaging/Test-ReleaseInstaller.ps1`
  - `docs/third-party/XFVERSE_1_0_THIRD_PARTY_INVENTORY.md`
  - Phase 8 总计划、8.8/8.9 计划、阶段日志、Known Issues、文档信息架构和正式打包记录。
- 新增文件：
  - `docs/release/XFVerse-1.0.0-发布说明.md`
  - `docs/third-party/THIRD_PARTY_NOTICES.md`
  - `docs/third-party/CORRESPONDING_SOURCE.md`
  - `scripts/packaging/Build-CorrespondingSource.ps1`
- 删除文件：无。
- 明确未做事项：
  - 未修改业务逻辑、ViewModel、XAML、数据库模型或 migration。
  - 未建立公开下载页，未宣布 GA。
  - 未执行应用级 RC、首次启动、数据库初始化、旧数据升级或实际播放。
- build 结果：
  - `dotnet restore MediaLibrary.sln`：PASS。
  - `dotnet build MediaLibrary.sln -c Release --no-restore`：PASS，0 warning，0 error。
  - 三个正式打包相关 PowerShell 脚本语法检查：PASS。
  - 双架构对应源代码包生成与内部 SHA-256 校验：PASS。
  - x64/ARM64 publish、安装器编译和 staging 敏感扫描：PASS。
  - x64/ARM64 首次安装、同版本修复和卸载生命周期：PASS。
- 链接、许可证和产物信息验证结果：
  - README、发布说明和第三方文档的本地相对链接：PASS。
  - 文档敏感信息模式扫描：PASS。
  - 两个候选安装包 SHA-256 在 README 和发布说明中各出现一次，并与 Phase 8.4 正式打包记录一致。
  - 当前候选安装包 Authenticode 为 `NotSigned`，README 和发布说明已披露。
  - 仓库没有 XFVerse 项目自身根许可证，文档未暗示已开放再分发授权。
  - 新安装包内已包含第三方声明、对应源代码说明和源码包 SHA-256 清单。
- migration 状态：diff 为空；未新增 migration，未执行 database update。
- 人工验收矩阵结果：
  - 8.8-A01～A06：PASS。
  - 8.8-A07～A11：PASS。
- Known Issues：
  - Blocker：无；P8-B05 已关闭。
  - Deferred：双架构应用级 RC、数字签名、最终截图和文案实机复核。
  - Noise：`git diff --check` 仅报告工作区 LF/CRLF 转换提示。
- `git diff --stat`：当前共享工作区已跟踪差异为 24 files changed、1440 insertions、366 deletions；另有本阶段新增的发布说明、两份合规文档和源码打包脚本。被 `.gitignore` 忽略的双架构 ffprobe 替换及正式 artifacts 不计入 Git 统计。
- 是否建议进入 Phase 8.9：是。Phase 8.9 仍需完成全量应用级 RC 后才能宣布 GA。

## 建议 commit message

`build(release): close P8-B05 with source bundles`
