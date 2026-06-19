#define AppName "XFVerse"
#define AppVersion GetEnv("XFVERSE_PACKAGE_VERSION")
#define RepoRoot GetEnv("XFVERSE_REPO_ROOT")
#define PublishX64 GetEnv("XFVERSE_PUBLISH_X64")
#define SeedDataRoot GetEnv("XFVERSE_SEED_DATA_ROOT")
#define OutputDir GetEnv("XFVERSE_OUTPUT_DIR")
#define AppIcon GetEnv("XFVERSE_APP_ICON")

[Setup]
AppId={{4B84A41B-15B5-4DB4-8B93-704BD1439F25}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=XFVerse
DefaultDirName={localappdata}\Programs\XFVerse
DefaultGroupName=XFVerse
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\MediaLibrary.App.exe
OutputDir={#OutputDir}
OutputBaseFilename=XFVerse-TestSetup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes

[Messages]
WelcomeLabel1=Welcome to the XFVerse x64 test setup
WelcomeLabel2=This installer includes the app, player runtime, ffprobe, and a test data snapshot. It overwrites the current user's local XFVerse data.
SelectDirDesc=Choose the folder where XFVerse test build will be installed.
FinishedHeadingLabel=XFVerse test build has been installed
FinishedLabel=Setup has finished installing XFVerse. The app can be used directly without downloading extra runtime or player components.

[InstallDelete]
Type: filesandordirs; Name: "{localappdata}\MediaLibrary"
Type: filesandordirs; Name: "{app}"

[Files]
Source: "{#PublishX64}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SeedDataRoot}\*"; DestDir: "{localappdata}\MediaLibrary"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\scripts\packaging\CreateDesktopShortcut.ps1"; DestDir: "{app}\tools"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\XFVerse"; Filename: "{app}\MediaLibrary.App.exe"; WorkingDir: "{app}"

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\tools\CreateDesktopShortcut.ps1"" -TargetPath ""{app}\MediaLibrary.App.exe"" -WorkingDirectory ""{app}"" -ShortcutName ""XFVerse"""; Description: "Create a desktop shortcut"; Flags: postinstall runhidden
