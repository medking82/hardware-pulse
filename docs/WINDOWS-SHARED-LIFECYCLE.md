# Shared Windows lifecycle

Normal shared-host Windows launches use `WindowsInstanceSession` before creating
Avalonia or `MonitorSource`. Its Local named mutex is scoped to the current user
and profile identity. A secondary launch sends the existing `AppActivation`
show-home event and exits. The event contains no command or caller-supplied data.
An event signaled before UI initialization remains pending until the owner listens.

The existing WPF activation source now accepts a dispatcher-post delegate; its
WPF overload retains shutdown checks. Modern Windows adapters compile the same
source without a WPF dependency. Foreground permission is granted only to peers
matching the executable path/session, as before. Development `dotnet` launches
skip foreground permission transfer because the runtime path cannot identify
the app. Their activation event still restores the existing window.

`MonitorWindow.RestoreMain` is shared by activation and the tray. It restores
the same window, ignores activation after close, and preserves the existing
sampling task. Windows mutex abandonment permits recovery after a crashed owner.
The lock is acquired and released on the application STA thread.

The current identity is `Shared.Preview`, separate from installed WPF and its
settings. Demo, smoke and measurement runs bypass this identity and continue
to use no personal settings. Stable profile identity/migration and startup mode
restore remain separate release work; this is not a preview-to-stable promotion.

Guardrails: no scheduler registration, credential access, collector ownership,
installed-profile migration, or IPC command execution changes. Allowed scope is
activation reuse, the modern instance adapter, shared entrypoint/restore and tests.
Rollback is this diff without modifying runtime state or installed settings.

`WindowsInstanceTests` starts real hidden child test processes under randomized
identities. It verifies secondary exclusion, early and repeated notifications,
same-window/sampler restoration, separate identity admission, graceful shutdown,
abandoned-mutex recovery and suppression of a callback queued before disposal.
The crash test terminates only the process it created. No installed app is stopped.
Full headless/native Desktop tests and `scripts/Validate.ps1 -ModernCore` cover
shared behavior and WPF regression. Local logs: `vendor/test-instance-full.log`,
`vendor/test-instance-native-isolated.log`, `vendor/validate-shared-instance.log`.
The initial native run (`vendor/test-instance-native.log`) failed the existing
red-pixel visibility assertion while WPF Validate UI tests also ran. The complete
native suite passed when run alone, with no assertion or product change. Window
composition timing or overlapping test UI is suspected, not proven. Preserve
the initial evidence and serialize these desktop UI checks for final acceptance.
Actual stable installer launch and foreground behavior remain final acceptance.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
