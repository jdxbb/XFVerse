param(
    [string] $Configuration = "Release",
    [string] $PackageVersion = "",
    [ValidateSet("win-x64", "win-arm64")]
    [string] $RuntimeIdentifier = "win-x64",
    [string] $InnoSetupPath = ""
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
        throw "Path is outside the allowed release root."
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

function Ensure-Directory([string] $path) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

function Get-ArchitectureInfo([string] $runtimeIdentifier) {
    switch ($runtimeIdentifier) {
        "win-x64" {
            return [pscustomobject]@{
                RuntimeIdentifier = "win-x64"
                PeMachine = 0x8664
                InnoArchitecture = "x64compatible"
                OppositeRuntimeIdentifier = "win-arm64"
            }
        }
        "win-arm64" {
            return [pscustomobject]@{
                RuntimeIdentifier = "win-arm64"
                PeMachine = 0xAA64
                InnoArchitecture = "arm64"
                OppositeRuntimeIdentifier = "win-x64"
            }
        }
        default {
            throw "Unsupported release runtime identifier: $runtimeIdentifier"
        }
    }
}

function Invoke-Checked([string] $filePath, [string[]] $arguments) {
    Write-Host "> $filePath $($arguments -join ' ')"
    & $filePath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE."
    }
}

function Invoke-InnoCompiler([string] $compilerPath, [string] $scriptPath) {
    Write-Host "> $compilerPath /Qp $scriptPath"
    $output = & $compilerPath "/Qp" $scriptPath 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) {
        throw "Inno Setup compilation failed with exit code $exitCode."
    }

    $versionLine = $output | Where-Object { $_ -match 'Compiler engine version:\s*(.+)$' } | Select-Object -First 1
    if ($versionLine -and $versionLine -match 'Compiler engine version:\s*(.+)$') {
        return $Matches[1].Trim()
    }

    $historyPath = Join-Path (Split-Path -Parent $compilerPath) "whatsnew.htm"
    if (Test-Path -LiteralPath $historyPath) {
        $history = Get-Content -LiteralPath $historyPath -Raw
        if ($history -match '<span class="ver">(\d+\.\d+\.\d+)') {
            return $Matches[1]
        }
    }

    return "6.x"
}

