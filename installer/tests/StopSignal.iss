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

function TestHardLink(Name, Existing: String; Reserved: THandle): BOOL;
  external 'CreateHardLinkW@kernel32.dll stdcall';

procedure Check(Condition: Boolean; Message: String);
begin
  if not Condition then RaiseException(Message);
end;

function InitializeSetup(): Boolean;
var Path, Linked, Pinned: String; Contents: AnsiString; Rejected: Boolean; I: Integer;
    Directories: TStopHandles; Writer: THandle;
begin
  Path := ExpandConstant('{tmp}\STOP');
  WriteSetupStop(Path, 'owned attempt'); RestoreSetupStop();
  Check(not FileExists(Path), 'Absent prior STOP was not restored');
  Check(SaveStringToFile(Path, 'prior bytes'#13#10, False), 'Fixture write failed');
  WriteSetupStop(Path, 'second attempt');
  Check(LoadStringFromFile(Path, Contents) and (Contents = 'prior bytes'#13#10), 'Existing signal changed during setup');
  RestoreSetupStop();
  Check(LoadStringFromFile(Path, Contents) and (Contents = 'prior bytes'#13#10), 'Prior STOP bytes lost');
  Check(DeleteFile(Path), 'Could not reset fixture');
  WriteSetupStop(Path, 'third attempt');
  Check(SaveStringToFile(Path, 'concurrent owner', False), 'Concurrent fixture failed');
  RestoreSetupStop();
  Check(LoadStringFromFile(Path, Contents) and (Contents = 'concurrent owner'), 'Concurrent STOP overwritten');
  Check(DeleteFile(Path), 'Could not reset changed-content fixture');
  WriteSetupStop(Path, 'fourth attempt'); DeleteFile(Path); RestoreSetupStop();
  Check(not FileExists(Path), 'Concurrent removal undone');
  WriteSetupStop(Path, 'same bytes');
  Check(RenameFile(Path, Path + '.previous'), 'Could not retain original identity');
  Check(SaveStringToFile(Path, 'same bytes', False), 'Replacement fixture failed');
  RestoreSetupStop();
  Check(LoadStringFromFile(Path, Contents) and (Contents = 'same bytes'), 'A different file with equal bytes was removed');
  Check(DeleteFile(Path), 'Could not reset replacement fixture');
  WriteSetupStop(Path, 'linked signal');
  Check(TestHardLink(Path + '.second-name', Path, 0), 'Could not add fixture name');
  RestoreSetupStop();
  Check(FileExists(Path) and FileExists(Path + '.second-name'), 'Cleanup admitted a multi-link file');
  Check(DeleteFile(Path + '.second-name'), 'Could not release fixture name');
  RestoreSetupStop(); Check(not FileExists(Path), 'Cleanup did not recover after the extra name was removed');
  Check(SaveStringToFile(Path + '.source', 'preserved', False), 'Link fixture failed');
  Check(TestHardLink(Path, Path + '.source', 0), 'Could not create fixture name');
  Rejected := False;
  try WriteSetupStop(Path, 'must reject'); except Rejected := True; end;
  Check(Rejected and LoadStringFromFile(Path + '.source', Contents) and (Contents = 'preserved'), 'Initial STOP admission accepted multiple links');
  Check(DeleteFile(Path), 'Could not reset link fixture');
  WriteSetupStop(Path, ''); RestoreSetupStop(); Check(not FileExists(Path), 'Empty owned signal was not removed');
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
  Pinned := ExpandConstant('{tmp}\pinned'); Check(ForceDirectories(Pinned), 'Cannot create directory fixture');
  try
    PinStopDirectory(Pinned, Directories);
    Check(not RenameFile(Pinned, Pinned + '-moved'), 'Pinned directory could be renamed');
    Writer := PulseOpenFile(Pinned, $40000000, 7, 0, 3, $02200000, 0);
    if Writer <> THandle(-1) then begin PulseCloseFile(Writer); RaiseException('Pinned directory admitted a write handle'); end;
  finally CloseStopDirectories(Directories); end;
  Writer := PulseOpenFile(Pinned, $40000000, 7, 0, 3, $02200000, 0);
  Check(Writer <> THandle(-1), 'Released directory cannot be opened for write');
  try
    Rejected := False;
    try WriteSetupStop(Pinned + '\STOP', 'blocked'); except Rejected := True; end;
    Check(Rejected and not FileExists(Pinned + '\STOP'), 'Existing write handle did not prevent STOP publication');
  finally PulseCloseFile(Writer); end;
  Log('PASS STOP recovery: existing signal preservation, identity and content ownership, bounded input, single-link admission and pinned directory path');
  Result := False;
end;
