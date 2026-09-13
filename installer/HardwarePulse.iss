[Setup]
AppId={{75E8FDDA-D799-4D8A-882D-972DC72151C2}
AppName=Hardware Pulse
AppVersion=0.2.0
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
OutputBaseFilename=HardwarePulse-0.2.0-Setup
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
function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then MsgBox('Hardware Pulse requires the Windows .NET Framework 4.8 component. Repair or enable it in Windows, then run setup again.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer;
begin
  if CurStep = ssPostInstall then begin
    if not FileExists(ExpandConstant('{autopf}\PawnIO\PawnIOLib.dll')) then begin
      if not Exec(ExpandConstant('{tmp}\PawnIO-2.2.0.exe'), '-install', '', SW_HIDE, ewWaitUntilTerminated, Code) then
        RaiseException('Could not launch the PawnIO prerequisite installer.');
      if Code <> 0 then RaiseException('PawnIO installation failed. Exit code: ' + IntToStr(Code));
    end;
    if not Exec(ExpandConstant('{app}\HardwarePulse.exe'), '--install-startup', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      RaiseException('Could not register Hardware Pulse startup.');
    if Code <> 0 then RaiseException('Startup registration failed. See LocalAppData\HardwarePulse\host-error.txt.');
  end;
end;
