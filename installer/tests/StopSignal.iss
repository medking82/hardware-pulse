[Setup]
AppId=HardwarePulseStopSignalTests
AppName=Hardware Pulse STOP recovery validation
AppVersion=1.0
PrivilegesRequired=lowest
CreateAppDir=no
Uninstallable=no
OutputBaseFilename=stop-signal

[Code]
#include "..\StopSignal.iss"

procedure Check(Condition: Boolean; Message: String);
begin
  if not Condition then RaiseException(Message);
end;

function InitializeSetup(): Boolean;
var Path, Linked: String; Contents: AnsiString; Rejected: Boolean; I: Integer;
begin
  Path := ExpandConstant('{tmp}\STOP');
  WriteSetupStop(Path, 'owned attempt'); RestoreSetupStop();
  Check(not FileExists(Path), 'Absent prior STOP was not restored');
  Check(SaveStringToFile(Path, 'prior bytes'#13#10, False), 'Fixture write failed');
  WriteSetupStop(Path, 'second attempt'); RestoreSetupStop();
  Check(LoadStringFromFile(Path, Contents) and (Contents = 'prior bytes'#13#10), 'Prior STOP bytes lost');
  WriteSetupStop(Path, 'third attempt');
  Check(SaveStringToFile(Path, 'concurrent owner', False), 'Concurrent fixture failed');
  RestoreSetupStop();
  Check(LoadStringFromFile(Path, Contents) and (Contents = 'concurrent owner'), 'Concurrent STOP overwritten');
  WriteSetupStop(Path, 'fourth attempt'); DeleteFile(Path); RestoreSetupStop();
  Check(not FileExists(Path), 'Concurrent removal undone');
  Contents := ''; for I := 1 to 4097 do Contents := Contents + 'x';
  Check(SaveStringToFile(Path, Contents, False), 'Oversized fixture failed');
  Rejected := False;
  try WriteSetupStop(Path, 'must not overwrite'); except Rejected := True; end;
  Check(Rejected and LoadStringFromFile(Path, Contents) and (Length(Contents) = 4097), 'Oversized STOP was changed');
  Linked := ExpandConstant('{param:LINKPATH|}');
  if Linked <> '' then begin
    Rejected := False;
    try WriteSetupStop(Linked + '\STOP', 'must not follow'); except Rejected := True; end;
    Check(Rejected and not FileExists(Linked + '\STOP'), 'Linked path admitted');
  end;
  Log('PASS STOP recovery: absent, prior bytes, concurrent replacement/removal, bounded input and linked path');
  Result := False;
end;
