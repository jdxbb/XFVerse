# XFVerse 1.0 对应源代码状态

Last updated: 2026-06-21

## 状态

当前状态：**对应源代码交付已建立，P8-B05 可关闭。**

本文记录 XFVerse 1.0 候选安装包所携带 GPL 原生组件的对象代码、固定来源、源码包和公开分发要求。

本文不是法律意见。XFVerse 1.0 分别为 x64 与 ARM64 建立对应源代码包，并在安装包内提供本说明和源码包 SHA-256 清单。公开分发时必须把对应架构源码包与安装包放在同一下载位置。

## 对应源代码包

| 架构 | 源代码包 | 大小 | SHA-256 |
| --- | --- | --- |
| x64 | `XFVerse-1.0.0-corresponding-source-win-x64.zip` | 41,353,342 bytes | `295DD94628A74B0D85890882B14EC4A2F1D42015AF31D28A3E8018F84941D8E9` |
| ARM64 | `XFVerse-1.0.0-corresponding-source-win-arm64.zip` | 41,353,339 bytes | `7836F690311094DC3CDE280A53D09A4A0C7C1A68C417E3F8BC9ED5ACDE7883C6` |

安装包内的 `licenses/CORRESPONDING-SOURCE-SHA256.txt` 保存同一校验清单。源码包由 `scripts/packaging/Build-CorrespondingSource.ps1` 从固定 URL 和固定 SHA-256 输入生成。

## 为什么上游首页链接不够

GNU GPLv3 对“对应源代码”的定义包括生成、安装和运行对象代码所需的源代码，以及控制这些活动的脚本。

当对象代码通过网络下载提供时，应在分发位置提供等价的对应源代码访问，或在对象代码旁清楚指向相同获取能力。

官方文本：

- GNU GPLv3：https://www.gnu.org/licenses/gpl-3.0.html
- 第 1 节 Source Code。
- 第 6 节 Conveying Non-Source Forms。

因此，仅链接 mpv 或 FFmpeg 项目首页不能替代：

- 精确源代码版本。
- 构建脚本和构建配置。
- 应用的补丁。
- 静态或紧密组合依赖的源代码。
- 能够确认来源与当前二进制匹配的证据。

## 对象代码清单

### libmpv

| 架构 | 对象文件 | 报告版本 | SHA-256 |
| --- | --- | --- | --- |
| x64 | `mpv/win-x64/libmpv-2.dll` | `v0.41.0-514-g06f4ce75a` | `AE7B7B6B3ECCC7AEEFF3CBEF30FD175F97B27F1B815F89D0BD0CCA926C8C2D99` |
| ARM64 | `mpv/win-arm64/libmpv-2.dll` | `v0.41.0-514-g06f4ce75a` | `37C731D685A164CBD3BA544EA0C0E8D9DDD02C12B615DEB33917EED60FC0CFB4` |

已验证的上游提交：

- https://github.com/mpv-player/mpv/commit/06f4ce75a

已验证的构建来源：

- 构建项目：https://github.com/shinchiro/mpv-winbuild-cmake
- 原始发布：https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260419

已验证信息：

- 两个 DLL 报告同一 mpv 提交版本。
- 两个 DLL 内嵌的构建路径包含 `mpv-winbuild-cmake`，与上述构建项目一致。
- 20260419 发布同时提供 x86_64 与 aarch64 构建，报告的 mpv 版本与两个 DLL 一致。
- 文件属性声明 mpv 按 GNU GPL version 2 or later 分发。
- XFVerse 未修改这两个 DLL。

对应源代码安排：

- 归档 mpv 提交 `06f4ce75aaf161c5589387aeba39d34cd42eb648`。
- 归档 libmpv 构建中使用的 FFmpeg 提交 `d538a71ad52404662d986ec9921b6bc53d353e7f`。
- 归档 `mpv-winbuild-cmake` 提交 `ec6f81cd420b1fb80a682fd58b30b6ad61aa114b`。
- 构建系统归档保留依赖源位置、配置、补丁和构建步骤。
- 源码包内 `SOURCE-MANIFEST.json` 和 `SHA256SUMS.txt` 建立对象代码映射与文件校验。

结论：两个架构的 libmpv 已映射到固定源码和构建系统快照。

