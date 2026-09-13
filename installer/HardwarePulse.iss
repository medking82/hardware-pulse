[Setup]
AppId={{75E8FDDA-D799-4D8A-882D-972DC72151C2}
AppName=Hardware Pulse
AppVersion=0.4.1
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
OutputBaseFilename=HardwarePulse-0.4.1-Setup
SetupIconFile=..\assets\pulse.ico
UninstallDisplayIcon={app}\HardwarePulse.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\build\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\vendor\PawnIO-2.2.0.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Hardware Pulse"; Filename: "{app}\HardwarePulse.exe"

[Run]
Filename: "{app}\HardwarePulse.exe"; Description: "Launch Hardware Pulse"; Flags: postinstall nowait skipifsilent runasoriginaluser

[UninstallRun]
Filename: "{app}\HardwarePulse.exe"; Parameters: "--remove-startup"; Flags: runhidden waituntilterminated; RunOnceId: "RemovePulseStartup"

[Code]
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
          RaiseException('Cannot inspect the previous Hardware Pulse session.');
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
    if Owner.ReturnValue <> 0 then RaiseException('Cannot identify the setup account.');
    Sid := Owner.Sid;
    if (Pos('S-1-', Sid) <> 1) or (Pos('\', Sid) > 0) or (Pos('/', Sid) > 0) then
      RaiseException('Invalid setup account identifier.');
    Runtime := ExpandConstant('{commonappdata}\HardwarePulse\') + Sid + '\runtime';
    if DirExists(Runtime) then begin
      if not SaveStringToFile(Runtime + '\STOP', 'Installer preparing upgrade', False) then
        RaiseException('Cannot request Hardware Pulse shutdown.');
      Log('Requested cooperative Hardware Pulse shutdown.');
    end;
    for Attempt := 1 to 40 do begin
      if not CollectorRunning(Service, ExpectedPath) then begin
        Log('Collector stopped; Windows Restart Manager will close any remaining UI.');
        Exit;
      end;
      Sleep(250);
    end;
    Result := 'The previous Hardware Pulse collector is still running. Exit Pulse in other signed-in accounts and retry. No application files were replaced.';
  except
    Result := 'Could not prepare Hardware Pulse for upgrade: ' + GetExceptionMessage + ' No application files were replaced.';
  end;
end;

function InitializeSetup(): Boolean;
var Release: Cardinal;
    PSVersion: String;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then begin
    MsgBox('The Windows .NET Framework 4.8 component is missing or damaged. It is included with supported Windows versions. Repair Windows components, then run setup again.', mbError, MB_OK);
    Exit;
  end;
  Result := FileExists(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe')) and
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\PowerShell\3\PowerShellEngine', 'PowerShellVersion', PSVersion);
  if Result then Result := Pos('5.1.', PSVersion + '.') = 1;
  if not Result then MsgBox('Windows PowerShell 5.1 is missing or damaged. Restore this Windows component, then run setup again. PowerShell 7 is not a substitute.', mbError, MB_OK);
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
        RaiseException('Could not launch the PawnIO prerequisite installer.');
      if Code <> 0 then RaiseException('PawnIO installation failed. Exit code: ' + IntToStr(Code));
      if not PawnIOPresent() then
        RaiseException('PawnIO setup finished, but its library or driver registration is missing. Hardware Pulse startup was not registered. Check the PawnIO installation and run setup again.');
    end;
    if not Exec(ExpandConstant('{app}\HardwarePulse.exe'), '--install-startup', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException('Could not register Hardware Pulse startup.');
    if Code <> 0 then RaiseException('Startup registration failed. See LocalAppData\HardwarePulse\host-error.txt.');
  end;
end;
