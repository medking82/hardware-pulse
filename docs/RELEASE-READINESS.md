# Multi-platform stable release readiness

The delivery target is a public stable release with usable, verified packages,
not merely pushed adapters or a renamed experimental release. Existing Windows
WPF v0.6.26 remains stable; v0.7.0-preview.1 is still experimental.

## Scope decision

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

## Acceptance evidence

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
