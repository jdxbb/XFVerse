# XFVerse 1.0 第三方声明

Last updated: 2026-06-21

## 说明

XFVerse 1.0 候选安装包包含第三方运行时、库、原生媒体组件、图标资源和安装工具生成的运行文件。

第三方组件保留各自版权和许可证。XFVerse 不主张拥有这些第三方组件。

完整版本、对象文件哈希和构建证据见[XFVerse 1.0 Third-Party Inventory](XFVERSE_1_0_THIRD_PARTY_INVENTORY.md)。

GPL 对应源代码状态见[XFVerse 1.0 对应源代码状态](CORRESPONDING_SOURCE.md)。

## 项目自身许可证

仓库当前没有 XFVerse 项目自身的根 `LICENSE` 文件。

因此：

- 不应把 XFVerse 项目代码描述为已经采用 MIT、GPL、Apache 或其他开源许可证。
- 不应因仓库可见而推定获得复制、修改、发布或再分发授权。
- 第三方组件的许可证不自动成为 XFVerse 项目自身许可证。
- 如后续公开源代码，应由项目权利人另行选择许可证并复核第三方兼容性。

## 随包许可证目录

Phase 8.4 候选安装包的 `licenses` 目录包含：

| 文件 | 内容 |
| --- | --- |
| `APACHE-2.0.txt` | Apache License 2.0 |
| `GPL-3.0.txt` | GNU General Public License version 3 |
| `Inno-Setup-LICENSE.txt` | Inno Setup License |
| `Microsoft-DotNet-LICENSE.txt` | Microsoft .NET License |
| `Microsoft-DotNet-THIRD-PARTY-NOTICES.txt` | .NET 第三方声明 |
| `Microsoft-WindowsDesktop-LICENSE.txt` | Windows Desktop Runtime License |
| `PHOSPHOR_ICONS_NOTICE.md` | Phosphor Icons 声明 |
| `SMARTDATE_NOTICE.md` | SmartDate-inspired 控件声明 |
| `XFVERSE_1_0_THIRD_PARTY_INVENTORY.md` | XFVerse 1.0 第三方组件审计清单 |

2026-06-21 合规重建后的两个候选安装包均已确认包含：

- `THIRD-PARTY-NOTICES.txt`
- `CORRESPONDING-SOURCE.txt`
- `CORRESPONDING-SOURCE-SHA256.txt`

## Microsoft .NET 与 WPF

### 组件

- .NET Runtime 8.0.26。
- WPF / Windows Desktop Runtime 8.0.26。
- Microsoft.Extensions.DependencyInjection 8.0.1。
- System.Security.Cryptography.ProtectedData 8.0.0。

### 来源

- https://github.com/dotnet/runtime
- https://github.com/dotnet/wpf

### 许可证

相关组件主要按 MIT 许可及 Microsoft 随包声明分发。完整文本位于安装目录的 `licenses` 文件夹。

## Entity Framework Core 与 Microsoft.Data.Sqlite

### 组件

- Entity Framework Core 8.0.11。
- Microsoft.Data.Sqlite 8.0.11。

### 来源

- https://github.com/dotnet/efcore

### 许可证

MIT。相关版权与第三方信息同时受 .NET 随包声明约束。

## SQLitePCLRaw 与 SQLite

### 组件

- SQLitePCLRaw 2.1.6。
- 由 SQLitePCLRaw 提供的 e_sqlite3 原生运行时。

### 来源

- https://github.com/ericsink/SQLitePCL.raw
- https://www.sqlite.org/

### 许可证

- SQLitePCLRaw：Apache License 2.0。
- SQLite：Public Domain。

Apache 2.0 完整文本随包提供。

## mpv / libmpv

### 分发文件

- x64：`mpv/win-x64/libmpv-2.dll`
- ARM64：`mpv/win-arm64/libmpv-2.dll`

### 版本

`v0.41.0-514-g06f4ce75a`

### 来源与版权

- 上游：https://github.com/mpv-player/mpv
- 已验证提交：https://github.com/mpv-player/mpv/commit/06f4ce75a
- 构建项目：https://github.com/shinchiro/mpv-winbuild-cmake
- 原始发布：https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260419
- 版权和许可证：https://github.com/mpv-player/mpv/blob/master/Copyright

对象文件属性声明 GNU GPL version 2 or later。由于分发的 Windows DLL 为包含多个原生组件的组合构建，XFVerse 使用 GPLv3 文本进行保守披露。

XFVerse 未修改两个 libmpv DLL。

两个 DLL 内嵌构建路径、架构和报告版本与 20260419 发布匹配。固定 mpv、FFmpeg 和构建系统快照已经纳入对应架构源码包，详情见[对应源代码状态](CORRESPONDING_SOURCE.md)。

## FFmpeg / ffprobe

### 双架构固定来源

- x64 文件：`tools/ffmpeg/win-x64/ffprobe.exe`
- ARM64 文件：`tools/ffmpeg/win-arm64/ffprobe.exe`
- 版本：`n8.1.2-20260620`
- 构建项目：https://github.com/BtbN/FFmpeg-Builds
- 固定发布：https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-06-20-13-30
- FFmpeg 8.1.2 提交：`38b88335f99e76ed89ff3c93f877fdefce736c13`

原来源不明的 ARM64 ffprobe 和原 Gyan x64 ffprobe 均已替换。两个新对象通过上游发布校验文件、PE 架构和 `-version` 执行验证。

### 许可证

两个 ffprobe 均报告 `--enable-gpl --enable-version3`，按 GNU GPL version 3 or later 处理。

XFVerse 未修改这两个 ffprobe 文件。

对应源代码闭环状态见[CORRESPONDING_SOURCE.md](CORRESPONDING_SOURCE.md)。

## SmartDate-inspired 日期选择控件

- 来源：https://github.com/JamesnetGroup/smartdate
- 许可证：MIT。
- 原始版权：Copyright (c) 2024 vickyqu115。
- XFVerse 修改：保留 WPF `Control`、`Popup` 和日历网格思路，替换视觉资源、命名和交互细节。

完整声明见[SMARTDATE_NOTICE.md](SMARTDATE_NOTICE.md)。

## Phosphor Icons

- 来源：https://github.com/phosphor-icons/core
- 许可证：MIT。
- 版权：Copyright (c) 2020 Phosphor Icons。
- XFVerse 修改：选择部分图标保存为本地 SVG 资源，并由 XFVerse 控件渲染。

完整 MIT 声明见[PHOSPHOR_ICONS_NOTICE.md](PHOSPHOR_ICONS_NOTICE.md)。

## Inno Setup

- 用途：生成安装器和卸载器。
- 版本：当前候选使用 6.7.1。
- 来源：https://jrsoftware.org/isinfo.php
- 许可证：Inno Setup License。

安装后的许可证目录包含所用编译器旁的许可证文本。

Inno Setup 是构建工具；其生成的安装器运行文件随 XFVerse 安装包分发。

## 许可证和来源请求

公开 GA 前应在正式下载位置同时提供：

- 最终 x64 与 ARM64 安装包。
- 每个安装包的 SHA-256。
- 第三方声明。
- GPL 对应源代码归档及其 SHA-256。
- 安装包与对应源代码归档的明确映射。

两个架构的对应源代码归档和 SHA-256 已生成。公开 GA 时仍必须把安装包、对应架构源码包和校验值同时上传；仅在本地生成而不公开提供不满足最终分发要求。

## 无担保

第三方软件按各自许可证提供。具体担保、责任限制和权利义务以随包许可证原文为准。
