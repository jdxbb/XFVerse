#define AppName "XFVerse"
#define AppVersion GetEnv("XFVERSE_PACKAGE_VERSION")
#define RepoRoot GetEnv("XFVERSE_REPO_ROOT")
#define PublishX64 GetEnv("XFVERSE_PUBLISH_X64")
#define PublishArm64 GetEnv("XFVERSE_PUBLISH_ARM64")
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
ArchitecturesAllowed=x64 arm64
ArchitecturesInstallIn64BitMode=x64 arm64
WizardStyle=modern
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\MediaLibrary.App.exe
OutputDir={#OutputDir}
OutputBaseFilename=XFVerse-TestSetup-{#AppVersion}
Compression=none
SolidCompression=no
SetupLogging=yes
CloseApplications=yes

[Messages]
WelcomeLabel1=欢迎安装 XFVerse 测试版
WelcomeLabel2=本安装包包含应用程序、播放器内核、ffprobe、当前测试数据库和本机用户配置快照。安装过程中会覆盖当前用户的 XFVerse 本地数据。
SelectDirDesc=请选择 XFVerse 测试版的安装文件夹。
FinishedHeadingLabel=XFVerse 测试版安装完成
FinishedLabel=安装已完成。可以直接启动应用，无需额外下载运行时或播放器组件。

[InstallDelete]
Type: filesandordirs; Name: "{localappdata}\MediaLibrary"
Type: filesandordirs; Name: "{app}"

[Files]
Source: "{#PublishX64}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: not IsArm64
Source: "{#PublishArm64}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsArm64
Source: "{#SeedDataRoot}\*"; DestDir: "{localappdata}\MediaLibrary"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\scripts\packaging\CreateDesktopShortcut.ps1"; DestDir: "{app}\tools"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\XFVerse"; Filename: "{app}\MediaLibrary.App.exe"; WorkingDir: "{app}"

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\tools\CreateDesktopShortcut.ps1"" -TargetPath ""{app}\MediaLibrary.App.exe"" -WorkingDirectory ""{app}"" -ShortcutName ""XFVerse"""; Description: "创建桌面快捷方式"; Flags: postinstall runhidden

[Code]
function InitializeSetup(): Boolean;
begin
  if not (IsX64 or IsArm64) then
  begin
    MsgBox('XFVerse 测试版目前仅支持 Windows x64 或 Windows ARM64。', mbError, MB_OK);
    Result := False;
    exit;
  end;

  Result := True;
end;
