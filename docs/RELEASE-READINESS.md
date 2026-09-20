# Multi-platform stable release readiness

The delivery target is a public stable release with usable, verified packages,
not merely pushed adapters or a renamed experimental release. The Windows
reference is WPF v0.6.27. The withdrawn v0.7.0 must not be reused.

## Scope decision

Current user decision (2026-09-20): prioritize a lightweight Windows build over
a shared Windows/macOS UI host. Iterate the original v0.6.27 WPF App, Desktop
and Settings for Windows stable delivery. Retain useful shared Core/adapter
logic without forcing the Avalonia host or its self-contained runtime/fonts
into the Windows installer. macOS packaging and UI may evolve independently;
its release stays RC until the platform gates are verified.

Preserve saved user preferences, geometry and the original visual design.
New/default appearance should retain light text with a deeper background for
readability, keeping the opacity control. Quota reliability and measured
CPU/RAM remain delivery work. Compare the final Windows installer and workload
against v0.6.27; compression alone does not establish runtime improvements.
The shared Windows 0.7.1-rc.1 build is experimental evidence, not the chosen
Windows stable deliverable. Do not publish it as the final Windows update.

## Earlier scope and validation history

Latest priority (2026-09-16, after the Win7 test publication): the user requests
Windows 10/11 and macOS stable delivery as soon as possible. Ship Windows
independently; continue macOS completion. Win7 stays at the explicitly requested
real-device test release while awaiting feedback. The unintegrated NVIDIA API
experiment is not part of the Windows or macOS release payload.

Priority correction (2026-09-16): the user is waiting for Windows x64 delivery
and Windows 7 compatibility. Finish the in-flight checks, then pause further
macOS/Linux expansion and prioritize the existing Windows WPF host and Windows 7
assessment. Windows releases need not wait for multi-platform feature parity.
The full multi-platform objective remains open; this changes order, not completion.
See [Windows 7 compatibility](WINDOWS7-COMPATIBILITY.md).

Accepted Win7 scope (2026-09-16): FPS is explicitly unsupported and is not a
Win7 release gate. Clearly document that limitation, disable FPS controls and
omit PresentMon. Actual Win7 installation and telemetry verification remain
required; modern Windows FPS support is unchanged.

CI run 35068158569 at cf6e82e completed: Linux x64/ARM64, macOS Intel/ARM64
and Windows x64 passed. Linux native X11 checks now confirm pointer delivery
to the underlying window while visible, restored input/frame and closed lifetime.
Windows ARM64 still fails before locking: its fixture is covered by a full-screen
WWAHost window (job 104703135000). This is not a passed interaction gate.

The user selected option 2: complete the main Windows features before the final
multi-platform stable release. Platform-specific capability lists alone do not
satisfy this scope. FPS, Desktop overlay and its interaction/appearance controls,
remaining hardware and quota integrations, and host lifecycle/distribution must
be implemented and verified where applicable. Report actual OS limitations rather
than inventing readings or silently omitting a required feature. A successful
launch or a renamed preview does not establish completion.

The first overlay increment owns presentation only: one floating window consumes
the existing Monitor snapshot, uses native move/resize, and supports optional
topmost display. It must not start another collector, quota reader or timer.
Closing it leaves Monitor running; closing Monitor closes the floating window.
Click-through locking, desktop-layer placement, materials/local contrast, FPS
and persistent layout remain explicit later parity work. This increment does not
claim a completed Desktop mode or exclusive-fullscreen compatibility.

