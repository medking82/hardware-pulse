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
5. The shared worker first performs read-only installation ownership admission,
   then creates its runtime directory and clears STOP before task mutation.
   `InstallAndStartCollector` re-reads and admits tasks, captures the preimage,
   registers and reads back both definitions, then requests collector launch inside
   the same task transaction. A launch failure validates the current new
   collector task before requesting Stop, then restores prior definitions in
   reverse order. Failure to stop or restore is reported alongside the original
   error; a foreign replacement is neither stopped nor overwritten. Successful
   on-demand launch preserves the independent logon preferences. Enable/disable
   and remove still require the new exact collector action; they do not migrate.

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

## Launch failure transaction follow-up

The previous shared worker called Install, runtime preparation and StartCollector
as separate operations. An injected Run failure reproduced committed new tasks
after the command failed (`vendor/test-startup-launch-red.log`). The follow-up
extends the existing task transaction through launch and admits filesystem work
before registration. WPF callers of `Install()` retain registration-only behavior;
task names, action paths, SID/logon/privilege rules and management arguments are
unchanged. The added failure-path Stop of an elevated collector requires the
high-risk frozen-diff review below. No test modifies installed scheduled tasks.

Focused fake-store checks cover fresh-install removal, exact disabled legacy XML
restoration, no launch after registration failure, successful launch with disabled
logon, an uncertain launch plus failed Stop, and a foreign task substituted during
Run. The latter remains untouched and produces incomplete-rollback evidence.
The task transaction does not restore installer files or the runtime STOP marker,
and does not claim to recover a killed installer/process or power loss. A failed
Stop is an explicit unresolved runtime effect, not a successful rollback claim.
The final installer file/task transaction remains a release gate.
Focused regression passed in `vendor/test-startup-launch-final.log`; final
repository validation, including the added read-only ownership admission check,
passed in `vendor/validate-startup-launch-final.log`.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"changed","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"required","kind":"risk-classification-assessment","reasons":{"formal_review":["high_risk_requires_review"],"risk":["privilege_boundary_change"]},"risk":"high","schema_version":2} -->
