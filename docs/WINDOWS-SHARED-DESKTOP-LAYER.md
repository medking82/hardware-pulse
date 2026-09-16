# Shared Windows desktop placement

The shared floating view uses the existing WPF `DesktopLayer` Z-order policy:
the locked readout is directly above the visible Progman/WorkerW icon host and
below ordinary windows. `WindowsDesktopLayer` owns only this view's HWND and its
out-of-context foreground notification. It does not reparent into Explorer,
inject code, replace wallpaper or change another window. Windows input styles
remain owned by `WindowsWindowInput`; sampling remains owned by Monitor.

The adapter refreshes on foreground notification, lock/topmost/show transitions
and the existing Monitor snapshot cadence. It adds no timer or sampling loop.
Queued notifications coalesce and check disposal. Hidden windows stay hidden;
unlock raises the editor without stealing activation. Explicit Monitor/tray
opening retains its existing activation behavior. Topmost remains the saved user
choice. Missing Explorer host leaves the view as an ordinary non-topmost window;
the next sample retries discovery. `DesktopLayerAvailable` reports attachment.

Allowed scope is the shared floating view, Windows host adapter and its native
tests. WPF, collector privileges, task registration, user profiles and packaging
remain unchanged. Rollback is removal of this adapter and its floating-view
wiring, preserving all settings.

`WindowsLayerTests` checks actual HWND Z-order above the running Explorer icon
host, unchanged parent/position, no activation theft, preserved locked input
styles, topmost roundtrip, user-hidden persistence, show recovery, editable unlock
and disposal. The first run reproduced an unlock defect: restoring input alone
left the editor at desktop depth. Raising only on the placed-to-unlocked
transition fixed that failure. Tests use their own windows and do not invoke
Show Desktop or restart the user's Explorer. Those cases and final installed
acceptance remain open; this evidence does not cover background capture,
Local Contrast or screenshot mode.

Validation commands: DesktopTests Release build, `--layer-native`, full headless,
`--native-session`, then `scripts/Validate.ps1 -ModernCore`. Run the UI suites
sequentially. Local logs: `vendor/test-layer-full.log`,
`vendor/test-layer-native.log`, `vendor/validate-layer.log`.

Win32 references: [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)
and [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook).

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