The first floating monitor increment at `8879bf30612d413a0552199b6965fb642ae2e733`
passed all six native session/package jobs in
[run 35061561902](https://github.com/medking82/hardware-pulse/actions/runs/35061561902).
The next increment adds a localized tray/menu-bar entry that restores the same
floating window and rejects callbacks after App exit. Headless and Windows native
tests cover command behavior; shell menu interaction and native click-through
are still unverified/unimplemented respectively.

Tray increment `9c382f8ec7dabcc28fb7d92a0e8f967e0982450e` passed all six jobs in
[run 35062008040](https://github.com/medking82/hardware-pulse/actions/runs/35062008040).
The next Windows-only increment introduces an own-process HWND input adapter.
Native tests confirm underlying-window hit testing while locked, visible content,
idempotence, exact style restoration and tray unlock of the existing floating
window. It refuses foreign HWNDs and an existing layered rendering policy.
This is not evidence for exclusive-fullscreen games, Linux/macOS click-through,
or desktop-layer placement. Other platforms do not expose the Lock button yet.

Run `35062598077` passed Windows x64, both Linux and both macOS jobs, but Windows
ARM64 failed the new input fixture's pre-lock hit-test assertion. No lock had
been applied at that point. The diagnostic correction waits for the real native
hit-test target rather than assuming readiness after 150 ms, and records handles,
screen point, window position, client size, scaling and activation. Local native
tests pass; the ARM64 result must still establish whether this was timing or a
different desktop/environment condition. The failed run does not verify ARM64 lock.

Run `35063101437` again passed five platforms but failed the same ARM64 pre-lock
fixture even after the bounded wait: the window was active at scale 1 and client
300x220, yet point (288,291) hit a different HWND. This rules out assuming a
150 ms startup delay was the sole cause. The next diagnostic records class,
process, native rectangle and root HWND, and compares top-level targets so a
native child surface is not mistaken for a foreign window. Original lock,
visible-content and unlock assertions remain required. macOS adapter work is
held locally until this diagnosis is isolated; it is not part of these CI runs.

Run `35063617065` isolated the ARM64 obstruction: the hit target was a foreign
`Windows.UI.Core.CoreWindow` with a different PID, full-screen 1024x768 bounds
and topmost style. The Pulse fixture itself was active, topmost and at the
expected (160,160) bounds. This is evidence of external occlusion before locking,
not a failed pass-through transition or child-surface mismatch. The next log also
identifies that process by name; no system window is dismissed or modified.
ARM64 interactive lock verification remains open. Independent macOS adapter
verification may proceed without treating this Windows job as passed.

The macOS implementation borrows Avalonia's NSWindow handle and changes only
AppKit `ignoresMouseEvents`. It requires the main thread and membership in the
current NSApplication windows list before sending a window message, verifies
the property after each change, and neither retains nor releases the host's
window. Shared Lock/tray actions use the existing presentation lifecycle.
Native tests cover the property round trip, visibility, off-thread rejection,
closed-window rejection and tray unlock. Local Windows compilation/regression
cannot establish macOS behavior; both native architectures remain required.
The API contract is documented by
[Apple](https://developer.apple.com/documentation/appkit/nswindow/ignoresmouseevents).

Run `35064156631` passed Windows x64 and both Linux jobs. Both macOS jobs failed
the closed-window guard after the input-property round trip: AppKit can leave a
closed NSWindow in its windows list. The host now explicitly disposes its borrowed
input adapter on Closed, before any further native operation is allowed. The
closed-window assertion remains required; native verification of this fix is pending.
The Windows ARM64 pre-lock obstruction was identified as a foreign full-screen,
topmost `WWAHost` window. It is left untouched; this job remains failed and does
not establish ARM64 click-through behavior.

At `2732f8be0783e28edcf2bf325bfa05771060e370`,
[run 35064777018](https://github.com/medking82/hardware-pulse/actions/runs/35064777018)
passed both macOS jobs, including the input-property round trip, main-thread and
closed-window guards, floating-window tray unlock, and extracted `.app` Launch
Services checks. Windows x64 and both Linux jobs also passed. Windows ARM64 again failed before lock
because a full-screen `WWAHost` window covered the fixture; the failed result is
retained. AppKit property verification is not a physical game-input test, and
these checks do not complete Desktop overlay parity or authorize stable delivery.

The matching upstream runner report is
[actions/runner-images#14069](https://github.com/actions/runner-images/issues/14069),
still open when inspected on 2026-09-16. It documents Windows 11 ARM GUI tests
obstructed by the first-run privacy experience, including `WWAHost`. This supports
an environment diagnosis; it does not prove Pulse input behavior. Treat runner
desktop initialization as a separate CI change, not a reason to weaken the native
hit-test assertions or modify the installed user's desktop/security settings.

## Acceptance evidence

The X11 input increment is owned by the shared host's `X11WindowInput`: it borrows
the live Avalonia window, checks XFixes/Shape capability, shapes only the owned
client/render surfaces, and removes the window-manager frame through the host's
decoration property. It restores default input regions so later resize remains
interactive. Closed releases its connection; Core, sampling and installed WPF
settings are unchanged. The private Xvfb native test checks pointer routing to an
underlying fixture while the composed pixel stays visible, unlock/frame restore,
resize after unlock and closed-handle rejection. Local headless tests and
`Validate.ps1 -ModernCore` precede commit; Linux x64/ARM64 CI is required. Native
Wayland and physical desktop/game behavior remain unverified. Rollback is the
isolated host adapter/integration and native fixture change.

Initial X11 run `35066167507` failed on both Linux architectures in the existing
floating-window width assertion, before the pointer-routing fixture ran. Lock and
tray unlock returned successfully. The assertion previously sampled layout
immediately after requesting width 360; changing native decorations adds an
asynchronous WM resize. The diagnostic correction records client/text widths and
requires native client-width acknowledgement before the unchanged overflow check.
Until native CI passes, neither the resize hypothesis nor X11 pointer routing is
considered verified.

Run `35066793428` retained the Linux failure: requested width 360, actual client
440 and widest text 408 after three seconds. The wait only ran managed jobs and
slept; unlike the existing localization/tray fixtures it did not enter Avalonia's
native event loop. Both the resize wait and X11 pointer fixture now use the same
bounded `MainLoop` pump as those existing native tests. Original assertions stay
required. Verification remains pending; no additional Linux product scope is added
while Windows 7 has priority.

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->

| Area | Current evidence | Work before stable delivery |
| --- | --- | --- |
| Architecture / CPU / RAM / selected network | Native x64/ARM64 CI on Windows, Linux, macOS | Repeat on the exact release commit; document supported OS and counter limitations |
| Linux temperature / fans | Read-only hwmon fixtures and native graceful-absence tests; shared UI integration implemented with responsive headless tests | Verify native integrated UI and real exposed channels; do not infer physical coverage from an empty CI host |
| Other hardware / FPS / Desktop mode | Existing Windows WPF implementation only | Scope-dependent platform implementation and verification, or explicit unsupported capabilities |
| Settings / tray / lifecycle | Isolated settings, native language/theme/close/reopen/Tray command checks on all six targets, plus extracted-package launch | Validate shell-level menu interaction and intended desktop launch/download experience; command-handler tests do not cover those flows |
| Quota | Opt-in Codex file-login adapter, synthetic file/HTTP and UI tests | Verify intended supported provider scope without exposing credentials |
| Distribution | Six self-contained development archives, inventory, runtime and SHA-256 checks | Final package names/version, user launch/install instructions and platform installation behavior |
| Updates | Stable Windows updater uses latest stable GitHub release | Ensure publishing shared-host assets cannot break installed WPF update selection; choose explicit release channels/asset rules |
| macOS / Windows signing | No publisher signature; macOS not notarized | State the distribution policy and validate the download/launch experience; never disable OS security to pass a check |
| Performance | Short native process measurements and X11 allocation regression | Measure representative integrated workload; report scope and remaining physical-device gaps |
| Publication | Preview release assets independently verified | Freeze final source, run applicable checks, upload matching verified artifacts, verify remote metadata/digests and download paths |

Unsupported hardware must stay unavailable; absence cannot be represented as a
fabricated zero. Existing user settings and the Windows stable installation must
remain untouched by preview development. No new stable release has been made
by this readiness document.

## Existing Windows update-channel contract

Installed WPF builds query GitHub's `releases/latest`. Their coordinator selects
exactly one asset named `HardwarePulse-Setup.exe`, verifies its repository/tag
URL, size and SHA-256 metadata, and rejects draft/prerelease metadata. A release
containing only shared-host archives is therefore not a valid WPF update target.
Keep shared-only releases out of GitHub's latest selection. Do not reuse an older
WPF installer under a newer stable tag: its installed version would not match the
offered version and users could be offered the same update again.

Before a combined latest release, include a freshly built WPF installer matching
that stable tag and validate it independently of the six shared-host packages.
Shared Windows archives must not replace that installer asset. Existing installed
versions retain this contract; a future channel change requires an explicit
migration rather than merely changing the current source.

`UpdateCoordinatorTests` exercises a stable release with all six architecture
archives and checksum sidecars, placing the installer first, middle and last.
It verifies exact URL/tag/digest/size forwarding and refuses automatic download
when the installer is missing or duplicated. These tests use a fake downloader;
they do not run an installer or establish an installed upgrade result.

Linux sensor UI integration at `ada4f306c36ef70e32f5975d79be74e7167c44a7`
passed local Desktop render/interaction tests and `Validate.ps1 -ModernCore`.
[Native run 35046006965](https://github.com/medking82/hardware-pulse/actions/runs/35046006965)
passed all six packaged application jobs. This closes integrated native startup
validation for this change, not the remaining release scope decision or physical
sensor coverage.

Shared Session Max at `c3f547f65098770f0a9004899f2a5b2c7540ed0c` reuses
Core ReadingSession for CPU, selected network and Linux sensor peaks. RAM and
quota remain current, matching the existing Windows behavior. Local projection,
mode-switch, responsive rendering and full regression checks passed;
[run 35046859819](https://github.com/medking82/hardware-pulse/actions/runs/35046859819)
passed all six native package jobs. No stable release has been published from
these development commits.

Shared Network settings now offer explicit interface refresh without restarting.
Selection survives reordering; an absent saved interface stays unselected and is
restored when it reappears. Enumeration runs off the UI thread only at startup
or on request, with no additional sampling timer. Headless interaction tests cover
removal, reconnection, empty lists, refresh and selection persistence. Physical
USB/VPN hotplug across all platforms remains a separate device validation step.
The exact Network refresh commit `50d2ebc6c6b8ffeb9ea1ff024bd0333db5dc8f89`
passed all six native package jobs in
[run 35049167172](https://github.com/medking82/hardware-pulse/actions/runs/35049167172).

Development distribution now carries separate English and Simplified Chinese
launch guides, also retained inside the movable macOS bundle. The package
verifier rejects missing or empty guides even with an updated manifest; nine
package contract tests and an extracted Windows x64 live CPU/RAM smoke passed.
CLI help now accurately describes Linux hwmon and shared Session Max support.
These are source/package improvements, not a new stable cross-platform release.
All six native package jobs for guide commit
`d3ed72ef38dee672200728a5eaf0834b15ac6863` passed in
[run 35049537837](https://github.com/medking82/hardware-pulse/actions/runs/35049537837).

Shared hardware sampling now contains individual reader exceptions within the
existing one-second loop. Live values become unavailable, Session Max remains
historical, and a later successful sample restores live status. Repeated failures
retain the normal cadence; shutdown cancels the loop. Synthetic reader/UI tests
cover success/failure/recovery, mode switching during failure, repeated failures,
and strict smoke/measurement failure (exit code 3, no automatic retry).
`IMonitorSource` is the host's synchronous reader boundary; it owns no scheduling
and leaves platform adapter selection in `MonitorSource`.
Sampling recovery commit `87cca3956bf8a6fc781b286ccab380d5ce19d2d0` passed all
six native package jobs in [run 35049906632](https://github.com/medking82/hardware-pulse/actions/runs/35049906632).

Shared UI localization now supports Auto (System), English and Simplified Chinese
with immediate Monitor/Settings/sensor/quota/tray updates and isolated preference
persistence. Headless interactions cover system fallback, language round trips,
unchanged running sampling task and cached quota reuse. Windows renders at 360 px
were inspected for Monitor, Appearance and quota. Traditional Chinese is now
included with matching catalog keys/placeholders and script/region resolution.
Traditional Monitor/Appearance renders at 360 px were also inspected; saved choice
restoration and cached quota language round trips pass. Native cross-platform
font/interaction validation is not established by these Windows renders.
English/Simplified localization commit `7ff4362fdd3fd02ebf9d99747b35817ce457e3d9`
passed all six native package jobs in
[run 35050745266](https://github.com/medking82/hardware-pulse/actions/runs/35050745266).

Native smoke now emits `FONT_COVERAGE` for all three UI catalogs using the native
Avalonia font manager (normal and semibold weights). Windows x64 reports no
missing code points: 59 English, 204 Simplified Chinese, 209 Traditional Chinese.
This is catalog glyph availability, not proof for arbitrary device labels or
typographic quality. A missing glyph report is not a successful font gate even
if CPU/RAM smoke passes. Headless reports are separately labelled and cannot
substitute for native platform font evidence. The probe runs only in smoke,
not normal usage or measurement, to preserve the performance workload.
Traditional Chinese commit `e45231455b4ec2749909473b6d87fef6a8570c63` passed
all six native package jobs in
[run 35051139255](https://github.com/medking82/hardware-pulse/actions/runs/35051139255).
That run predates native glyph coverage logging and is not font coverage evidence.

Native coverage in run `35051508276` exposed a real Linux font gap: both Linux
architectures lacked 184 Simplified and 189 Traditional catalog code points,
despite successful telemetry smoke. Windows and macOS had no missing catalog
glyphs. The repair embeds pinned Noto Sans CJK SC/TC fonts, uses the corresponding
regional family for Chinese UI, and makes missing native catalog glyphs fail
smoke. Local Windows embedded-family, language-switch and native coverage checks
pass. Run `35052855090` at `88ae22248a30c29e5ee553dae8c76c9ed10b7abb`
then reported zero missing catalog glyphs natively on Linux x64 and ARM64,
and all Linux/macOS package jobs passed. Both Windows jobs passed native glyph
coverage but failed packaging because the workflow supplied extra CLI arguments.
The corrected Windows command passes local extracted-package smoke; the complete
matrix must be repeated on the correction commit. This failed run is not a
six-platform release gate. Font diagnostics remain outside performance measurement.

The correction at `411c90cb3fb3799e85de8261b1b6a0cd26c52d5e` passed all six
native build/package jobs in
[run 35053263263](https://github.com/medking82/hardware-pulse/actions/runs/35053263263).
This closes the bundled-font matrix gate, not the broader stable release gates.

The test runner now supports `--native-session`, reusing production platform
initialization with isolated test profiles and demo readings. It exercises actual
native windows through settings/theme/language changes, close/reopen persistence,
and Tray Open/Quit command handlers. This is separate from both headless rendering
and the extracted-package live telemetry smoke. It does not prove shell-level
tray icon placement, mouse interaction with native menus, download quarantine or
physical sensor coverage. Local Windows execution passes; six-platform native
session results are pending.

Run `35053670196` found a Linux-only narrow-layout assertion in the native session
test after settings restore and complete glyph coverage passed. The test requested
360 px then checked child bounds without waiting for the native window manager's
resize acknowledgement. The next revision records before/settled client and text
widths, waits for the requested client width, and keeps the overflow assertion.
Windows local checks remain green; Linux results are needed to distinguish a
test timing defect from actual layout overflow. The failed run does not close
the native session gate.

The resize diagnostic confirmed Linux X11 initially reported an 800 px client
with 752 px text after a 360 px request; after acknowledgement it reported a
360 px client and 312 px text. Both Linux architectures in run `35054413692`
then completed close, reopen and quota language phases, but failed the Tray
maximized-state assertion. That CI environment had Xvfb without a window manager.
The session check now starts Openbox in its isolated Xvfb display and requires
the EWMH window-manager readiness property before testing native window state.
This changes CI setup only; it neither installs a window manager for users nor
weakens the maximize/restore assertion. The earlier timeout remains recorded;
phase logging alone does not establish a fix for intermittent hangs.

Run `35054885669` with Openbox isolated the Tray assertion further: both before
and after Open the reported state was Normal, because the test did not await
the requested native maximize transition. The next revision requires activation,
minimize and maximize acknowledgements before testing Tray behavior, with bounded
timeouts and unchanged final state assertions. This fixes test sequencing; it is
not evidence of a production Tray repair until the native run confirms it.

Run [35055326312](https://github.com/medking82/hardware-pulse/actions/runs/35055326312)
passed all six native session and package jobs at
`ea2f7616178def7c09de5a51690d4294b11354c7`. Both Linux architectures reported
Maximized before and after Open, completed settings/language/close/reopen/quit
checks, and reported zero missing glyphs for all three catalogs. The earlier
Tray assertion was a test precondition/timing defect, not a demonstrated App
state-loss bug. One passing matrix does not establish long-duration stability.

The extracted Linux packages' roughly 61-second live measurements in that run
used 0.55% (x64) / 0.54% (ARM64) of a four-logical-CPU CI host and ended at about
186 / 195 MiB working set, with no collections in the measured interval.
These are Xvfb CI workloads, not physical desktop/game results or a claim of
zero host overhead. Native session checks are separate processes from measurement.
No cross-platform stable publication follows automatically from these checks;
the scope, distribution and feature gates above remain open.

The macOS extracted-package gate now additionally opens the exact `.app` through
Launch Services (`open -n -W`) with a fresh smoke session. It requires the App's
live CPU/RAM success marker, not merely a successful `open` exit, and rejects
reported App failures. Direct executable measurement remains a separate check.
The two negative output checks pass locally; native Intel/ARM64 bundle results
are pending CI. This does not establish downloaded-quarantine, signing or
notarization behavior. A timeout fails the CI job; the ephemeral runner owns
any remaining launched process and is discarded, rather than terminating an
unrelated installed App.

Run [35056501603](https://github.com/medking82/hardware-pulse/actions/runs/35056501603)
passed all six native jobs at `1559568dbc165e2bd8dd6f0a9f93cee7f52ea4fe`.
Both macOS jobs explicitly emitted `PASS macOS Launch Services bundle launch`
and the matching extracted-package success marker (Intel at 04:43:45 UTC,
ARM64 at 04:43:21 UTC on 2026-09-16). This closes the native `.app` launch-path
gate while retaining the download-quarantine/signing limitations above.
