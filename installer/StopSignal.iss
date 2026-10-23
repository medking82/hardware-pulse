// Preserve an existing signal. Own only a new, bounded file created by this setup.
type
  TStopInfo = record
    Attributes: Cardinal;
    Creation, Access, WriteTime: TFileTime;
    Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow: Cardinal;
  end;
  TStopHandles = array of THandle;

var SetupStopPath: String;
    SetupStopWritten: AnsiString;
    SetupStopIdentity: TStopInfo;

function PulseOpenFile(Path: String; Access, Share: Cardinal; Security: THandle; Disposition, Flags: Cardinal; Template: THandle): THandle;
  external 'CreateFileW@kernel32.dll stdcall';
function PulseFileInfo(Handle: THandle; var Info: TStopInfo): BOOL;
  external 'GetFileInformationByHandle@kernel32.dll stdcall';
function PulseReadFile(Handle: THandle; Buffer: AnsiString; Count: Cardinal; var ReadCount: Cardinal; Overlapped: THandle): BOOL;
  external 'ReadFile@kernel32.dll stdcall';
function PulseWriteFile(Handle: THandle; Buffer: AnsiString; Count: Cardinal; var Written: Cardinal; Overlapped: THandle): BOOL;
  external 'WriteFile@kernel32.dll stdcall';
function PulseSetFileInfo(Handle: THandle; Kind: Integer; Buffer: AnsiString; Size: Cardinal): BOOL;
  external 'SetFileInformationByHandle@kernel32.dll stdcall';
function PulseCloseFile(Handle: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';
function PulseFileError(): Cardinal;
  external 'GetLastError@kernel32.dll stdcall';

procedure CloseStopDirectories(var Handles: TStopHandles);
var I: Integer;
begin
  for I := GetArrayLength(Handles) - 1 downto 0 do PulseCloseFile(Handles[I]);
  SetArrayLength(Handles, 0);
end;

procedure PinStopDirectory(Path: String; var Handles: TStopHandles);
var Parent: String; Handle: THandle; Info: TStopInfo; Count: Integer;
begin
  Parent := ExtractFileDir(Path);
  if (Parent <> '') and (Parent <> Path) then PinStopDirectory(Parent, Handles);
  // LIST_DIRECTORY | READ_ATTRIBUTES participates in sharing (attributes alone does not).
  // Share READ only, OPEN_EXISTING, BACKUP_SEMANTICS | OPEN_REPARSE_POINT.
  Handle := PulseOpenFile(Path, $81, 1, 0, 3, $02200000, 0);
  if Handle = THandle(-1) then RaiseException('Cannot pin the Hardware Pulse STOP directory.');
  Count := GetArrayLength(Handles); SetArrayLength(Handles, Count + 1); Handles[Count] := Handle;
  if not PulseFileInfo(Handle, Info) then RaiseException('Cannot inspect the Hardware Pulse STOP directory.');
  if (Info.Attributes and $410) <> $10 then RaiseException('Hardware Pulse STOP directory must not be a reparse point.');
end;

procedure InspectStop(Handle: THandle; var Info: TStopInfo);
begin
  if not PulseFileInfo(Handle, Info) then RaiseException('Cannot inspect Hardware Pulse STOP.');
  if ((Info.Attributes and $410) <> 0) or (Info.Links <> 1) or
     (Info.SizeHigh <> 0) or (Info.SizeLow > 4096) then
    RaiseException('Hardware Pulse STOP must be a bounded, regular, single-link file.');
end;

procedure DeleteStopHandle(Handle: THandle);
begin
  // FileDispositionInfo deletes this opened file on close, without reopening its name.
  if not PulseSetFileInfo(Handle, 4, #1, 1) then RaiseException('Cannot remove this setup attempt''s STOP signal.');
end;

procedure RestoreSetupStop();
var Directories: TStopHandles; Handle: THandle; Info: TStopInfo; Current: AnsiString; Count: Cardinal;
begin
  if SetupStopPath = '' then Exit;
  try
    try
      PinStopDirectory(ExtractFileDir(SetupStopPath), Directories);
      Handle := PulseOpenFile(SetupStopPath, $80010000, 0, 0, 3, $00200000, 0); // READ | DELETE
      if Handle = THandle(-1) then begin
        if PulseFileError() <> 2 then RaiseException('Cannot open this setup attempt''s STOP signal.');
        SetupStopPath := ''; Exit;
      end;
      try
        InspectStop(Handle, Info);
        if (Info.Volume = SetupStopIdentity.Volume) and (Info.IndexHigh = SetupStopIdentity.IndexHigh) and
           (Info.IndexLow = SetupStopIdentity.IndexLow) then begin
          SetLength(Current, Info.SizeLow);
          if not PulseReadFile(Handle, Current, Info.SizeLow, Count, 0) then RaiseException('Cannot read this setup attempt''s STOP signal.');
          if (Count = Info.SizeLow) and (Current = SetupStopWritten) then begin
            DeleteStopHandle(Handle); Log('Removed this setup attempt''s STOP signal.');
          end else Log('STOP contents changed; preserving the current state.');
        end else Log('STOP identity changed; preserving the current state.');
        SetupStopPath := '';
      finally
        PulseCloseFile(Handle);
      end;
    finally
      CloseStopDirectories(Directories);
    end;
  except
    Log('Hardware Pulse STOP recovery requires attention: ' + GetExceptionMessage);
    // Keep ownership evidence for the existing teardown recovery attempt.
  end;
end;

procedure WriteSetupStop(Path, Token: String);
var Directories: TStopHandles; Handle: THandle; Info: TStopInfo; Written: Cardinal; Contents: AnsiString;
begin
  if SetupStopPath <> '' then begin
    RestoreSetupStop();
    if SetupStopPath <> '' then RaiseException('Previous STOP recovery is incomplete.');
  end;
  Path := ExpandFileName(Path); Contents := AnsiString(Token);
  if Length(Contents) > 4096 then RaiseException('Hardware Pulse STOP token is too long.');
  try
    PinStopDirectory(ExtractFileDir(Path), Directories);
    Handle := PulseOpenFile(Path, $80000000, 0, 0, 3, $00200000, 0);
    if Handle <> THandle(-1) then begin
      try InspectStop(Handle, Info); finally PulseCloseFile(Handle); end;
      Log('Existing Hardware Pulse STOP signal preserved.'); Exit;
    end;
    if PulseFileError() <> 2 then RaiseException('Cannot inspect the existing Hardware Pulse STOP signal.');
    Handle := PulseOpenFile(Path, $C0010000, 0, 0, 1, $00200000, 0); // READ | WRITE | DELETE, CREATE_NEW
    if Handle = THandle(-1) then RaiseException('Cannot create Hardware Pulse STOP.');
    try
      try
        InspectStop(Handle, Info);
        if not PulseWriteFile(Handle, Contents, Length(Contents), Written, 0) then RaiseException('Cannot request Hardware Pulse shutdown.');
        if Written <> Cardinal(Length(Contents)) then RaiseException('Incomplete Hardware Pulse STOP signal.');
        SetupStopIdentity := Info; SetupStopWritten := Contents; SetupStopPath := Path;
      except
        DeleteStopHandle(Handle); RaiseException(GetExceptionMessage);
      end;
    finally
      PulseCloseFile(Handle);
    end;
  finally
    CloseStopDirectories(Directories);
  end;
end;
