// Own only the exact bounded STOP contents written by this setup attempt.
var SetupStopPath: String;
    SetupStopBefore, SetupStopWritten: AnsiString;
    SetupStopExisted: Boolean;

function PulseFileAttributes(Path: String): Cardinal;
  external 'GetFileAttributesW@kernel32.dll stdcall';

procedure CheckStopPath(Path: String);
var Parent: String; Attributes: Cardinal;
begin
  while Path <> '' do begin
    Attributes := PulseFileAttributes(Path);
    if (Attributes <> $FFFFFFFF) and ((Attributes and $400) <> 0) then
      RaiseException('Hardware Pulse STOP path traverses a reparse point.');
    Parent := ExtractFileDir(Path);
    if Parent = Path then Exit;
    Path := Parent;
  end;
end;

function ReadBoundedStop(Path: String; var Contents: AnsiString): Boolean;
var Info: TFindRec;
begin
  Result := False;
  if not FindFirst(Path, Info) then Exit;
  try
    if (Info.SizeHigh <> 0) or (Info.SizeLow > 4096) then Exit;
    Result := LoadStringFromFile(Path, Contents) and (Length(Contents) <= 4096);
  finally
    FindClose(Info);
  end;
end;

procedure RestoreSetupStop();
var Current: AnsiString;
begin
  if SetupStopPath = '' then Exit;
  try
    CheckStopPath(SetupStopPath);
    if ReadBoundedStop(SetupStopPath, Current) and (Current = SetupStopWritten) then begin
      if SetupStopExisted then begin
        if not SaveStringToFile(SetupStopPath, SetupStopBefore, False) then
          RaiseException('Could not restore the prior Hardware Pulse STOP signal.');
      end else if not DeleteFile(SetupStopPath) then
        RaiseException('Could not remove this setup attempt''s STOP signal.');
      Log('Restored prior Hardware Pulse STOP state.');
    end else Log('STOP changed since setup wrote it; preserving the current state.');
    SetupStopPath := '';
  except
    Log('Hardware Pulse STOP recovery requires attention: ' + GetExceptionMessage);
    // Keep ownership evidence for another bounded recovery attempt at teardown.
  end;
end;

procedure WriteSetupStop(Path, Token: String);
begin
  if SetupStopPath <> '' then begin
    RestoreSetupStop();
    if SetupStopPath <> '' then RaiseException('Previous STOP recovery is incomplete.');
  end;
  CheckStopPath(Path);
  SetupStopExisted := FileExists(Path);
  SetupStopBefore := '';
  if SetupStopExisted and not ReadBoundedStop(Path, SetupStopBefore) then
    RaiseException('Cannot preserve the existing Hardware Pulse STOP signal.');
  SetupStopWritten := AnsiString(Token);
  SetupStopPath := Path;
  if not SaveStringToFile(Path, SetupStopWritten, False) then
    RaiseException('Cannot request Hardware Pulse shutdown.');
end;
