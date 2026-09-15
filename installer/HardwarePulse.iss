[Setup]
AppId={{75E8FDDA-D799-4D8A-882D-972DC72151C2}
AppName=Hardware Pulse
AppVersion=0.6.5
AppPublisher=Marck Wong
AppPublisherURL=https://github.com/medking82
AppSupportURL=https://github.com/medking82/hardware-pulse/issues
DefaultDirName={autopf}\Hardware Pulse
DisableDirPage=yes
DefaultGroupName=Hardware Pulse
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir=..\dist
OutputBaseFilename=HardwarePulse-0.6.5-Setup
SetupIconFile=..\assets\pulse.ico
UninstallDisplayIcon={app}\HardwarePulse.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=auto

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zhCN"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "zhTW"; MessagesFile: "languages\ChineseTraditional.isl"

[CustomMessages]
en.LaunchPulse=Launch Hardware Pulse
zhCN.LaunchPulse=启动 Hardware Pulse
zhTW.LaunchPulse=啟動 Hardware Pulse

[Files]
Source: "..\build\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\vendor\PawnIO-2.2.0.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[InstallDelete]
; Exact obsolete app-owned files. Preserve settings, Windows components and shared drivers.
Type: files; Name: "{app}\CardVisibility.ps1"
Type: files; Name: "{app}\Collector.ps1"
Type: files; Name: "{app}\Density.ps1"
Type: files; Name: "{app}\DeviceProfile.ps1"
Type: files; Name: "{app}\Glass.ps1"
Type: files; Name: "{app}\Icons.ps1"
Type: files; Name: "{app}\Install-Startup.ps1"
Type: files; Name: "{app}\Localization.ps1"
Type: files; Name: "{app}\Overlay.ps1"
Type: files; Name: "{app}\Paths.ps1"
Type: files; Name: "{app}\Preferences.ps1"
Type: files; Name: "{app}\Remove-Startup.ps1"
Type: files; Name: "{app}\Sensors.ps1"
Type: files; Name: "{app}\Set-Startup.ps1"
Type: files; Name: "{app}\Test-Sensors.ps1"
Type: files; Name: "{app}\Typography.ps1"
Type: files; Name: "{app}\CardDrag.cs"
Type: files; Name: "{app}\FrameCapture.cs"
Type: files; Name: "{app}\GameOverlay.cs"
Type: files; Name: "{app}\UpdateCheck.cs"
Type: files; Name: "{app}\WidgetHost.cs"
Type: files; Name: "{app}\WindowSnap.cs"
Type: files; Name: "{app}\PulseUpgrade.exe"

[Icons]
Name: "{group}\Hardware Pulse"; Filename: "{app}\HardwarePulse.exe"

[Run]
Filename: "{app}\HardwarePulse.exe"; Description: "{cm:LaunchPulse}"; Flags: postinstall nowait skipifsilent runasoriginaluser; Check: not IsPulseUpdate
Filename: "{app}\HardwarePulse.exe"; Flags: nowait runasoriginaluser; Check: IsPulseUpdate

[UninstallRun]
Filename: "{app}\HardwarePulse.exe"; Parameters: "--remove-startup"; Flags: runhidden waituntilterminated; RunOnceId: "RemovePulseStartup"

[Code]
function LocalText(English, Simplified, Traditional: String): String;
begin
  if ActiveLanguage = 'zhCN' then Result := Simplified
  else if ActiveLanguage = 'zhTW' then Result := Traditional
  else Result := English;
end;
function IsPulseUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:PULSEUPDATE|0}') = '1';
end;
function GetCurrentProcessId(): Cardinal;
  external 'GetCurrentProcessId@kernel32.dll stdcall';

function CollectorRunning(Service: Variant; ExpectedPath: String): Boolean;
var Processes, Process: Variant;
    I: Integer;
    CommandLine: String;
begin
  Result := False;
  Processes := Service.ExecQuery('SELECT ExecutablePath, CommandLine FROM Win32_Process WHERE Name = ''HardwarePulse.exe''');
  for I := 0 to Processes.Count - 1 do begin
    Process := Processes.ItemIndex(I);
    if not VarIsNull(Process.ExecutablePath) then
      if CompareText(Process.ExecutablePath, ExpectedPath) = 0 then begin
        if VarIsNull(Process.CommandLine) then
          RaiseException(LocalText('Cannot inspect the previous Hardware Pulse session.','无法检查先前的 Hardware Pulse 会话。','無法檢查先前的 Hardware Pulse 工作階段。'));
        CommandLine := Process.CommandLine;
        if Pos('--collector', CommandLine) > 0 then Result := True;
      end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Locator, Service, Owner: Variant;
    Runtime, Sid, ExpectedPath: String;
    Attempt: Integer;
