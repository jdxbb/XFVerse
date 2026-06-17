param(
    [Parameter(Mandatory = $true)]
    [string] $TargetPath,

    [Parameter(Mandatory = $true)]
    [string] $WorkingDirectory,

    [string] $ShortcutName = "XFVerse"
)

$ErrorActionPreference = "Stop"

$desktop = [Environment]::GetFolderPath("DesktopDirectory")
$shortcutPath = Join-Path $desktop "$ShortcutName.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $TargetPath
$shortcut.WorkingDirectory = $WorkingDirectory
$shortcut.IconLocation = "$TargetPath,0"
$shortcut.Save()
