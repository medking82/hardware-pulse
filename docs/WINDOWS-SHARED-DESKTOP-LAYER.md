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

## Locked Windows frame

Native inspection found that the shared locked window retained WS_CAPTION and
WS_THICKFRAME, unlike the borderless WPF Desktop. `WindowsLayerTests` first
reproduced this failure (`vendor/test-lock-frame-red.log`). The floating host now
sets `WindowDecorations.None` before applying Windows pass-through, remembers the
editing decorations, and restores them on unlock or a refused lock. Frame
changes preserve the requested client width/height so an unlock does not rewrite
the user's reading area. The native HWND stays the same; capture, input and
Desktop layer adapters retain their owner. Other platforms keep their policies.

Native regression requires no caption/resize frame while locked, the original
editing frame after unlock, repeated-lock idempotence, preserved HWND and an
injected input-refusal path that restores decorations. Existing floating tests
retain their exact geometry assertion. An initial attempt failed that assertion;
preserving client dimensions corrected the implementation rather than weakening
the test. Z-order, topmost, activation, hidden restore and input checks remain.
Final validation passed: full native suite (`vendor/test-lock-frame-native-final.log`),
headless suite (`vendor/test-lock-frame-headless.log`), and
`Validate.ps1 -ModernCore` (`vendor/validate-lock-frame.log`).

## Native Auto-column fixture capacity

Windows x64 CI run `35128982728` reproduced the overflow at commit `59be0e9`
with geometry diagnostics: work area 1024 x 728 at scale 1, window/client/frame
1008 x 712, two columns with minimum width 360, 50 readings, panel height 700,
extent 732 and viewport 712. Requested and achieved dimensions match. The
window has reached the permitted work-area size; a third minimum-width column
cannot fit. This disproves both an unacknowledged resize and unused available
column capacity as the cause of this failure.

The fixture incorrectly called that 20-DIP overflow avoidable: its row count
assumed a 20-DIP row without spacing and margins. It now derives the repeated
fan-row stride from actual measurement, supplies one screen of those rows plus
the four basic metrics, and requires that the single-column fixture overflows
before locking. It retains the original widening and zero-overflow assertions
where two columns fit. A separate over-capacity case requires all readings and
font size to survive, locked dimensions to stay inside the work area, and
unlock/scroll to recover the hidden rows. Production layout is unchanged.

Local diagnostic evidence: `vendor/ci-locked-59be0e9-job.log` and
`vendor/diagnose-locked-layout-local.log`. The earlier local pass used a
3840 x 2088 work area at scale 1.5, explaining why it did not reveal the smaller
CI fixture's impossible capacity requirement. The corrected focused native
test is recorded in `vendor/test-locked-capacity-local.log`; CI verification of
the correction passed in Windows x64 job 104907509224 of Desktop CI run
35129749374 at commit 6c9a5f6. This verifies the CI layout fixture, not installed
release acceptance.
The corrected build had zero warnings/errors, and repository
`scripts/Validate.ps1` passed (`vendor/validate-locked-capacity.log`).

## Intermittent pixel observation

The previously observed plain input-fixture red-pixel failure remains an open
diagnosis. Twenty isolated `--input-native` runs on 2026-09-17 all observed red
both before and immediately after locking (`vendor/input-diagnosis-1.log` through
`vendor/input-diagnosis-20.log`). This does not establish a cause or fix. The test
now logs both pixel values; if the immediate sample fails, it records bounded
later samples and hit targets before retaining the original failure. This can
distinguish initial rendering/composition delay from sustained content loss or
occlusion on a future reproduction. The red-pixel assertion is unchanged.

Win32 references: [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)
and [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook).

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
