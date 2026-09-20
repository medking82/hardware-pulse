[Setup]
AppId=HardwarePulseCollectorIdentityTests
AppName=Hardware Pulse collector identity validation
AppVersion=1.0
PrivilegesRequired=lowest
CreateAppDir=no
Uninstallable=no
OutputBaseFilename=collector-identity

[Code]
#include "..\CollectorIdentity.iss"

procedure Check(Condition: Boolean; Message: String);
begin
  if not Condition then RaiseException(Message);
end;

function InitializeSetup(): Boolean;
var Ui, Worker: String;
begin
  Ui := 'C:\Program Files\Hardware Pulse\HardwarePulse.exe';
  Worker := ExtractFileDir(Ui) + '\worker\HardwarePulse.Collector.exe';
  Check(IsPulseCollector(Ui, '"' + Ui + '" --collector', Ui), 'Legacy collector missed');
  Check(IsPulseCollector(Worker, '"' + Worker + '" --collector', Ui), 'Shared worker missed');
  Check(IsPulseCollector(Lowercase(Worker), '--collector', Ui), 'Windows path case changed identity');
  Check(not IsPulseCollector(Ui, '"' + Ui + '"', Ui), 'UI mistaken for collector');
  Check(not IsPulseCollector(Worker, '--startup-enabled', Ui), 'Management command mistaken for collector');
  Check(not IsPulseCollector('C:\other\HardwarePulse.exe', '--collector', Ui), 'Foreign legacy path admitted');
  Check(not IsPulseCollector('C:\other\worker\HardwarePulse.Collector.exe', '--collector', Ui), 'Foreign worker path admitted');
  Check(not IsPulseCollector(ExtractFileDir(Ui) + '\worker-other\HardwarePulse.Collector.exe', '--collector', Ui), 'Sibling worker admitted');
  Log('PASS collector identity: legacy and shared paths, UI/management and foreign-path rejection');
  // Exit before any setup/install step. The runner expects exit code 1 and this marker.
  Result := False;
end;
