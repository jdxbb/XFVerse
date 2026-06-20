#define AppName "XFVerse"
#define AppVersion GetEnv("XFVERSE_RELEASE_VERSION")
#define AppFileVersion GetEnv("XFVERSE_RELEASE_FILE_VERSION")
#define StagingRoot GetEnv("XFVERSE_RELEASE_STAGING")
#define OutputDir GetEnv("XFVERSE_RELEASE_OUTPUT_DIR")
#define AppIcon GetEnv("XFVERSE_APP_ICON")

[Setup]
AppId={{C0D20576-16FD-4539-94AA-FA7041348EEB}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=XFVerse
AppPublisherURL=https://xfverse.fun
AppSupportURL=https://xfverse.fun
AppUpdatesURL=https://xfverse.fun
VersionInfoVersion={#AppFileVersion}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\XFVerse
DefaultGroupName=XFVerse
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\MediaLibrary.App.exe
UninstallDisplayName={#AppName} {#AppVersion}
OutputDir={#OutputDir}
OutputBaseFilename=XFVerse-Setup-{#AppVersion}-win-x64
Compression=lzma2/max
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
SetupMutex=XFVerseReleaseSetupMutex

[Messages]
SetupAppTitle=安装程序
SetupWindowTitle=安装 - %1
UninstallAppTitle=卸载
UninstallAppFullTitle=卸载 %1
ErrorTitle=错误
ButtonBack=< 上一步
ButtonNext=下一步 >
ButtonInstall=安装
ButtonCancel=取消
ButtonFinish=完成
ButtonBrowse=浏览...
ButtonWizardBrowse=浏览...
ClickNext=单击“下一步”继续，或单击“取消”退出安装程序。
WelcomeLabel1=欢迎使用 [name] 安装向导
WelcomeLabel2=此向导将在当前用户下安装 [name/ver]。%n%n建议在继续之前关闭正在运行的 XFVerse。
WizardSelectDir=选择安装位置
SelectDirDesc=请选择 [name] 的安装位置。
SelectDirLabel3=安装程序将把 [name] 安装到以下文件夹。
SelectDirBrowseLabel=单击“下一步”继续；如需更改位置，请单击“浏览”。
WizardSelectTasks=选择附加任务
SelectTasksDesc=请选择需要执行的附加任务。
SelectTasksLabel2=选择安装 [name] 时需要执行的附加任务，然后单击“下一步”。
WizardReady=准备安装
ReadyLabel1=安装程序已准备好在当前用户下安装 [name]。
ReadyLabel2a=单击“安装”继续；如需检查或更改设置，请单击“上一步”。
ReadyLabel2b=单击“安装”继续。
ReadyMemoDir=安装位置：
ReadyMemoGroup=开始菜单文件夹：
ReadyMemoTasks=附加任务：
WizardPreparing=正在准备安装
PreparingDesc=安装程序正在准备安装 [name]。
WizardInstalling=正在安装
InstallingLabel=请稍候，安装程序正在安装 [name]。
FinishedHeadingLabel=[name] 安装完成
FinishedLabel=[name] 已安装完成，可以通过已创建的快捷方式启动。
StatusExtractFiles=正在解压文件...
StatusCreateIcons=正在创建快捷方式...
StatusCreateRegistryEntries=正在创建注册表项...
StatusSavingUninstall=正在保存卸载信息...
StatusRunProgram=正在完成安装...
ConfirmUninstall=确定要卸载 %1 吗？程序文件和快捷方式将被删除，XFVerse 软件数据将保留。
UninstallStatusLabel=请稍候，正在卸载 %1。
UninstalledAll=%1 已成功卸载，XFVerse 软件数据已保留。

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: unchecked

[Files]
Source: "{#StagingRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\XFVerse"; Filename: "{app}\MediaLibrary.App.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\XFVerse"; Filename: "{app}\MediaLibrary.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\MediaLibrary.App.exe"; Description: "启动 XFVerse"; Flags: nowait postinstall skipifsilent
