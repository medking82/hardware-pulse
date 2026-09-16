var PulseInstallReady: Boolean;

function IsPulseInstallReady(): Boolean;
begin
  Result := PulseInstallReady;
end;

procedure MarkPulseInstallComplete();
begin
  PulseInstallReady := True;
end;

function GetCustomSetupExitCode(): Integer;
begin
  { A caught post-install exception must not become a successful silent update. }
  if PulseInstallReady then Result := 0 else Result := 10;
end;
