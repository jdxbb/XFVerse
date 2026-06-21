param(
    [string] $PackageVersion = "",
    [string] $CacheDirectory = "",
    [string] $OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepoPath([string] $relativePath) {
    return [System.IO.Path]::GetFullPath((Join-Path $script:RepoRoot $relativePath))
}

function Assert-UnderRoot([string] $path, [string] $root) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    $fullRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $candidate = $fullPath.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the allowed source-package root."
    }

    return $fullPath
}

function Reset-Directory([string] $path, [string] $root) {
    $fullPath = Assert-UnderRoot $path $root
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Get-RelativePath([string] $basePath, [string] $path) {
    $baseUri = New-Object System.Uri(([System.IO.Path]::GetFullPath($basePath).TrimEnd('\') + '\'))
    $pathUri = New-Object System.Uri([System.IO.Path]::GetFullPath($path))
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('\', '/')
}

function Read-ReleaseVersion() {
    [xml] $properties = Get-Content -LiteralPath (Resolve-RepoPath "Directory.Build.props") -Raw
    $node = $properties.SelectSingleNode("/Project/PropertyGroup/Version")
    if (-not $node) {
        $node = $properties.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")
    }
    if (-not $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw "The repository release version was not found."
    }

    return [string] $node.InnerText
}

function Get-SourceEntries() {
    return @(
        [pscustomobject]@{
            FileName = "mpv-06f4ce75aaf161c5589387aeba39d34cd42eb648.tar.gz"
            Category = "upstream"
            Url = "https://codeload.github.com/mpv-player/mpv/tar.gz/06f4ce75aaf161c5589387aeba39d34cd42eb648"
            Sha256 = "3AAF2D73233B431F44490C7ACD8C7A258BAA8DEC565A4C890FED1604E57D5EF8"
        },
        [pscustomobject]@{
            FileName = "FFmpeg-d538a71ad52404662d986ec9921b6bc53d353e7f.tar.gz"
            Category = "upstream"
            Url = "https://codeload.github.com/FFmpeg/FFmpeg/tar.gz/d538a71ad52404662d986ec9921b6bc53d353e7f"
            Sha256 = "D84FA2E1ACE9671753087C823E66620144532B4150023D013CFD22AD8F5853C9"
        },
        [pscustomobject]@{
            FileName = "FFmpeg-n8.1.2-38b88335f99e76ed89ff3c93f877fdefce736c13.tar.gz"
            Category = "upstream"
            Url = "https://codeload.github.com/FFmpeg/FFmpeg/tar.gz/38b88335f99e76ed89ff3c93f877fdefce736c13"
            Sha256 = "2AE7E42343CFFFB811D15CFE98B6D005F082595FCDF034D30A4FF90CFED9F9C6"
        },
        [pscustomobject]@{
            FileName = "mpv-winbuild-cmake-ec6f81cd420b1fb80a682fd58b30b6ad61aa114b.zip"
            Category = "build-systems"
            Url = "https://codeload.github.com/shinchiro/mpv-winbuild-cmake/zip/ec6f81cd420b1fb80a682fd58b30b6ad61aa114b"
            Sha256 = "FFBFD4C825921B6C7368BA99B1169CED25DA9F10550F03828823729546715BF3"
        },
        [pscustomobject]@{
            FileName = "FFmpeg-Builds-bfcf840002eb1cf68ed626657db2d250cf62e8a2.zip"
            Category = "build-systems"
            Url = "https://codeload.github.com/BtbN/FFmpeg-Builds/zip/bfcf840002eb1cf68ed626657db2d250cf62e8a2"
            Sha256 = "D44D5EED94EDC7F751C927FEFEAA131756F88FB53596C79DD156068DAB4A03FC"
        },
        [pscustomobject]@{
            FileName = "BtbN-autobuild-2026-06-20-13-30-checksums.sha256"
            Category = "release-evidence"
            Url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-06-20-13-30/checksums.sha256"
            Sha256 = "5F1D76B3F2D936CFE06F7D36548C2D048CA2B5535AAEF7BF5D76057EDF3D3A93"
        }
    )
}

function Ensure-SourceFile([object] $entry, [string] $cacheRoot) {
    $path = Join-Path $cacheRoot $entry.FileName
    if (Test-Path -LiteralPath $path) {
        $existingHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($existingHash -eq $entry.Sha256) {
            return $path
        }

        Remove-Item -LiteralPath $path -Force
    }

    Write-Host "Downloading source input: $($entry.FileName)"
    Invoke-WebRequest `
        -UseBasicParsing `
        -Uri $entry.Url `
        -OutFile $path `
        -Headers @{ "User-Agent" = "XFVerse-corresponding-source" }

    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actualHash -ne $entry.Sha256) {
        Remove-Item -LiteralPath $path -Force
        throw "Source input hash verification failed: $($entry.FileName)"
    }

    return $path
}

function Get-BinaryMapping([string] $runtimeIdentifier) {
    if ($runtimeIdentifier -eq "win-x64") {
        return [ordered]@{
            libmpvSha256 = "AE7B7B6B3ECCC7AEEFF3CBEF30FD175F97B27F1B815F89D0BD0CCA926C8C2D99"
            ffprobeSha256 = "5ADF6B558DA8CB60F15B67D6FF1E0B2B0EDCFEBAA1F47106A66F20506B43E769"
            ffprobeReleaseArchive = "ffmpeg-n8.1.2-win64-gpl-8.1.zip"
            ffprobeReleaseArchiveSha256 = "48D45E97F1EF6EDBC94AF7F561F2748FF42ACE5339FEB9B439DBE5020F5DEFA7"
        }
    }

    return [ordered]@{
        libmpvSha256 = "37C731D685A164CBD3BA544EA0C0E8D9DDD02C12B615DEB33917EED60FC0CFB4"
        ffprobeSha256 = "DA87820CAC84552A59B192EB0C99B5388068A387028060BA580A7D460B04C2CC"
        ffprobeReleaseArchive = "ffmpeg-n8.1.2-winarm64-gpl-8.1.zip"
        ffprobeReleaseArchiveSha256 = "D0B6D82BA55F09C6EDC3C9E9F236443B4C765320EB28344C014397048E1CF56C"
    }
}

function Write-BundleDocumentation(
    [string] $bundleRoot,
    [string] $runtimeIdentifier,
    [object] $mapping,
    [object[]] $sourceEntries) {
    $readme = @"
# XFVerse $PackageVersion corresponding source - $runtimeIdentifier

This bundle maps the GPL native media objects distributed with the XFVerse
$PackageVersion $runtimeIdentifier installer to fixed upstream source snapshots,
the build-system snapshots, patches and release checksum evidence.

## Object mapping

- libmpv-2.dll SHA-256: $($mapping.libmpvSha256)
- libmpv version: v0.41.0-514-g06f4ce75a
- libmpv build release: shinchiro/mpv-winbuild-cmake 20260419
- ffprobe.exe SHA-256: $($mapping.ffprobeSha256)
- ffprobe version: n8.1.2-20260620
- ffprobe build release: BtbN/FFmpeg-Builds autobuild-2026-06-20-13-30
- ffprobe release archive: $($mapping.ffprobeReleaseArchive)
- ffprobe release archive SHA-256: $($mapping.ffprobeReleaseArchiveSha256)

## Bundle layout

- upstream/: exact mpv and FFmpeg source snapshots.
- build-systems/: fixed build recipes, dependency declarations and patches.
- release-evidence/: upstream release checksum evidence.
- licenses/: applicable GPL text and XFVerse third-party notices.
- SOURCE-MANIFEST.json: machine-readable object and source mapping.
- SHA256SUMS.txt: hashes for every file in this bundle except itself.

The build-system archives retain the dependency source locations, revision
selectors, configuration and patches used by their respective build projects.
XFVerse does not modify the distributed libmpv or ffprobe binaries.
"@
    Set-Content -LiteralPath (Join-Path $bundleRoot "README.md") -Value $readme -Encoding UTF8

    $manifest = [ordered]@{
        product = "XFVerse"
        version = $PackageVersion
        architecture = $runtimeIdentifier
        objects = @(
            [ordered]@{
                path = "mpv/$runtimeIdentifier/libmpv-2.dll"
                sha256 = $mapping.libmpvSha256
                source = @(
                    "upstream/mpv-06f4ce75aaf161c5589387aeba39d34cd42eb648.tar.gz",
                    "upstream/FFmpeg-d538a71ad52404662d986ec9921b6bc53d353e7f.tar.gz",
                    "build-systems/mpv-winbuild-cmake-ec6f81cd420b1fb80a682fd58b30b6ad61aa114b.zip"
                )
            },
            [ordered]@{
                path = "tools/ffmpeg/$runtimeIdentifier/ffprobe.exe"
                sha256 = $mapping.ffprobeSha256
                source = @(
                    "upstream/FFmpeg-n8.1.2-38b88335f99e76ed89ff3c93f877fdefce736c13.tar.gz",
                    "build-systems/FFmpeg-Builds-bfcf840002eb1cf68ed626657db2d250cf62e8a2.zip",
                    "release-evidence/BtbN-autobuild-2026-06-20-13-30-checksums.sha256"
                )
            }
        )
        sourceInputs = @(
            $sourceEntries | ForEach-Object {
                [ordered]@{
                    file = "$($_.Category)/$($_.FileName)"
                    sha256 = $_.Sha256
                    url = $_.Url
                }
            }
        )
    }
    $manifest | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $bundleRoot "SOURCE-MANIFEST.json") -Encoding UTF8
}

$script:RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
Set-Location $script:RepoRoot

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = Read-ReleaseVersion
}
if ([string]::IsNullOrWhiteSpace($CacheDirectory)) {
    $CacheDirectory = Resolve-RepoPath ".tmp\corresponding-source-cache"
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Resolve-RepoPath "artifacts\release\$PackageVersion\source"
}

$cacheRoot = [System.IO.Path]::GetFullPath($CacheDirectory)
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$buildBase = Resolve-RepoPath ".tmp\corresponding-source-build"
New-Item -ItemType Directory -Path $cacheRoot, $outputRoot, $buildBase -Force | Out-Null
Reset-Directory $outputRoot (Resolve-RepoPath "artifacts\release")
Reset-Directory $buildBase (Resolve-RepoPath ".tmp")

$sourceEntries = @(Get-SourceEntries)
$resolvedSources = @{}
foreach ($entry in $sourceEntries) {
    $resolvedSources[$entry.FileName] = Ensure-SourceFile $entry $cacheRoot
}

$archives = @()
foreach ($runtimeIdentifier in @("win-x64", "win-arm64")) {
    $mapping = Get-BinaryMapping $runtimeIdentifier
    $bundleName = "XFVerse-$PackageVersion-corresponding-source-$runtimeIdentifier"
    $bundleRoot = Join-Path $buildBase $bundleName
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null

    foreach ($category in @("upstream", "build-systems", "release-evidence", "licenses")) {
        New-Item -ItemType Directory -Path (Join-Path $bundleRoot $category) -Force | Out-Null
    }
    foreach ($entry in $sourceEntries) {
        Copy-Item `
            -LiteralPath $resolvedSources[$entry.FileName] `
            -Destination (Join-Path (Join-Path $bundleRoot $entry.Category) $entry.FileName)
    }

    Copy-Item -LiteralPath (Resolve-RepoPath "tools\ffmpeg\win-arm64\LICENSE.txt") `
        -Destination (Join-Path $bundleRoot "licenses\GPL-3.0.txt")

    Write-BundleDocumentation $bundleRoot $runtimeIdentifier $mapping $sourceEntries

    $hashLines = Get-ChildItem -LiteralPath $bundleRoot -Recurse -File |
        Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
        Sort-Object FullName |
        ForEach-Object {
            $relative = Get-RelativePath $bundleRoot $_.FullName
            "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $relative"
        }
    Set-Content -LiteralPath (Join-Path $bundleRoot "SHA256SUMS.txt") -Value $hashLines -Encoding ASCII

    $archivePath = Join-Path $outputRoot "$bundleName.zip"
    Compress-Archive -Path (Join-Path $bundleRoot "*") -DestinationPath $archivePath -CompressionLevel Optimal
    $archives += Get-Item -LiteralPath $archivePath
}

$archiveHashes = $archives | ForEach-Object {
    "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $($_.Name)"
}
Set-Content -LiteralPath (Join-Path $outputRoot "SHA256SUMS.txt") -Value $archiveHashes -Encoding ASCII

$archives | ForEach-Object {
    Write-Host "$($_.Name) $($_.Length) $((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"
}
