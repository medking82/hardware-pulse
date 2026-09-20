// Shared by the install gate and a no-install Pascal Script validation harness.
// Resolve both generations relative to this exact installation, never by name alone.
function IsPulseCollectorPath(ActualPath, ExpectedUi: String): Boolean;
begin
  Result := (CompareText(ActualPath, ExpectedUi) = 0) or
    (CompareText(ActualPath, ExtractFileDir(ExpectedUi) + '\worker\HardwarePulse.Collector.exe') = 0);
end;

function IsPulseCollector(ActualPath, CommandLine, ExpectedUi: String): Boolean;
begin
  Result := IsPulseCollectorPath(ActualPath, ExpectedUi) and
    (Pos('--collector', CommandLine) > 0);
end;
