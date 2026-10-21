#define AppName "轻录"
#define AppVersion "0.3.0"

[Setup]
SourceDir=..
AppId={{1D095F09-C56B-4897-ACB6-2A10CA566D6F}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=轻录
AppVerName=轻录 {#AppVersion}
DefaultDirName={localappdata}\Programs\LightRecorder
DefaultGroupName=轻录
DisableProgramGroupPage=yes
OutputDir=release
OutputBaseFilename=轻录-Win10-x64-安装包
SetupIconFile=assets\app.ico
UninstallDisplayIcon={app}\轻录.exe
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
PrivilegesRequired=lowest
AppMutex=LightRecorder_482d64a1
CloseApplications=no
RestartApplications=no
UninstallDisplayName=轻录
VersionInfoVersion=0.3.0.0
VersionInfoProductName=轻录
VersionInfoDescription=轻录 Windows 10 安装程序
DiskSpanning=no
DisableWelcomePage=no

[Languages]
Name: "chinesesimp"; MessagesFile: "assets\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："

[Files]
Source: "dist\轻录.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\轻录.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\ScreenRecorderLib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\ScreenRecorderLib-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "dist\使用说明.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "packaging\installed.flag"; DestDir: "{app}"; Flags: ignoreversion
Source: "vendor\vc_redist.x64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\轻录"; Filename: "{app}\轻录.exe"; WorkingDir: "{app}"; AppUserModelID: "LightRecorder.Desktop"
Name: "{autodesktop}\轻录"; Filename: "{app}\轻录.exe"; WorkingDir: "{app}"; Tasks: desktopicon; AppUserModelID: "LightRecorder.Desktop"

[Run]
Filename: "{app}\轻录.exe"; Description: "启动轻录"; Flags: nowait postinstall skipifsilent runasoriginaluser; Check: CanLaunch

[Code]
var
  RuntimeNeedsRestart: Boolean;

function CanLaunch: Boolean;
begin
  Result := not RuntimeNeedsRestart;
end;

function InitializeSetup: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    SuppressibleMsgBox('此版本需要 Windows 10 2004 或更新版本自带的 .NET Framework 4.8。当前系统组件缺失或未启用，请修复系统组件后再安装。', mbError, MB_OK, IDOK);
end;

function RuntimeReady: Boolean;
var
  MS, LS: Cardinal;
begin
  Result := FileExists(ExpandConstant('{sys}\msvcp140.dll')) and
    FileExists(ExpandConstant('{sys}\concrt140.dll')) and
    FileExists(ExpandConstant('{sys}\vcruntime140_1.dll')) and
    GetVersionNumbers(ExpandConstant('{sys}\vcruntime140.dll'), MS, LS);
  if Result then
    Result := (MS > ((14 shl 16) or 44)) or
      ((MS = ((14 shl 16) or 44)) and ((LS shr 16) >= 35211));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  if not RuntimeReady then begin
    WizardForm.StatusLabel.Caption := '正在安装录制所需的 Microsoft C++ 运行库…';
    ExtractTemporaryFile('vc_redist.x64.exe');
    if not ShellExec('runas', ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, Code) then begin
      Result := '无法启动 Microsoft C++ 运行库安装程序：' + SysErrorMessage(Code);
      exit;
    end;
    if (Code <> 0) and (Code <> 3010) and (Code <> 1638) then begin
      Result := 'Microsoft C++ 运行库安装失败，错误代码：' + IntToStr(Code) + '。请重试安装。';
      exit;
    end;
    if Code = 3010 then begin RuntimeNeedsRestart := True; NeedsRestart := True; end;
    if (not RuntimeReady) and (not RuntimeNeedsRestart) then begin
      Result := 'Microsoft C++ 运行库尚未就绪，请重启 Windows 后重新运行安装程序。';
      exit;
    end;
  end else Log('C++ runtime already satisfies minimum version; no installation required.');

  if not FileExists(ExpandConstant('{sys}\mfplat.dll')) then begin
    WizardForm.StatusLabel.Caption := '正在为 Windows 安装媒体组件（需要联网）…';
    if not ShellExec('runas', ExpandConstant('{sysnative}\dism.exe'), '/Online /Add-Capability /CapabilityName:Media.MediaFeaturePack~~~~0.0.1.0 /NoRestart', '', SW_HIDE, ewWaitUntilTerminated, Code) then begin
      Result := '无法安装 Windows 媒体组件：' + SysErrorMessage(Code);
      exit;
    end;
    if Code = 3010 then begin RuntimeNeedsRestart := True; NeedsRestart := True; end;
    if not FileExists(ExpandConstant('{sys}\mfplat.dll')) then
      Result := 'Windows 媒体组件未就绪。N/KN 版本需连接 Windows Update 安装媒体功能包，必要时重启后重试。错误代码：' + IntToStr(Code);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  Report: String;
  Contents: AnsiString;
begin
  if (CurStep = ssPostInstall) and (not RuntimeNeedsRestart) then begin
    WizardForm.StatusLabel.Caption := '正在检查录制组件与首次启动…';
    Report := ExpandConstant('{tmp}\LightRecorder-readiness.txt');
    if not Exec(ExpandConstant('{app}\轻录.exe'), '--health-check "' + Report + '"', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException('轻录启动检查失败：' + SysErrorMessage(Code));
    if Code <> 0 then begin
      LoadStringFromFile(Report, Contents);
      Log(Contents);
      RaiseException('轻录启动检查未通过，安装未完成。请重试，或查看安装日志中的依赖检查结果。');
    end;
    LoadStringFromFile(Report, Contents);
    Log(Contents);
  end;
end;
