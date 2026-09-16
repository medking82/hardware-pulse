# Shared Windows FPS client

The shared Monitor FPS card and floating Desktop consume one `FpsPanel` session.
The persisted `fps` preference defaults off; `fpsTarget` stores a process name,
not a path or PID. Auto mode reuses the WPF foreground selection policy, including
retaining the last eligible app while configuring Pulse. Refresh apps is explicit.
Reset clears displayed values immediately and sends a generation change to the
existing collector. Unknown 1% low remains unavailable rather than zero.

## Existing boundaries reused

`WindowsFpsSource` owns the selected process and the existing `FpsClient`.
It uses the fixed `worker/HardwarePulse.Collector.exe` server identity beneath
the AppHost directory. The modern Windows adapter links `FpsTransport.cs` and
`OverlayTarget.cs`; only the Framework target compiles `FpsServer`. Modern-only
OS guards prevent Windows identity APIs from being used on other platforms.
The fixed request/response schema, SID/session, process birth, exact peer path
and collector target lease checks are preserved. The client never supplies a
tool path or executable command and never launches an elevated helper.

`FpsPanel` serializes source access off the UI thread and publishes an immutable
display snapshot to both views. Disabling cancels the session, clears values and
disposes its source. Late target/reset/cancel results are rejected. Re-enabling
waits for the prior session to finish, so acquisition sessions do not overlap.
No polling, process discovery or pipe is started by a disabled new profile.
Floating Desktop owns no extra FPS acquisition. Close-to-tray retains the same
session; application close disposes it.

## Verification and remaining release gates

`FpsPanelTests` checks opt-in, shared values, unknown low, Reset, process selection,
pending cancellation, disposal, settings roundtrip, path rejection and modern
Windows peer/process-birth checks. Headless rendering at 360 DIP is inspected.
The existing Framework FPS tests continue to cover framing, fragmentation, PID
reuse and timeout cancellation; full repository validation remains required.

Local validation passed: `Validate.ps1 -ModernCore`, complete headless Desktop
regression, native Windows Desktop session and focused FPS lifecycle tests,
including re-enable while a canceled poll is pending. Evidence is retained in
`vendor/validate-shared-fps-final.log`, `vendor/test-shared-fps-full.log`,
`vendor/test-shared-fps-native.log` and `vendor/test-shared-fps-final.log`.

This is client integration, not real-game capture acceptance. Preview archives
do not yet include a registered matching worker; the installed WPF collector is
not accepted as a substitute identity. Missing collectors remain unavailable.
The final Windows AppHost must be `HardwarePulse.exe` to satisfy the worker's
existing UI peer check. Installer/startup integration, real capture verification,
game-overlay behavior and total CPU/RAM/soak acceptance remain open.

Rollback reverts this adapter/presentation wiring and preserves installed settings,
tasks, driver and credential stores. WPF behavior and the server authorization
contract are not changed by this increment.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
