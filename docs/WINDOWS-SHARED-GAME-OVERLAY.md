# Shared Windows game overlay

## Frozen implementation boundary

Restore the WPF game overlay behavior in the shared host: independent opt-in,
same selected application as FPS, foreground client-area anchoring, hide on
minimize/foreground loss, nonactivating pass-through window, six positions,
compact/detailed text, background color/opacity and independent FPS, CPU, GPU,
memory, fan and storage presentation. Use current readings, not Session Max.
Exclusive-fullscreen and real games require device acceptance; do not promise
injection or universal game support.

One FpsPanel session owns target selection and optional capture. Extend the
Windows source to publish target HWND/name with its metrics in one immutable
sample; hardware-only overlay resolves a target without opening a FPS pipe.
No second collector, hardware sampler, credential reader or target selector.
Monitor supplies its existing snapshot even when hidden. Overlay options are
new optional shared-profile fields, default off; no WPF migration change in
this slice. Preserve existing unknown-field persistence and FPS cancellation.

Windows adapter reads target bounds and changes only the owned overlay's input
policy through WindowsWindowInput. UI owns window, rendering and lifetime.
Disabled overlay closes its window and releases tracking; hidden target keeps
the selected session but does not display stale content on another window.
Close cancels/disposes all owned work. Unsupported platforms remain disabled.

Acceptance: fake-source session target/metrics identity, hardware-only capture
off, disable/re-enable/late-result behavior; settings round trips and bounds;
all displayed metric groups, unavailable readings, six anchors, localization;
native foreground/minimize/move/pass-through/no-activation fixtures; full
headless/native suites and repository validation. Real game/display performance
is still a final installed acceptance gate. Do not modify the installed app,
quarantined verifier, startup/tasks, driver policy or current preview version.

## Implemented evidence

`GameOverlayPanel` owns opt-in controls/window lifetime; `FpsPanel` owns the
single serial source session. `WindowsFpsSource.PollGame` resolves the target
once and publishes HWND/name with metrics. Hardware-only tracking leaves its
FPS client absent; enabling capture creates it, while disabling capture
disposes it. Overlay visibility remains separate from FPS capture opt-in.
The UI states that FPS capture must be enabled separately. Closing the main
window disposes the overlay and the shared session.

Focused tests cover all metric groups, compact/detailed output, unavailable
values, six anchors including negative coordinates, normalization, persisted
controls/reset, hardware-only capture off, combined FPS snapshots and rejected
late target results. Native fixtures cover real owned windows, foreground
loss/minimize/restore, movement, no-activation, native hit-test pass-through and
all six positions. `vendor/game-overlay-native.png` was visually inspected.

Build passed with zero warnings/errors. Complete headless and Windows native
Desktop suites passed (`vendor/test-game-overlay-headless.log` and
`vendor/test-game-overlay-native-session.log`); subsequently added controls and
late-result cases passed `--game-overlay` (`vendor/test-game-overlay-focused.log`).
Repository `scripts/Validate.ps1` passed under PowerShell 7
(`vendor/validate-game-overlay.log`), and `git diff --check` passed.
The initial render-export build used an obsolete bitmap overload; using the
supported PNG encoder resolved it. Initial controls fixtures included template
children and read before queued text-change delivery; named selectors and
normal dispatcher processing resolved those fixture defects without changing
the assertions or production behavior.

Real game capture/exclusive-fullscreen support, installed performance and
multi-display acceptance remain unverified. Existing WPF overlay preferences
are preserved in their source file but are not imported into the new options
by this change. The current preview version and installed WPF app are unchanged.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