### ffprobe

| 架构 | 对象文件 | 报告版本 | SHA-256 |
| --- | --- | --- | --- |
| x64 | `tools/ffmpeg/win-x64/ffprobe.exe` | `n8.1.2-20260620` | `5ADF6B558DA8CB60F15B67D6FF1E0B2B0EDCFEBAA1F47106A66F20506B43E769` |
| ARM64 | `tools/ffmpeg/win-arm64/ffprobe.exe` | `n8.1.2-20260620` | `DA87820CAC84552A59B192EB0C99B5388068A387028060BA580A7D460B04C2CC` |

已验证来源：

- 构建项目：https://github.com/BtbN/FFmpeg-Builds
- 固定发布：https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-06-20-13-30
- 构建项目提交：`bfcf840002eb1cf68ed626657db2d250cf62e8a2`
- FFmpeg 8.1.2 提交：`38b88335f99e76ed89ff3c93f877fdefce736c13`

原始发布包：

| 架构 | 发布包 | SHA-256 |
| --- | --- | --- |
| x64 | `ffmpeg-n8.1.2-win64-gpl-8.1.zip` | `48D45E97F1EF6EDBC94AF7F561F2748FF42ACE5339FEB9B439DBE5020F5DEFA7` |
| ARM64 | `ffmpeg-n8.1.2-winarm64-gpl-8.1.zip` | `D0B6D82BA55F09C6EDC3C9E9F236443B4C765320EB28344C014397048E1CF56C` |

已验证信息：

- 两个发布包均通过 BtbN 同一发布附带的 `checksums.sha256` 验证。
- x64 对象 PE Machine 为 `8664`，ARM64 对象为 `AA64`。
- 两个对象均可执行 `-version`，报告 FFmpeg 8.1.2。
- 两个对象配置均包含 `--enable-gpl --enable-version3`。
- XFVerse 只从发布包提取 `ffprobe.exe`，未修改二进制。

对应源代码安排：

- 归档 FFmpeg 8.1.2 固定提交源码。
- 归档 BtbN 固定构建项目提交，包含依赖声明、配置和补丁。
- 保存原始发布校验文件。
- 源码包内为每个架构记录原始发布包、对象哈希和源码映射。

结论：原 ARM64 来源不明对象和原 Gyan x64 对象均已替换，两种架构现在使用同一可追溯构建来源。

## 已完成的闭环

1. 固定 libmpv、FFmpeg 和两套构建系统的源码快照。
2. 替换 x64 与 ARM64 ffprobe 为同一 BtbN 固定发布。
3. 为两个架构分别生成源码包和 SHA-256。
4. 安装包内包含 GPL 文本、第三方声明、对应源代码说明和源码包校验清单。
5. 正式打包脚本要求源码包校验清单存在，否则拒绝构建。
6. 安装生命周期脚本验证上述合规文件实际安装。

## 最终分发要求

Phase 8.9 公开 GA 时：

1. 安装包与对应源代码归档同时上传。
2. 下载页面在每个安装包旁列出对应源代码链接和 SHA-256。
3. `THIRD_PARTY_NOTICES.md` 指向实际可下载归档。
4. 安装包内保留 GPL 文本、第三方声明和本状态文档的最终版。
5. 对应源代码访问不得要求额外付费、特殊密码或私有账号。
6. 对象代码可下载期间，应保持对应源代码入口可用。

如果无法满足这些条件，不发布包含相应 GPL 对象代码的 GA 安装包。

## 当前可核对的官方资料

- GNU GPLv3：https://www.gnu.org/licenses/gpl-3.0.html
- FFmpeg 法律与合规说明：https://ffmpeg.org/legal.html
- FFmpeg 源码：https://ffmpeg.org/download.html#get-sources
- mpv 源码：https://github.com/mpv-player/mpv
- mpv 版权和许可证：https://github.com/mpv-player/mpv/blob/master/Copyright
- mpv-winbuild-cmake 构建项目：https://github.com/shinchiro/mpv-winbuild-cmake
- 本次 libmpv 原始发布：https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260419
- BtbN FFmpeg 构建项目：https://github.com/BtbN/FFmpeg-Builds
- 本次 ffprobe 原始发布：https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-06-20-13-30
