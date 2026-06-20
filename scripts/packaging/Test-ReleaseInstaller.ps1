param(
    [string] $PackageVersion = "",
    [string] $InstallerPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepoPath([string] $relativePath) {
    return [System.IO.Path]::GetFullPath((Join-Path $script:RepoRoot $relativePath))
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

function Start-InstallerProcess([string] $filePath, [string[]] $arguments) {
    $process = Start-Process `
        -FilePath $filePath `
        -ArgumentList $arguments `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
    return $process.ExitCode
}

$script:RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
Set-Location $script:RepoRoot

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = Read-ReleaseVersion
}
if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $InstallerPath = Resolve-RepoPath "artifacts\release\$PackageVersion\output\XFVerse-Setup-$PackageVersion-win-x64.exe"
}
if (-not (Test-Path -LiteralPath $InstallerPath)) {
    throw "The release installer was not found."
}

$stagingDirectory = Resolve-RepoPath "artifacts\release\$PackageVersion\staging"
$reportsDirectory = Resolve-RepoPath "artifacts\release\$PackageVersion\reports"
$testRoot = Resolve-RepoPath ".tmp\phase8-release-installer-test"
$installDirectory = Join-Path $testRoot "program"
$tmpRoot = (Resolve-RepoPath ".tmp").TrimEnd('\') + '\'
$resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot).TrimEnd('\') + '\'
if (-not $resolvedTestRoot.StartsWith($tmpRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer test directory escaped the workspace temp root."
}

if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

$userDataDirectory = Join-Path $env:LOCALAPPDATA "MediaLibrary"
$userDataExistedBefore = Test-Path -LiteralPath $userDataDirectory
$userDataTimestampBefore = if ($userDataExistedBefore) {
    (Get-Item -LiteralPath $userDataDirectory).LastWriteTimeUtc
}
else {
    $null
}

$installerArguments = @(
    "/VERYSILENT",
    "/SUPPRESSMSGBOXES",
    "/NORESTART",
    "/NOICONS",
    "/TASKS=`"`"",
    "/DIR=`"$installDirectory`""
)

$firstInstallExitCode = Start-InstallerProcess $InstallerPath $installerArguments
if ($firstInstallExitCode -ne 0) {
    throw "First installation failed with exit code $firstInstallExitCode."
}
Start-Sleep -Seconds 2

$requiredFiles = @(
    "MediaLibrary.App.exe",
    "mpv\win-x64\libmpv-2.dll",
    "tools\ffmpeg\win-x64\ffprobe.exe",
    "licenses\XFVERSE_1_0_THIRD_PARTY_INVENTORY.md"
)
$missingFiles = @(
    $requiredFiles | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $installDirectory $_))
    }
)
if ($missingFiles.Count -gt 0) {
    throw "Installed files are missing: $($missingFiles -join ', ')"
}

$stagedAppHash = (Get-FileHash -LiteralPath (Join-Path $stagingDirectory "MediaLibrary.App.exe") -Algorithm SHA256).Hash
$installedAppHash = (Get-FileHash -LiteralPath (Join-Path $installDirectory "MediaLibrary.App.exe") -Algorithm SHA256).Hash
if ($stagedAppHash -ne $installedAppHash) {
    throw "Installed application hash does not match staging."
}

$repairExitCode = Start-InstallerProcess $InstallerPath $installerArguments
if ($repairExitCode -ne 0) {
    throw "Same-version repair installation failed with exit code $repairExitCode."
}
Start-Sleep -Seconds 2

$uninstaller = Get-ChildItem -LiteralPath $installDirectory -Filter "unins*.exe" | Select-Object -First 1
if (-not $uninstaller) {
    throw "The uninstaller was not created."
}

$uninstallExitCode = Start-InstallerProcess `
    $uninstaller.FullName `
    @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART")
if ($uninstallExitCode -ne 0) {
    throw "Uninstall failed with exit code $uninstallExitCode."
}
Start-Sleep -Seconds 1

$userDataExistsAfter = Test-Path -LiteralPath $userDataDirectory
$userDataTimestampAfter = if ($userDataExistsAfter) {
    (Get-Item -LiteralPath $userDataDirectory).LastWriteTimeUtc
}
else {
    $null
}

$report = [ordered]@{
    status = "PASS"
    version = $PackageVersion
    architecture = "win-x64"
    firstInstallExitCode = $firstInstallExitCode
    repairExitCode = $repairExitCode
    uninstallExitCode = $uninstallExitCode
    installedAppHashMatched = $true
    programDirectoryRemoved = -not (Test-Path -LiteralPath $installDirectory)
    userDataExistenceUnchanged = $userDataExistedBefore -eq $userDataExistsAfter
    userDataTimestampUnchanged = $userDataTimestampBefore -eq $userDataTimestampAfter
    applicationLaunchTested = $false
    databaseInitializationExecuted = $false
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
}

$reportPath = Join-Path $reportsDirectory "installer-lifecycle-report.json"
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding UTF8

if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}

Write-Host "RELEASE_INSTALLER_LIFECYCLE=PASS"
Write-Host "Report: installer-lifecycle-report.json"
