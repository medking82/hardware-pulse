# Shared Windows startup controls

Appearance settings expose Start with Windows, Refresh startup status and an
explicit Start hardware collector action alongside the startup-view preference.
Startup view remains a presentation preference; the controls do not persist a
second copy of scheduler state. Actual worker readback is authoritative.

## Boundary and behavior

`WindowsStartupManagement` accepts no executable, path or arbitrary command from
the UI. It resolves the fixed `worker/HardwarePulse.Collector.exe` beneath the
running shared AppHost directory. The running process must be exactly that
directory's `HardwarePulse.exe`; both paths must pass the existing Program Files
and non-reparse `FpsProtocol.ProtectedTool` policy on every operation.
Development `dotnet`, preview archives and other hosts report unavailable.

Only four existing worker commands are used: `--startup-enabled` without
elevation, and `--enable-startup`, `--disable-startup`, `--start-collector` through
the same explicit UAC flow as WPF. No installer registration/removal command is
exposed. Task ownership, principal checks, XML changes and transaction rollback
remain in the previously reviewed worker/Startup.Shared implementation.

The UI reads status when first displayed and on explicit refresh. Merely opening
settings does not start a collector or change tasks. Commands serialize; while
busy, controls are disabled. After a command, the UI reads actual state rather
than assuming exit success means the requested state was applied. Cancellation,
failure and readback mismatch have explicit messages. Disposal rejects late
results and performs no follow-up query.

The read-only query has a ten-second timeout and may terminate only its own
query child. A management process is never killed mid-transaction when the UI
closes or a timeout expires; the worker retains its rollback responsibility.
Management failure details are not copied into UI logs or settings.

## Guardrails and checks

Allowed scope: modern Windows process adapter, shared startup controls,
Monitor wiring, catalogs and tests. No changes to worker command parsing,
scheduler ownership/privilege policy, installed tasks, installer, user profiles,
collector protocol or WPF. Rollback removes the UI/adapter wiring without
rewriting task state.

`DesktopStartupTests` uses a fake backend to check explicit actions, actual state
readback, UAC cancellation, failure, unverified success, duplicate-command
rejection, disposal and three-language narrow layout. It also calls the real
adapter under the development test process and verifies query/change/start are
rejected before any helper launch. Native and headless suites exercise the UI.
No test changes the user's scheduled tasks or requests real elevation.

Validation: DesktopTests Release build, `--startup-controls`, full headless,
full `--native-session`, then `scripts/Validate.ps1 -ModernCore`, sequentially.
Local logs: `vendor/test-startup-controls-full.log`,
`vendor/test-startup-controls-native.log`, `vendor/validate-startup-controls.log`.
Actual installed payload, UAC, worker matching, task enable/disable and
real logon/session shutdown remain final release acceptance; fake UAC outcomes
do not establish those results.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