function Get-RelativePath([string] $basePath, [string] $path) {
    $baseUri = New-Object System.Uri(([System.IO.Path]::GetFullPath($basePath).TrimEnd('\') + '\'))
    $pathUri = New-Object System.Uri([System.IO.Path]::GetFullPath($path))
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

function Read-ReleaseVersion() {
    $versionFile = Resolve-RepoPath "Directory.Build.props"
    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw "Directory.Build.props was not found."
    }

    [xml] $properties = Get-Content -LiteralPath $versionFile -Raw
    $versionNode = $properties.SelectSingleNode("/Project/PropertyGroup/Version")
    $version = if ($versionNode) { [string] $versionNode.InnerText } else { [string]::Empty }
    if ([string]::IsNullOrWhiteSpace($version)) {
        $versionPrefixNode = $properties.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")
        $version = if ($versionPrefixNode) { [string] $versionPrefixNode.InnerText } else { [string]::Empty }
    }

    if ($version -notmatch '^\d+\.\d+\.\d+([\-+][0-9A-Za-z.-]+)?$') {
        throw "Release version is missing or invalid."
    }

    return $version
}

function Find-InnoCompiler() {
    if (-not [string]::IsNullOrWhiteSpace($InnoSetupPath)) {
        if (-not (Test-Path -LiteralPath $InnoSetupPath)) {
            throw "The specified Inno Setup compiler was not found."
        }

        return [System.IO.Path]::GetFullPath($InnoSetupPath)
    }

    $repoCompiler = Resolve-RepoPath ".tmp\packaging\inno\ISCC.exe"
    if (Test-Path -LiteralPath $repoCompiler) {
        return $repoCompiler
    }

    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "Inno Setup 6 was not found. Pass -InnoSetupPath explicitly."
}

function Get-LatestPackageDirectory([string] $packageId) {
    $nugetRoot = $env:NUGET_PACKAGES
    if ([string]::IsNullOrWhiteSpace($nugetRoot)) {
        $nugetRoot = Join-Path $env:USERPROFILE ".nuget\packages"
    }

    $packageRoot = Join-Path $nugetRoot $packageId.ToLowerInvariant()
    $directory = Get-ChildItem -LiteralPath $packageRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^8\.' } |
        Sort-Object { [version] $_.Name } -Descending |
        Select-Object -First 1
    if (-not $directory) {
        throw "Required NuGet package directory was not found: $packageId"
    }

    return $directory.FullName
}

function Remove-ReleaseNoise([string] $stagingDirectory, [object] $architecture) {
    $runtimeIdentifier = $architecture.RuntimeIdentifier
    $oppositeRuntimeIdentifier = $architecture.OppositeRuntimeIdentifier
    $paths = @(
        (Join-Path $stagingDirectory "mpv\$oppositeRuntimeIdentifier"),
        (Join-Path $stagingDirectory "tools\ffmpeg\$oppositeRuntimeIdentifier"),
        (Join-Path $stagingDirectory "mpv\$runtimeIdentifier\include"),
        (Join-Path $stagingDirectory "mpv\$runtimeIdentifier\libmpv.dll.a"),
        (Join-Path $stagingDirectory "mpv\$runtimeIdentifier\README.md"),
        (Join-Path $stagingDirectory "tools\ffmpeg\$runtimeIdentifier\README.md"),
        (Join-Path $stagingDirectory "tools\ffmpeg\$runtimeIdentifier\LICENSE.txt")
    )

    foreach ($path in $paths) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }

    Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File -Filter *.pdb |
        Remove-Item -Force
}

function Copy-OptimizedPersonaPosters([string] $stagingDirectory) {
    $sourceRoot = Resolve-RepoPath "src\MediaLibrary.App\Assets\WatchPersonas"
    if (-not (Test-Path -LiteralPath $sourceRoot)) {
        return [pscustomobject]@{ OptimizedCount = 0; BytesBefore = 0L; BytesAfter = 0L }
    }

    $destinationRoot = Join-Path $stagingDirectory "Assets\WatchPersonas"
    Ensure-Directory $destinationRoot
    Add-Type -AssemblyName System.Drawing
    $optimizedCount = 0
    $bytesBefore = 0L
    $bytesAfter = 0L
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter *.png) {
        $relativePath = Get-RelativePath $sourceRoot $file.FullName
        $destinationPath = Join-Path $destinationRoot $relativePath
        Ensure-Directory (Split-Path -Parent $destinationPath)
        $bytesBefore += $file.Length
        $image = [System.Drawing.Image]::FromFile($file.FullName)
        try {
            if ($image.Width -le 1086) {
                Copy-Item -LiteralPath $file.FullName -Destination $destinationPath -Force
                $bytesAfter += $file.Length
                continue
            }

            $targetWidth = 1086
            $targetHeight = [int] [Math]::Round($image.Height * $targetWidth / $image.Width)
            $bitmap = New-Object System.Drawing.Bitmap(
                $targetWidth,
                $targetHeight,
                [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $bitmap.SetResolution(96, 96)
                $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
                try {
                    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $graphics.DrawImage($image, 0, 0, $targetWidth, $targetHeight)
                }
                finally {
                    $graphics.Dispose()
                }

                $bitmap.Save($destinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally {
                $bitmap.Dispose()
            }
        }
        finally {
            $image.Dispose()
        }

        $optimizedCount++
        $bytesAfter += (Get-Item -LiteralPath $destinationPath).Length
    }

    return [pscustomobject]@{
        OptimizedCount = $optimizedCount
        BytesBefore = $bytesBefore
        BytesAfter = $bytesAfter
    }
}

function Copy-NativeReleaseFiles([string] $stagingDirectory, [string] $runtimeIdentifier) {
    $expectedHashes = switch ($runtimeIdentifier) {
        "win-x64" {
            @{
                LibMpv = "AE7B7B6B3ECCC7AEEFF3CBEF30FD175F97B27F1B815F89D0BD0CCA926C8C2D99"
                Ffprobe = "5ADF6B558DA8CB60F15B67D6FF1E0B2B0EDCFEBAA1F47106A66F20506B43E769"
            }
        }
        "win-arm64" {
            @{
                LibMpv = "37C731D685A164CBD3BA544EA0C0E8D9DDD02C12B615DEB33917EED60FC0CFB4"
                Ffprobe = "DA87820CAC84552A59B192EB0C99B5388068A387028060BA580A7D460B04C2CC"
            }
        }
    }
    $files = @(
        [pscustomobject]@{
            Source = Resolve-RepoPath "native\mpv\$runtimeIdentifier\libmpv-2.dll"
            Destination = Join-Path $stagingDirectory "mpv\$runtimeIdentifier\libmpv-2.dll"
            Sha256 = $expectedHashes.LibMpv
        },
        [pscustomobject]@{
            Source = Resolve-RepoPath "tools\ffmpeg\$runtimeIdentifier\ffprobe.exe"
            Destination = Join-Path $stagingDirectory "tools\ffmpeg\$runtimeIdentifier\ffprobe.exe"
            Sha256 = $expectedHashes.Ffprobe
        }
    )

    foreach ($file in $files) {
        if (-not (Test-Path -LiteralPath $file.Source)) {
            throw "Required native release file was not found."
        }
        $actualHash = (Get-FileHash -LiteralPath $file.Source -Algorithm SHA256).Hash
        if ($actualHash -ne $file.Sha256) {
            throw "Required native release file hash does not match the audited input."
        }
        Ensure-Directory (Split-Path -Parent $file.Destination)
        Copy-Item -LiteralPath $file.Source -Destination $file.Destination -Force
    }
}

function Copy-LicenseBundle(
    [string] $stagingDirectory,
    [string] $isccPath,
    [string] $runtimeIdentifier) {
    $licenseRoot = Join-Path $stagingDirectory "licenses"
    Ensure-Directory $licenseRoot

    Copy-Item -LiteralPath (Resolve-RepoPath "docs\third-party\XFVERSE_1_0_THIRD_PARTY_INVENTORY.md") -Destination $licenseRoot
    Copy-Item -LiteralPath (Resolve-RepoPath "docs\third-party\THIRD_PARTY_NOTICES.md") -Destination (Join-Path $licenseRoot "THIRD-PARTY-NOTICES.txt")
    Copy-Item -LiteralPath (Resolve-RepoPath "docs\third-party\CORRESPONDING_SOURCE.md") -Destination (Join-Path $licenseRoot "CORRESPONDING-SOURCE.txt")
    $sourceChecksums = Resolve-RepoPath "artifacts\release\$PackageVersion\source\SHA256SUMS.txt"
    if (-not (Test-Path -LiteralPath $sourceChecksums)) {
        throw "Corresponding-source package checksums were not found. Run Build-CorrespondingSource.ps1 first."
    }
    Copy-Item -LiteralPath $sourceChecksums -Destination (Join-Path $licenseRoot "CORRESPONDING-SOURCE-SHA256.txt")
    Copy-Item -LiteralPath (Resolve-RepoPath "docs\third-party\SMARTDATE_NOTICE.md") -Destination $licenseRoot
    Copy-Item -LiteralPath (Resolve-RepoPath "docs\third-party\PHOSPHOR_ICONS_NOTICE.md") -Destination $licenseRoot
    Copy-Item -LiteralPath (Resolve-RepoPath "docs\third-party\licenses\APACHE-2.0.txt") -Destination $licenseRoot

    $gplSource = Resolve-RepoPath "tools\ffmpeg\win-arm64\LICENSE.txt"
    if (-not (Test-Path -LiteralPath $gplSource)) {
        throw "The GNU GPL v3 license text required by the native media tools was not found."
    }
    Copy-Item -LiteralPath $gplSource -Destination (Join-Path $licenseRoot "GPL-3.0.txt")

    $runtimePackage = Get-LatestPackageDirectory "microsoft.netcore.app.runtime.$runtimeIdentifier"
    Copy-Item -LiteralPath (Join-Path $runtimePackage "LICENSE.TXT") -Destination (Join-Path $licenseRoot "Microsoft-DotNet-LICENSE.txt")
    Copy-Item -LiteralPath (Join-Path $runtimePackage "THIRD-PARTY-NOTICES.TXT") -Destination (Join-Path $licenseRoot "Microsoft-DotNet-THIRD-PARTY-NOTICES.txt")

    $desktopPackage = Get-LatestPackageDirectory "microsoft.windowsdesktop.app.runtime.$runtimeIdentifier"
    Copy-Item -LiteralPath (Join-Path $desktopPackage "LICENSE") -Destination (Join-Path $licenseRoot "Microsoft-WindowsDesktop-LICENSE.txt")

    $innoLicense = Join-Path (Split-Path -Parent $isccPath) "license.txt"
    if (-not (Test-Path -LiteralPath $innoLicense)) {
        throw "The Inno Setup license file was not found beside the compiler."
    }
    Copy-Item -LiteralPath $innoLicense -Destination (Join-Path $licenseRoot "Inno-Setup-LICENSE.txt")
}

function Get-PeMachine([string] $path) {
    $stream = [System.IO.File]::Open($path, 'Open', 'Read', 'ReadWrite')
    try {
        $reader = New-Object System.IO.BinaryReader($stream)
        try {
            $stream.Seek(0x3C, [System.IO.SeekOrigin]::Begin) | Out-Null
            $peOffset = $reader.ReadInt32()
            $stream.Seek($peOffset + 4, [System.IO.SeekOrigin]::Begin) | Out-Null
            return $reader.ReadUInt16()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Test-StagingContent([string] $stagingDirectory, [object] $architecture) {
    $runtimeIdentifier = $architecture.RuntimeIdentifier
    $oppositeRuntimeIdentifier = $architecture.OppositeRuntimeIdentifier
    $issues = New-Object System.Collections.Generic.List[string]
    $files = Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File
    $prohibitedExtensions = @('.db', '.sqlite', '.sqlite3', '.pdb', '.log', '.bak')
    foreach ($file in $files) {
        $relativePath = Get-RelativePath $stagingDirectory $file.FullName
        $hasProhibitedExtension = $prohibitedExtensions -contains $file.Extension.ToLowerInvariant()
        $hasProhibitedDirectory = $relativePath -match '(?i)(^|\\)(seed-data|test-data|cache|logs)(\\|$)'
        $hasSqliteSidecar = $relativePath -match '(?i)\.db-(wal|shm)$'
        $hasWrongArchitectureOrDevelopmentFile =
            $relativePath -match "(?i)$([regex]::Escape($oppositeRuntimeIdentifier))|\\include\\|\.dll\.a$"
        if ($hasProhibitedExtension -or $hasProhibitedDirectory -or $hasSqliteSidecar -or $hasWrongArchitectureOrDevelopmentFile) {
            $issues.Add("prohibited-file: $relativePath")
        }
    }

    $textExtensions = @('.json', '.config', '.txt', '.md', '.xml')
    foreach ($file in $files | Where-Object { $textExtensions -contains $_.Extension.ToLowerInvariant() }) {
        $content = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($content -match 'C:\\Users\\') {
            $issues.Add("private-path-text: $(Get-RelativePath $stagingDirectory $file.FullName)")
        }
        if ($content -match '(?i)(password|token|api[_-]?key|authorization)\s*[:=]\s*(?!<redacted>|empty|\(none\))[^\s"'';]+') {
            $issues.Add("possible-secret-text: $(Get-RelativePath $stagingDirectory $file.FullName)")
        }
    }

    foreach ($file in $files | Where-Object { $_.Extension -in @('.exe', '.dll') }) {
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
        if ($ascii.Contains('C:\Users\')) {
            $issues.Add("private-path-binary: $(Get-RelativePath $stagingDirectory $file.FullName)")
        }
    }

    $requiredFiles = @(
        "MediaLibrary.App.exe",
        "mpv\$runtimeIdentifier\libmpv-2.dll",
        "tools\ffmpeg\$runtimeIdentifier\ffprobe.exe",
        "licenses\XFVERSE_1_0_THIRD_PARTY_INVENTORY.md",
        "licenses\THIRD-PARTY-NOTICES.txt",
        "licenses\CORRESPONDING-SOURCE.txt",
        "licenses\CORRESPONDING-SOURCE-SHA256.txt",
        "licenses\GPL-3.0.txt"
    )
    foreach ($relativePath in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagingDirectory $relativePath))) {
            $issues.Add("missing-required-file: $relativePath")
        }
    }

    foreach ($relativePath in @(
        "MediaLibrary.App.exe",
        "mpv\$runtimeIdentifier\libmpv-2.dll",
        "tools\ffmpeg\$runtimeIdentifier\ffprobe.exe")) {
        $path = Join-Path $stagingDirectory $relativePath
        if ((Test-Path -LiteralPath $path) -and (Get-PeMachine $path) -ne $architecture.PeMachine) {
            $issues.Add("wrong-architecture: $relativePath")
        }
    }

    return $issues
}

function Write-ReleaseReports(
    [string] $stagingDirectory,
    [string] $reportsDirectory,
    [string] $installerPath,
    [object] $personaResult,
    [string] $isccPath,
    [string] $innoVersion,
    [object] $architecture) {
    $runtimeIdentifier = $architecture.RuntimeIdentifier
    $files = Get-ChildItem -LiteralPath $stagingDirectory -Recurse -File
    $inventory = foreach ($file in $files | Sort-Object FullName) {
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        "{0}`t{1}`t{2}" -f (Get-RelativePath $stagingDirectory $file.FullName), $file.Length, $hash
    }
    Set-Content -LiteralPath (Join-Path $reportsDirectory "file-inventory.txt") -Value $inventory -Encoding UTF8

    $topFiles = $files | Sort-Object Length -Descending | Select-Object -First 30 |
        ForEach-Object {
            [ordered]@{
                path = Get-RelativePath $stagingDirectory $_.FullName
                bytes = $_.Length
            }
        }
    $sizeReport = [ordered]@{
        totalBytes = ($files | Measure-Object Length -Sum).Sum
        fileCount = $files.Count
        personaImages = [ordered]@{
            optimizedCount = $personaResult.OptimizedCount
            bytesBefore = $personaResult.BytesBefore
            bytesAfter = $personaResult.BytesAfter
        }
        topFiles = $topFiles
    }
    $sizeReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reportsDirectory "size-report.json") -Encoding UTF8

    @(
        "status=PASS",
        "database-files=0",
        "log-files=0",
        "pdb-files=0",
        "architecture=$runtimeIdentifier",
        "opposite-architecture-files=0",
        "private-path-matches=0",
        "possible-secret-matches=0"
    ) | Set-Content -LiteralPath (Join-Path $reportsDirectory "sensitive-scan-report.txt") -Encoding UTF8

    Copy-Item -LiteralPath (Resolve-RepoPath "docs\third-party\XFVERSE_1_0_THIRD_PARTY_INVENTORY.md") -Destination (Join-Path $reportsDirectory "third-party-inventory.md")

    $installerHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    $hashLines = @(
        "$installerHash  $(Split-Path -Leaf $installerPath)"
    )
    foreach ($relativePath in @(
        "MediaLibrary.App.exe",
        "mpv\$runtimeIdentifier\libmpv-2.dll",
        "tools\ffmpeg\$runtimeIdentifier\ffprobe.exe")) {
        $path = Join-Path $stagingDirectory $relativePath
        $hashLines += "$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)  $relativePath"
    }
    Set-Content -LiteralPath (Join-Path $reportsDirectory "sha256.txt") -Value $hashLines -Encoding ASCII

    $manifest = [ordered]@{
        product = "XFVerse"
        version = $PackageVersion
        architecture = $runtimeIdentifier
        configuration = $Configuration
        selfContained = $true
        publishTrimmed = $false
        publishSingleFile = $false
        publishReadyToRun = $false
        rawPublishRetained = $false
        installer = [ordered]@{
            fileName = Split-Path -Leaf $installerPath
            bytes = (Get-Item -LiteralPath $installerPath).Length
            sha256 = $installerHash
        }
        staging = [ordered]@{
            fileCount = $files.Count
            totalBytes = ($files | Measure-Object Length -Sum).Sum
        }
        toolchain = [ordered]@{
            dotnetSdk = (& dotnet --version)
            innoCompilerVersion = $innoVersion
            innoCompilerSha256 = (Get-FileHash -LiteralPath $isccPath -Algorithm SHA256).Hash
        }
        generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reportsDirectory "release-manifest.json") -Encoding UTF8
}

$script:RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
Set-Location $script:RepoRoot

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = Read-ReleaseVersion
}

$architecture = Get-ArchitectureInfo $RuntimeIdentifier
$releaseBase = Resolve-RepoPath "artifacts\release"
$releaseRoot = Join-Path $releaseBase $PackageVersion
$architectureRoot = Join-Path $releaseRoot $RuntimeIdentifier
$publishDirectory = Join-Path $architectureRoot "publish"
$stagingDirectory = Join-Path $architectureRoot "staging"
$reportsDirectory = Join-Path $architectureRoot "reports"
$outputDirectory = Join-Path $architectureRoot "output"
$installerName = "XFVerse-Setup-$PackageVersion-$RuntimeIdentifier.exe"
$installerPath = Join-Path $outputDirectory $installerName
$appProject = Resolve-RepoPath "src\MediaLibrary.App\MediaLibrary.App.csproj"
$installerScript = Resolve-RepoPath "scripts\packaging\XFVerse.ReleaseInstaller.iss"
$appIcon = Resolve-RepoPath "src\MediaLibrary.App\Assets\app-logo.ico"
$iscc = Find-InnoCompiler

Ensure-Directory $releaseBase
Ensure-Directory $releaseRoot
Reset-Directory $architectureRoot $releaseRoot
Ensure-Directory $publishDirectory
Ensure-Directory $stagingDirectory
Ensure-Directory $reportsDirectory
Ensure-Directory $outputDirectory

Invoke-Checked "dotnet" @(
    "publish",
    $appProject,
    "--configuration", $Configuration,
    "--runtime", $RuntimeIdentifier,
    "--self-contained", "true",
    "--output", $publishDirectory,
    "/p:PublishTrimmed=false",
    "/p:PublishSingleFile=false",
    "/p:PublishReadyToRun=false",
    "/p:DebugType=None",
    "/p:DebugSymbols=false",
    "/p:XFVerseReleasePackaging=true"
)

Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $stagingDirectory -Recurse -Force
Remove-ReleaseNoise $stagingDirectory $architecture
Copy-NativeReleaseFiles $stagingDirectory $RuntimeIdentifier
$personaResult = Copy-OptimizedPersonaPosters $stagingDirectory
Copy-LicenseBundle $stagingDirectory $iscc $RuntimeIdentifier

$issues = @(Test-StagingContent $stagingDirectory $architecture)
if ($issues.Count -gt 0) {
    $issues | Set-Content -LiteralPath (Join-Path $reportsDirectory "sensitive-scan-report.txt") -Encoding UTF8
    throw "Release staging validation failed. See the sensitive scan report."
}

$env:XFVERSE_RELEASE_VERSION = $PackageVersion
[xml] $releaseProperties = Get-Content -LiteralPath (Resolve-RepoPath "Directory.Build.props") -Raw
$fileVersionNode = $releaseProperties.SelectSingleNode("/Project/PropertyGroup/FileVersion")
$env:XFVERSE_RELEASE_FILE_VERSION = if ($fileVersionNode) { [string] $fileVersionNode.InnerText } else { "$PackageVersion.0" }
$env:XFVERSE_RELEASE_STAGING = $stagingDirectory
$env:XFVERSE_RELEASE_OUTPUT_DIR = $outputDirectory
$env:XFVERSE_APP_ICON = $appIcon
$env:XFVERSE_RELEASE_ARCHITECTURE = $RuntimeIdentifier
$env:XFVERSE_INNO_ARCHITECTURE = $architecture.InnoArchitecture
$env:XFVERSE_OPPOSITE_RUNTIME = $architecture.OppositeRuntimeIdentifier

$innoVersion = Invoke-InnoCompiler $iscc $installerScript
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "The expected installer output was not produced."
}

Write-ReleaseReports `
    $stagingDirectory `
    $reportsDirectory `
    $installerPath `
    $personaResult `
    $iscc `
    $innoVersion `
    $architecture

# The raw publish copy is an intermediate input. The validated staging tree is retained.
if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath (Assert-UnderRoot $publishDirectory $releaseRoot) -Recurse -Force
}
$publishRoot = Split-Path -Parent $publishDirectory
if ((Test-Path -LiteralPath $publishRoot) -and -not (Get-ChildItem -LiteralPath $publishRoot -Force)) {
    Remove-Item -LiteralPath (Assert-UnderRoot $publishRoot $releaseRoot) -Force
}

Write-Host ""
Write-Host "Release installer complete."
Write-Host "Version: $PackageVersion"
Write-Host "Architecture: $RuntimeIdentifier"
Write-Host "Installer: $installerName"
Write-Host "Installer SHA-256: $((Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash)"