begin
  Result := '';
  ExpectedPath := ExpandConstant('{app}\HardwarePulse.exe');
  if not FileExists(ExpectedPath) then Exit;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('', 'root\CIMV2');
    Owner := Service.Get('Win32_Process.Handle="' + IntToStr(GetCurrentProcessId()) + '"').ExecMethod_('GetOwnerSid');
    if Owner.ReturnValue <> 0 then RaiseException(LocalText('Cannot identify the setup account.','无法识别安装账户。','無法識別安裝帳戶。'));
    Sid := Owner.Sid;
    if (Pos('S-1-', Sid) <> 1) or (Pos('\', Sid) > 0) or (Pos('/', Sid) > 0) then
      RaiseException(LocalText('Invalid setup account identifier.','安装账户标识无效。','安裝帳戶識別碼無效。'));
    Runtime := ExpandConstant('{commonappdata}\HardwarePulse\') + Sid + '\runtime';
    if DirExists(Runtime) then begin
      if not SaveStringToFile(Runtime + '\STOP', 'Installer preparing upgrade', False) then
        RaiseException(LocalText('Cannot request Hardware Pulse shutdown.','无法请求 Hardware Pulse 退出。','無法要求 Hardware Pulse 結束。'));
      Log('Requested cooperative Hardware Pulse shutdown.');
    end;
    for Attempt := 1 to 40 do begin
      if not CollectorRunning(Service, ExpectedPath) then begin
        Log('Collector stopped; Windows Restart Manager will close any remaining UI.');
        Exit;
      end;
      Sleep(250);
    end;
    Result := LocalText('The previous Hardware Pulse collector is still running. Exit Pulse in other signed-in accounts and retry. No application files were replaced.','先前的 Hardware Pulse 采集进程仍在运行。请退出其他已登录账户中的 Pulse 后重试。尚未替换应用文件。','先前的 Hardware Pulse 收集程序仍在執行。請結束其他已登入帳戶中的 Pulse 後重試。尚未取代應用程式檔案。');
  except
    Result := LocalText('Could not prepare Hardware Pulse for upgrade: ','无法准备 Hardware Pulse 升级：','無法準備 Hardware Pulse 升級：') + GetExceptionMessage + LocalText(' No application files were replaced.',' 尚未替换应用文件。',' 尚未取代應用程式檔案。');
  end;
end;

function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then begin
    MsgBox(LocalText('The Windows .NET Framework 4.8 component is missing or damaged. It is included with supported Windows versions. Repair Windows components, then run setup again.','Windows .NET Framework 4.8 组件缺失或损坏。受支持的 Windows 已包含此组件，请修复后重新运行安装程序。','Windows .NET Framework 4.8 元件遺失或損壞。支援的 Windows 已包含此元件，請修復後重新執行安裝程式。'), mbError, MB_OK);
    Exit;
  end;
end;

function PawnIOPresent(): Boolean;
begin
  Result := FileExists(ExpandConstant('{autopf}\PawnIO\PawnIOLib.dll')) and
    RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\PawnIO');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer;
begin
  if CurStep = ssPostInstall then begin
    if not PawnIOPresent() then begin
      if not Exec(ExpandConstant('{tmp}\PawnIO-2.2.0.exe'), '-install', '', SW_HIDE, ewWaitUntilTerminated, Code) then
        RaiseException(LocalText('Could not launch the PawnIO prerequisite installer.','无法启动 PawnIO 依赖安装程序。','無法啟動 PawnIO 相依元件安裝程式。'));
      if Code <> 0 then RaiseException(LocalText('PawnIO installation failed. Exit code: ','PawnIO 安装失败。退出代码：','PawnIO 安裝失敗。結束代碼：') + IntToStr(Code));
      if not PawnIOPresent() then
        RaiseException(LocalText('PawnIO setup finished, but its library or driver registration is missing. Hardware Pulse startup was not registered. Check the PawnIO installation and run setup again.','PawnIO 安装结束，但缺少库文件或驱动注册。尚未注册 Hardware Pulse 启动项。请检查 PawnIO 后重新安装。','PawnIO 安裝結束，但缺少程式庫或驅動程式註冊。尚未註冊 Hardware Pulse 啟動項目。請檢查 PawnIO 後重新安裝。'));
    end;
    if not Exec(ExpandConstant('{app}\HardwarePulse.exe'), '--install-startup', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException(LocalText('Could not register Hardware Pulse startup.','无法注册 Hardware Pulse 启动项。','無法註冊 Hardware Pulse 啟動項目。'));
    if Code <> 0 then RaiseException(LocalText('Startup registration failed. See LocalAppData\HardwarePulse\host-error.txt.','启动项注册失败。请查看 LocalAppData\HardwarePulse\host-error.txt。','啟動項目註冊失敗。請查看 LocalAppData\HardwarePulse\host-error.txt。'));
  end;
end;
