param(
    [string] $Configuration = "Release",
    [string] $PackageVersion = (Get-Date -Format "yyyy.MM.dd.HHmm"),
    [string] $SourceAppData = (Join-Path $env:LOCALAPPDATA "MediaLibrary"),
    [string] $PythonPath = "C:\Users\32184\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe",
    [string] $InnoSetupDownloadUrl = "https://jrsoftware.org/download.php/is.exe",
    [ValidateSet("win-x64", "win-arm64")]
    [string[]] $RuntimeIdentifiers = @("win-x64", "win-arm64"),
    [switch] $PreserveProfileCache
)

$ErrorActionPreference = "Stop"

function Resolve-RepoPath([string] $relativePath) {
    return [System.IO.Path]::GetFullPath((Join-Path $script:RepoRoot $relativePath))
}

function Assert-UnderRepo([string] $path) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($script:RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside repo root: $fullPath"
    }

    return $fullPath
}

function Reset-Directory([string] $path) {
    $fullPath = Assert-UnderRepo $path
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Ensure-Directory([string] $path) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

function Copy-SeedUserData([string] $source, [string] $destination) {
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Source app data directory does not exist."
    }

    Ensure-Directory $destination
    $sourceRoot = [System.IO.Path]::GetFullPath($source).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $excludedRootDirectories = @("VideoCache", "backups", "logs")
    $excludedFilePatterns = @(
        "media-library*.db",
        "media-library*.db-shm",
        "media-library*.db-wal",
        "*.log",
        "*.bak"
    )

    Get-ChildItem -LiteralPath $source -Force -Recurse -File | ForEach-Object {
        $filePath = [System.IO.Path]::GetFullPath($_.FullName)
        $relativePath = $filePath.Substring($sourceRoot.Length)
        $segments = $relativePath -split '[\\/]'
        if ($segments.Length -gt 0 -and ($excludedRootDirectories -contains $segments[0])) {
            return
        }

        foreach ($pattern in $excludedFilePatterns) {
            if ($_.Name -like $pattern) {
                return
            }
        }

        $targetPath = Join-Path $destination $relativePath
        $targetDirectory = Split-Path -Parent $targetPath
        Ensure-Directory $targetDirectory
        Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Force
    }
}

function Find-Iscc([string] $toolsRoot) {
    $localIscc = Join-Path $toolsRoot "ISCC.exe"
    if (Test-Path -LiteralPath $localIscc) {
        return $localIscc
    }

    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    return $null
}

function Ensure-InnoSetup([string] $toolsRoot, [string] $downloadUrl) {
    Ensure-Directory $toolsRoot
    $iscc = Find-Iscc $toolsRoot
    if ($iscc) {
        return $iscc
    }

    $downloadRoot = Resolve-RepoPath ".tmp\packaging\downloads"
    Ensure-Directory $downloadRoot
    $installerPath = Join-Path $downloadRoot "innosetup.exe"

    Write-Host "Downloading Inno Setup compiler..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath

    Write-Host "Installing Inno Setup compiler into repo temp directory..."
    $arguments = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/NOICONS",
        "/DIR=$toolsRoot"
    )
    $process = Start-Process -FilePath $installerPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "Inno Setup installer failed with exit code $($process.ExitCode)."
    }

    $iscc = Find-Iscc $toolsRoot
    if (-not $iscc) {
        throw "ISCC.exe was not found after installing Inno Setup."
    }

    return $iscc
}

function Invoke-Checked([string] $filePath, [string[]] $arguments) {
    Write-Host "> $filePath $($arguments -join ' ')"
    & $filePath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE."
    }
}

function Prune-PublishDirectory([string] $publishDirectory, [string] $rid) {
    $otherRid = if ($rid -eq "win-arm64") { "win-x64" } else { "win-arm64" }
    $pathsToRemove = @(
        (Join-Path $publishDirectory "mpv\$otherRid"),
        (Join-Path $publishDirectory "tools\ffmpeg\$otherRid")
    )

    foreach ($path in $pathsToRemove) {
        if (Test-Path -LiteralPath $path) {
            $fullPath = Assert-UnderRepo $path
            Remove-Item -LiteralPath $fullPath -Recurse -Force
        }
    }

    Get-ChildItem -LiteralPath $publishDirectory -Recurse -Force -File -Filter *.pdb -ErrorAction SilentlyContinue |
        ForEach-Object {
            $fullPath = Assert-UnderRepo $_.FullName
            Remove-Item -LiteralPath $fullPath -Force
        }
}

$script:RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
Set-Location $script:RepoRoot

$packageRoot = Resolve-RepoPath "artifacts\test-installer"
$publishRoot = Join-Path $packageRoot "publish"
$publishX64 = Join-Path $publishRoot "win-x64"
$publishArm64 = Join-Path $publishRoot "win-arm64"
$publishByRid = @{
    "win-x64" = $publishX64
    "win-arm64" = $publishArm64
}
$seedDataRoot = Join-Path $packageRoot "seed-data\MediaLibrary"
$reportsRoot = Join-Path $packageRoot "reports"
$toolRoot = Resolve-RepoPath ".tmp\packaging\inno"
$appLogoSvg = Resolve-RepoPath "logo.svg"
$appIcon = Resolve-RepoPath "src\MediaLibrary.App\Assets\app-logo.ico"
$appIconPng = Resolve-RepoPath "src\MediaLibrary.App\Assets\app-logo.png"
$sourceDatabase = Join-Path $SourceAppData "media-library.db"
$seedDatabase = Join-Path $seedDataRoot "media-library.db"
$seedReport = Join-Path $reportsRoot "package-seed-report.json"
$dualArchitectureIssPath = Resolve-RepoPath "scripts\packaging\XFVerse.TestInstaller.iss"
$x64InstallerIssPath = Resolve-RepoPath "scripts\packaging\XFVerse.TestInstaller.x64.iss"
$installerOutput = Resolve-RepoPath "XFVerse-TestSetup-$PackageVersion.exe"

