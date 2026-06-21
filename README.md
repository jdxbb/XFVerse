# XFVerse

XFVerse 是基于 .NET 8 与 WPF 的 Windows 桌面影音管理系统，支持本地文件夹和 WebDAV 媒体的扫描、识别、管理与播放，并提供影片发现、在线字幕、推荐和电影观影洞察。

**[访问 XFVerse 官网](https://xfverse.fun/)** · **[官网文档中心](https://xfverse.fun/docs/)** · [安装与升级](https://xfverse.fun/docs/install.html) · [使用指南](https://xfverse.fun/docs/guide.html) · [帮助与排障](https://xfverse.fun/docs/help.html)

## 当前版本

- 产品版本：`1.0.0`
- 当前状态：Release Candidate 已完成自动、进程级和当前设备人工验收；因缺少可追踪干净重建、Windows 11 x64 原生环境和 Windows 10 x64 环境，不宣称已经 GA。
- 发布架构：Windows x64、Windows ARM64 独立 self-contained 候选安装包。
- 项目语言：简体中文。
- X86、Portable 包和自动更新器不在 1.0 范围。

## 核心能力

- 本地文件夹与 WebDAV 媒体来源。
- 电影、电视剧、季、集和多播放源管理。
- 扫描、识别、未识别内容保留、人工修正和安全复扫。
- WPF 媒体库、搜索、筛选、排序和批量操作。
- 基于 mpv 的本地/WebDAV 播放、续播、音轨、嵌入字幕和外挂字幕。
- OpenSubtitles 在线字幕搜索、下载、绑定和缓存管理。
- TMDB 影片搜索、榜单、元数据和识别修正。
- 喜爱、想看、不想看、已看、观影历史和收藏夹。
- 电影 AI 推荐、电影观影统计与画像。
- 本地用户资料、主题、缓存和外部服务设置。

XFVerse 1.0 的 AI 推荐、观影统计和画像为 Movie-only；电视剧不进入这些能力。

## 候选安装包

当前仓库存在以下 1.0.0 候选产物。公共下载页面尚未建立；在 Phase 8.9 的 Blocker 清零前，不提供虚构的 GA 下载链接。

| Windows 架构 | 候选文件 | 大小 | SHA-256 |
| --- | --- | --- | --- |
| x64 | `XFVerse-Setup-1.0.0-win-x64.exe` | 266,950,555 bytes / 254.58 MiB | `6D7641FBEB7E20FC282EE23BF81DF7ECA1CE81DFE6D4366ED1DE38D167F04A15` |
| ARM64 | `XFVerse-Setup-1.0.0-win-arm64.exe` | 240,257,892 bytes / 229.13 MiB | `6C75397ADAD4CEDF6374CA936D367D989C817ABB0ADAF9A0A31ECD6DADA52BC8` |

两个候选安装包当前均未进行 Authenticode 数字签名，Windows 可能显示未知发布者。安装前应核对来源、架构和完整 SHA-256，不要通过关闭系统安全功能绕过检查。

对应源代码包：

| 架构 | 文件 | SHA-256 |
| --- | --- | --- |
| x64 | `XFVerse-1.0.0-corresponding-source-win-x64.zip` | `295DD94628A74B0D85890882B14EC4A2F1D42015AF31D28A3E8018F84941D8E9` |
| ARM64 | `XFVerse-1.0.0-corresponding-source-win-arm64.zip` | `7836F690311094DC3CDE280A53D09A4A0C7C1A68C417E3F8BC9ED5ACDE7883C6` |

## 系统要求

### 使用安装包

- Windows 桌面系统。
- 与安装包匹配的 x64 或 ARM64 架构。
- 建议至少预留 1 GB 可用空间；数据库、海报、字幕和视频缓存会继续占用空间。
- self-contained 安装包不要求用户另行安装 .NET Desktop Runtime。

ARM64 已通过原生安装、首次启动、数据库和 libmpv 基础媒体载入验证；Windows 11 x64 原生和 Windows 10 x64 仍待验收，最终系统支持声明以 Blocker 关闭结果为准。

### 使用源码

- Windows。
- .NET SDK 8。
- PowerShell。
- 正式安装器构建另需 Inno Setup 6。

当前已记录的发布工具链为 .NET SDK `8.0.420` 与 Inno Setup `6.7.1`。

## 快速开始

1. 选择与 Windows 架构匹配的安装包。
2. 核对 SHA-256。
3. 按[安装说明](docs/安装说明.md)完成安装。
4. 从用户菜单进入“扫描任务”。
5. 添加本地目录，或在设置中配置 WebDAV。
6. 分别运行本地扫描或 WebDAV 扫描。
7. 在媒体库检查电影、电视剧和未识别内容。
8. 打开详情页选择播放源并播放。

TMDB、OMDb、OpenSubtitles、AI 和 WebDAV 均按对应功能使用，不是本地媒体库启动的共同前置条件。

## 文档

| 文档 | 用途 |
| --- | --- |
| [XFVerse 官网](https://xfverse.fun/) | 产品功能、界面预览、隐私说明和常见问题 |
| [官网文档中心](https://xfverse.fun/docs/) | 面向最终用户的安装、使用、帮助、发布与隐私文档 |
| [安装说明](docs/安装说明.md) | 架构选择、校验、安装、升级、修复、卸载与数据保留 |
| [软件使用说明书](docs/使用说明书.md) | 完整功能、界面、操作流程和数据语义 |
| [帮助中心](docs/help/README.md) | 按症状查找故障原因、安全检查和恢复步骤 |
| [软件设计说明书](docs/release-v1.0/XFVERSE_1_0_SOFTWARE_DESIGN.md) | 总体架构、模块、数据、流程、安全、部署与 UI 设计 |
| [XFVerse 1.0.0 发布说明](docs/release/XFVerse-1.0.0-发布说明.md) | 候选范围、变化、升级注意事项和限制 |
| [XFVerse 1.0.0 RC 验收报告](docs/release-v1.0/XFVERSE_1_0_RC_REPORT.md) | 已通过证据、未执行矩阵、Blocker 和发布前必做项 |
| [第三方声明](docs/third-party/THIRD_PARTY_NOTICES.md) | 第三方组件、许可证、来源和对应源代码状态 |
| [正式打包记录](docs/release-v1.0/XFVERSE_1_0_PACKAGING_RECORD.md) | 构建参数、产物、哈希、体积和安装生命周期证据 |

## 从源码构建

在仓库根目录执行：

```powershell
dotnet restore MediaLibrary.sln
dotnet build MediaLibrary.sln
```

Release 构建：

```powershell
dotnet build MediaLibrary.sln -c Release
```

解决方案包含：

- `src/MediaLibrary.App`：WPF Windows 桌面应用，`net8.0-windows`。
- `src/MediaLibrary.Core`：数据、扫描、识别、外部服务和业务核心，`net8.0`。
- `src/MediaLibrary.Tools`：命令行与诊断工具，`net8.0`。

当前仓库没有默认 test 项目。验证命令不得假定存在未提供的测试工程。

## 正式打包

正式打包与测试安装包链路相互独立。先生成对应源代码包：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Build-CorrespondingSource.ps1
```

然后构建两个架构的候选安装器：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Build-ReleaseInstaller.ps1 `
  -RuntimeIdentifier win-x64

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Build-ReleaseInstaller.ps1 `
  -RuntimeIdentifier win-arm64
```

如果 Inno Setup 6 的 `ISCC.exe` 不在仓库临时工具目录或 `PATH` 中，应通过 `-InnoSetupPath` 显式指定。

安装生命周期验证：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Test-ReleaseInstaller.ps1 `
  -RuntimeIdentifier win-x64

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File scripts/packaging/Test-ReleaseInstaller.ps1 `
  -RuntimeIdentifier win-arm64
```

正式脚本会重建对应 `artifacts/release/1.0.0/<RID>/` 目录。执行前应确认不需要保留其中的旧本地产物。

详细参数和验证结果见[正式打包记录](docs/release-v1.0/XFVERSE_1_0_PACKAGING_RECORD.md)。

## 数据与隐私

默认用户数据目录：

```text
%LOCALAPPDATA%\MediaLibrary
```

该目录保存数据库、设置、用户状态、历史、资料、缓存和日志。内部目录名为兼容历史保留。

核心数据语义：

- 移出媒体库：只隐藏软件记录，保留 metadata 和用户状态。
- 删除记录：删除相关 XFVerse 软件记录，不删除本地或 WebDAV 文件。
- 删除扫描路径：删除扫描配置，不删除目录或远端内容。
- 清理缓存：只清理 XFVerse 管理的缓存。
- 默认卸载：删除程序文件，保留用户数据。

日志和问题报告不得包含完整本地路径、完整 WebDAV URL、账号、密码、Token、API Key、数据库或私人媒体清单。

## 外部服务

| 服务 | 用途 | 是否为基础启动必需 |
| --- | --- | --- |
| WebDAV | 远端扫描和播放 | 否 |
| TMDB | 在线搜索、榜单、识别、修正和元数据 | 否 |
| OMDb | 部分外部评分补充 | 否 |
| OpenSubtitles | 在线字幕搜索和下载 | 否 |
| AI 兼容服务 | 辅助识别、电影推荐和电影画像 | 否 |

外部服务凭据使用当前 Windows 用户上下文的本机保护机制保存，不提供云同步。

## 当前限制

- 当前候选包来自未提交工作区，尚不能只依靠记录的 Git 提交号重现。
- Windows 11 x64 原生和 Windows 10 x64 尚未完成 P0 验收。
- 当前 Windows 11 ARM64 设备的 UI 与业务人工验收已由用户确认通过；该结论没有替代 x64/Windows 10 环境验收。
- 候选安装包未签名。
- 没有自动更新器；升级采用手动下载安装器覆盖安装。
- 没有云账号、真实登录或跨设备同步。
- X86 和 Portable 包不受支持。
- WebDAV 大文件、HEVC 4K 和不同网络环境的实际表现仍需最终 RC 记录。
- GPL 原生组件已建立双架构对应源代码包；公开发布时必须把匹配的源码包和 SHA-256 与安装包同时提供。

## 许可证与第三方组件

仓库当前没有项目自身的根 `LICENSE` 文件，因此不得将 XFVerse 项目代码描述为已采用某种开源许可证，也不得据此推定获得复制、修改或再分发授权。

随包第三方组件保持各自许可证。主要包括：

- .NET / WPF。
- Entity Framework Core、Microsoft.Data.Sqlite 和 SQLitePCLRaw。
- SQLite。
- mpv / libmpv。
- FFmpeg / ffprobe。
- SmartDate-inspired date picker。
- Phosphor Icons。
- Inno Setup。

完整组件版本、哈希、许可证和来源见[第三方声明](docs/third-party/THIRD_PARTY_NOTICES.md)。
