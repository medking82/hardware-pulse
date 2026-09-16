# Dedicated Windows x64 collector payload

The shared Windows release keeps a .NET Framework 4.8 worker for the existing
LibreHardwareMonitor collector. `scripts/Build-WindowsCollector.ps1` builds
`build/windows-worker/worker/HardwarePulse.Collector.exe`, its Framework
Core/adapter assemblies and private libraries. `tools/PresentMon.exe` and notices
live at the payload root. Dependency archives and the executable are SHA-256
checked against `dependencies.lock.json` before use. The modern UI assemblies
must remain outside `worker/`; their matching assembly names are not interchangeable.

## Ownership and compatibility

`src/Hosts/WindowsCollector/Program.cs` is the only new runtime entry point.
It accepts exactly `--collector` and uses a fixed layout: sibling `worker/` and
root `HardwarePulse.exe` (the future shared Windows AppHost). No arguments may
select a peer executable, runtime directory, target game or tool. Missing root
UI returns an error before touching installed state.

`src/Native/Collector.cs` and `src/Native/FpsTransport.cs` are compiled unchanged.
They still own the per-user snapshot, singleton collector mutex, cooperative
STOP handling, read-only sensors and FPS server. FPS retains the exact UI path,
current SID/session, process-start identity and target lease checks. PresentMon
must still be in a non-reparse Program Files location. The UI does not receive
driver access, and the worker contains no WPF, Avalonia or PowerShell dependency.

This change builds and exercises the payload only. It neither registers nor
changes scheduled tasks, installs a driver, starts an elevated collector or
replaces the installed WPF application. Startup ownership/migration, the shared
FPS client and the final installer remain separate pending release work.
Rollback removes only these new build/host files and the validation hook.

## Acceptance

`scripts/Test-WindowsCollector.ps1` compiles a temporary harness against the
actual worker assembly, rejects unsupported commands and an incomplete payload,
and verifies the fixed FPS peer path. It checks assembly references and runs two
driver-free samples with an isolated runtime directory and unique mutex, then
checks schema, sequence, process identity, CPU/RAM and absent invented sensors.
It removes the harness from the payload. Machine snapshot evidence stays under
ignored `vendor/`; it is not a distribution artifact or CI upload.

The check is part of `scripts/Validate.ps1` and the Windows x64 collector CI job.
It does not establish elevated physical sensor or real-game FPS acceptance;
those remain required against the final installed shared payload.

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