if (-not (Test-Path -LiteralPath $sourceDatabase)) {
    throw "Source database was not found in the current user app data directory."
}

$RuntimeIdentifiers = @($RuntimeIdentifiers | Select-Object -Unique)
$isSingleX64Package = $RuntimeIdentifiers.Count -eq 1 -and $RuntimeIdentifiers[0] -eq "win-x64"
$isDualArchitecturePackage = $RuntimeIdentifiers.Count -eq 2 `
    -and ($RuntimeIdentifiers -contains "win-x64") `
    -and ($RuntimeIdentifiers -contains "win-arm64")

if (-not $isSingleX64Package -and -not $isDualArchitecturePackage) {
    throw "This test installer script currently supports either win-x64 only or win-x64 + win-arm64."
}

$issPath = if ($isSingleX64Package) { $x64InstallerIssPath } else { $dualArchitectureIssPath }

Reset-Directory $packageRoot
foreach ($rid in $RuntimeIdentifiers) {
    Ensure-Directory $publishByRid[$rid]
}
Ensure-Directory $seedDataRoot
Ensure-Directory $reportsRoot

if (-not (Test-Path -LiteralPath $PythonPath)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if (-not $pythonCommand) {
        throw "Python was not found. Pass -PythonPath to a Python runtime with Pillow available."
    }

    $PythonPath = $pythonCommand.Source
}

Invoke-Checked $PythonPath @(
    (Resolve-RepoPath "scripts\packaging\generate_app_icon.py"),
    $appLogoSvg,
    $appIcon,
    $appIconPng
)

Write-Host "Copying app data snapshot, excluding VideoCache, backups, logs, and old database files..."
Copy-SeedUserData -source $SourceAppData -destination $seedDataRoot

Write-Host "Creating seeded package database copy..."
$seedArguments = @(
    "run",
    "--project",
    (Resolve-RepoPath "src\MediaLibrary.Tools\MediaLibrary.Tools.csproj"),
    "-c",
    $Configuration,
    "--",
    "package-test-data",
    "--source-db",
    $sourceDatabase,
    "--target-db",
    $seedDatabase,
    "--report",
    $seedReport
)

if ($PreserveProfileCache) {
    $seedArguments += "--preserve-profile-cache"
}

Invoke-Checked "dotnet" $seedArguments

Write-Host "Building solution..."
Invoke-Checked "dotnet" @("build", (Resolve-RepoPath "MediaLibrary.sln"), "-c", $Configuration)

foreach ($rid in $RuntimeIdentifiers) {
    $publishDirectory = $publishByRid[$rid]
    Write-Host "Publishing $rid self-contained app..."
    Invoke-Checked "dotnet" @(
        "publish",
        (Resolve-RepoPath "src\MediaLibrary.App\MediaLibrary.App.csproj"),
        "-c",
        $Configuration,
        "-r",
        $rid,
        "--self-contained",
        "true",
        "-p:PublishSingleFile=false",
        "-p:PublishReadyToRun=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-o",
        $publishDirectory
    )
    Prune-PublishDirectory -publishDirectory $publishDirectory -rid $rid
}

$requiredFiles = @($seedDatabase)
foreach ($rid in $RuntimeIdentifiers) {
    $publishDirectory = $publishByRid[$rid]
    $requiredFiles += @(
        (Join-Path $publishDirectory "MediaLibrary.App.exe"),
        (Join-Path $publishDirectory "mpv\$rid\libmpv-2.dll"),
        (Join-Path $publishDirectory "tools\ffmpeg\$rid\ffprobe.exe")
    )
}

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Required package file is missing: $requiredFile"
    }
}

$iscc = Ensure-InnoSetup -toolsRoot $toolRoot -downloadUrl $InnoSetupDownloadUrl

$env:XFVERSE_PACKAGE_VERSION = $PackageVersion
$env:XFVERSE_REPO_ROOT = $script:RepoRoot
$env:XFVERSE_PUBLISH_X64 = $publishX64
$env:XFVERSE_PUBLISH_ARM64 = $publishArm64
$env:XFVERSE_SEED_DATA_ROOT = $seedDataRoot
$env:XFVERSE_OUTPUT_DIR = $script:RepoRoot
$env:XFVERSE_APP_ICON = $appIcon

if (Test-Path -LiteralPath $installerOutput) {
    Remove-Item -LiteralPath $installerOutput -Force
}

Write-Host "Compiling installer..."
Invoke-Checked $iscc @($issPath)

if (-not (Test-Path -LiteralPath $installerOutput)) {
    throw "Installer output was not produced: $installerOutput"
}

$installerInfo = Get-Item -LiteralPath $installerOutput
Write-Host "Installer ready: $($installerInfo.FullName)"
Write-Host "Installer bytes: $($installerInfo.Length)"
Write-Host "Seed report: $seedReport"
