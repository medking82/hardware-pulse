# Shared Windows startup migration

## Frozen scope and owners

The release needs the existing `Hardware Pulse Widget` task to launch the shared
root `HardwarePulse.exe` at LeastPrivilege, and `Hardware Pulse Collector` to
launch `worker/HardwarePulse.Collector.exe --collector` at HighestAvailable.
Both remain InteractiveToken tasks for the same SID and logon trigger. The
existing `Startup`/`ITaskStore` boundary owns XML validation, registration and
rollback; `SchedulerStore` alone talks to Task Scheduler. The dedicated worker
provides fixed management switches for the eventual installer. WPF's original
constructor and same-executable contract remain supported.

Allowed changes: Startup's fixed shared-layout factory and explicit installation
migration, worker management entry points, build wiring, fake-store regression
and this gate record. Installer replacement, settings migration, UI startup
controls, driver installation and actual installed task mutations are outside
this increment. No development test may change the user's scheduled tasks.

## Migration and failure contract

1. Worker management requires both fixed executables beneath non-reparse Program
   Files paths. No CLI argument supplies a path, SID, task name or XML.
2. Read both named tasks and preserve their original XML before writes. Validate
   every existing action, SID, principal, logon type, trigger and run level.
   Only installation migration may also admit the exact legacy collector action
   at the same root `HardwarePulse.exe --collector`.
3. Generate the new task pair, preserving each task's effective logon preference.
   Before each write, reject a changed preimage. Read back each new task and
   validate the new ownership and enabled state.
4. On registration failure, restore prior XML in reverse order, or remove a newly
   created task. Rollback refuses foreign ownership and reports incomplete
   rollback rather than overwriting it. Read back restored ownership/preferences.
5. Only after installation registration succeeds may the worker clear STOP and
   request an on-demand collector start. This start is a separate operational
   effect: a launch failure reports an error but retains valid registration.
   Enable/disable and remove require the new exact collector action; they do not
   silently migrate legacy tasks.

Tests use in-memory stores for clean install, mixed/legacy migration, disabled
preferences, pre-write and post-write failures, unchanged state after preflight
rejection, rollback, unrelated task preservation and retained WPF behavior.
`Validate.ps1 -ModernCore` exercises both existing and dedicated worker tests.
One independent Native Review is required for the frozen passing diff. Actual
installer, reboot and uninstall acceptance remain later release gates.

Deterministic checks passed locally: full `Validate.ps1 -ModernCore` including
legacy WPF startup and dedicated worker tests; focused worker regression also
covers protected-path refusal, fresh-install cleanup and incomplete rollback.
Logs: `vendor/validate-shared-startup.log` and
`vendor/test-shared-startup-final.log`. No installed task was mutated.

Rollback of this source increment is a Git revert only. It is not authorization
to downgrade installed tasks or reinstall the user's app. The previous installed
UI, collector, settings and credentials are keepers throughout development.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"changed","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"required","kind":"risk-classification-assessment","reasons":{"formal_review":["high_risk_requires_review"],"risk":["privilege_boundary_change"]},"risk":"high","schema_version":2} -->
