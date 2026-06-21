# XFVerse 1.0 Third-Party Inventory

Last updated: 2026-06-21

## Distribution scope

This inventory covers the separate XFVerse 1.0 Windows x64 and Windows ARM64
self-contained installers. The release pipeline copies this document and the
applicable license texts to the installed `licenses` directory.

## Runtime and libraries

| Component | Version | Source | License | Distribution note |
| --- | --- | --- | --- | --- |
| .NET Runtime | 8.0.26 | https://github.com/dotnet/runtime | MIT and bundled third-party notices | Architecture-matched self-contained x64 or ARM64 runtime; Microsoft license and notices are included. |
| WPF / Windows Desktop Runtime | 8.0.26 | https://github.com/dotnet/wpf | MIT | Architecture-matched self-contained x64 or ARM64 desktop runtime; Microsoft license is included. |
| Entity Framework Core | 8.0.11 | https://github.com/dotnet/efcore | MIT | Included through application publish output. |
| Microsoft.Data.Sqlite | 8.0.11 | https://github.com/dotnet/efcore | MIT | Included through EF Core SQLite provider. |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | https://github.com/dotnet/runtime | MIT | Included through application publish output. |
| System.Security.Cryptography.ProtectedData | 8.0.0 | https://github.com/dotnet/runtime | MIT | Provides Windows DPAPI access. |
| SQLitePCLRaw | 2.1.6 | https://github.com/ericsink/SQLitePCL.raw | Apache-2.0 | Apache 2.0 license text is included. |
| SQLite amalgamation | supplied by SQLitePCLRaw 2.1.6 | https://www.sqlite.org/ | Public Domain | Native e_sqlite3 runtime included by SQLitePCLRaw. |

## Native media tools

### mpv / libmpv

- Distributed files:
  - x64: `mpv/win-x64/libmpv-2.dll`.
  - ARM64: `mpv/win-arm64/libmpv-2.dll`.
- File version: `v0.41.0-514-g06f4ce75a`.
- SHA-256 of audited inputs:
  - x64: `AE7B7B6B3ECCC7AEEFF3CBEF30FD175F97B27F1B815F89D0BD0CCA926C8C2D99`.
  - ARM64: `37C731D685A164CBD3BA544EA0C0E8D9DDD02C12B615DEB33917EED60FC0CFB4`.
- Upstream source: https://github.com/mpv-player/mpv
- Source revision indicated by the binary: `06f4ce75a`.
- Build project: https://github.com/shinchiro/mpv-winbuild-cmake
- Original binary release:
  https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20260419
- Upstream copyright and license information:
  https://github.com/mpv-player/mpv/blob/master/Copyright
- Distribution treatment: GPL-3.0-or-later-compatible terms are applied
  conservatively because the supplied Windows binary is a combined native
  distribution and does not carry a standalone license manifest.
- Modifications: XFVerse does not modify the binary.

The release package includes the GNU GPL version 3 text. Embedded build paths,
architectures and reported versions match the 20260419 mpv-winbuild-cmake
release. Fixed mpv, embedded FFmpeg and build-system snapshots are included in
the architecture-matched corresponding-source packages.

### FFmpeg / ffprobe

- Distributed files:
  - x64: `tools/ffmpeg/win-x64/ffprobe.exe`.
  - ARM64: `tools/ffmpeg/win-arm64/ffprobe.exe`.
- Versions:
  - x64: `n8.1.2-20260620`.
  - ARM64: `n8.1.2-20260620`.
- SHA-256 of audited inputs:
  - x64: `5ADF6B558DA8CB60F15B67D6FF1E0B2B0EDCFEBAA1F47106A66F20506B43E769`.
  - ARM64: `DA87820CAC84552A59B192EB0C99B5388068A387028060BA580A7D460B04C2CC`.
- Upstream source: https://ffmpeg.org/
- Build project: https://github.com/BtbN/FFmpeg-Builds
- Fixed release:
  https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-06-20-13-30
- FFmpeg source revision: `38b88335f99e76ed89ff3c93f877fdefce736c13`.
- License: GPL-3.0-or-later.
- Evidence: the binary reports `--enable-gpl --enable-version3`.
- Modifications: XFVerse does not modify the binary.

Each architecture package includes the GNU GPL version 3 text and records its
exact binary version, configuration and hash in the generated reports.

The previous Gyan x64 binary and source-unproven ARM64 binary were replaced.
Both current binaries come from one fixed BtbN release, pass the release
checksum file, report FFmpeg 8.1.2 and map to fixed source/build snapshots.

## UI and adapted components

| Component | Source | License | Notice |
| --- | --- | --- | --- |
| SmartDate-inspired date picker | https://github.com/JamesnetGroup/smartdate | MIT | `SMARTDATE_NOTICE.md` |
| Phosphor Icons | https://github.com/phosphor-icons/core | MIT | `PHOSPHOR_ICONS_NOTICE.md` |

## Packaging tool

| Component | Version | Source | License | Distribution note |
| --- | --- | --- | --- | --- |
| Inno Setup | 6.x local compiler | https://jrsoftware.org/isinfo.php | Inno Setup License | Installer bootstrap/uninstaller technology; license text is copied into the installed license bundle. |

## Release obligations

- Keep this inventory with the exact release version.
- Include the referenced MIT, Apache-2.0, GPL-3.0 and Inno Setup texts.
- Include Microsoft .NET third-party notices.
- Do not replace native binaries without updating version, hash, source and
  license evidence.
- Provide corresponding source or equivalent source access required by the
  applicable copyleft licenses when distributing the installer publicly.
- Do not describe third-party components as authored by XFVerse.
- Keep `THIRD-PARTY-NOTICES.txt`, `CORRESPONDING-SOURCE.txt` and
  `CORRESPONDING-SOURCE-SHA256.txt` in each installer license bundle.
- Publish the matching corresponding-source archive beside each installer.
