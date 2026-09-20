#ifndef ProbeToken
  #error ProbeToken is required
#endif
#ifndef ProbePayload
  #error ProbePayload is required
#endif
[Setup]
AppId=HardwarePulseRollbackProbe
AppName=Hardware Pulse isolated rollback probe
AppVersion=1.0
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultDirName={src}\installed
DisableDirPage=yes
DisableProgramGroupPage=yes
UsePreviousAppDir=no
Uninstallable=no
CreateUninstallRegKey=no
CloseApplications=no
RestartApplications=no
OutputBaseFilename=rollback-probe

[Files]
Source: "{#ProbePayload}"; DestName: "prerequisite.txt"; Flags: dontcopy
Source: "{#ProbePayload}"; DestDir: "{app}"; DestName: "replaced.txt"; Flags: ignoreversion; AfterInstall: AfterFirstFile
Source: "{#ProbePayload}"; DestDir: "{app}"; DestName: "new.txt"; Flags: ignoreversion

[Code]
#ifdef GuardInstallOutcome
#include "..\InstallOutcome.iss"
#else
function IsPulseInstallReady(): Boolean;
begin
  Result := True;
end;
procedure MarkPulseInstallComplete();
begin
end;
#endif

function CanRunProbe(): Boolean;
begin
  Result := IsPulseInstallReady();
  if Result then SaveStringToFile(ExpandConstant('{src}\launch-permitted.txt'), 'ready', False);
end;

function Phase(): String;
begin
  Result := ExpandConstant('{param:PROBEPHASE|invalid}');
end;

function InitializeSetup(): Boolean;
var Marker: AnsiString;
begin
  Result := LoadStringFromFile(ExpandConstant('{src}\fixture-owner.txt'), Marker) and (Marker = '{#ProbeToken}');
  Result := Result and ((Phase() = 'before') or (Phase() = 'prerequisite') or (Phase() = 'during') or (Phase() = 'after') or (Phase() = 'success'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if CompareText(ExpandConstant('{app}'), ExpandConstant('{src}\installed')) <> 0 then
    Result := 'Probe refuses a different destination'
  else if Phase() = 'before' then
    Result := 'PROBE deliberate failure before file replacement'
  else if Phase() = 'prerequisite' then begin
    try
      ExtractTemporaryFile('prerequisite.txt');
      if not FileExists(ExpandConstant('{tmp}\prerequisite.txt')) then
        RaiseException('Probe prerequisite extraction failed');
      Log('PROBE prerequisite extracted before file replacement');
      RaiseException('PROBE deliberate failure after prerequisite extraction');
    except
      Result := GetExceptionMessage;
    end;
  end;
end;

procedure AfterFirstFile();
begin
  if Phase() = 'during' then RaiseException('PROBE deliberate failure after first file replacement');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if Phase() = 'after' then RaiseException('PROBE deliberate failure at ssPostInstall');
    MarkPulseInstallComplete();
    Log('PROBE post-install completed');
  end;
end;

[Run]
#ifdef GuardInstallOutcome
Filename: "{sys}\whoami.exe"; Flags: postinstall runhidden waituntilterminated; Check: CanRunProbe
#else
Filename: "{sys}\whoami.exe"; Flags: runhidden waituntilterminated; Check: CanRunProbe
#endif
