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

GameOverlayWindow reads target bounds through read-only Windows calls and changes
only the owned overlay's input policy through WindowsWindowInput. UI owns its
window, rendering and lifetime.
Disabled overlay closes its window and releases tracking; hidden target keeps
the selected session but does not display stale content on another window.
Close cancels/disposes all owned work. Unsupported platforms remain disabled.

Acceptance: fake-source session target/metrics identity, hardware-only capture
off, disable/re-enable/late-result behavior; settings round trips and bounds;
all displayed metric groups, unavailable readings, six anchors, localization;
native foreground/minimize/move/pass-through/no-activation fixtures; full
headless/native suites and repository validation. Real game/display performance
is a final installed acceptance gate. Do not modify the installed app, quarantined
verifier, startup/tasks, driver policy or current preview version.

## Current restoration

The baseline WPF behavior in Native/Services.cs and OverlayAppearance.cs owns
capture semantics and appearance controls. Selecting FPS in the enabled overlay
requests capture independently of the Monitor FPS card; either consumer keeps the
single FpsPanel session alive. Turning off both capture choices while the overlay
is enabled retains target tracking without a pipe client. Its snapshot carries
target HWND/name with metrics, with stale target generations rejected.

MonitorWindow supplies its existing current hardware snapshot. OFFLINE hardware
never renders retained values as live. No extra hardware collector or quota reader
is added. Options persist in the isolated shared profile. Color and opacity affect
only the background, and a preview uses the same brush as the overlay. The pinned
Avalonia.Controls.ColorPicker 12.1.2 component replaces the historical prototype's
hex-only textbox; it uses the existing Avalonia MIT notices.

The original FPS Settings section is retained, with Game Overlay alongside it in
the existing adaptive section layout. English, Simplified Chinese and Traditional
Chinese labels share the existing localization owner. The source changes are
limited to the Windows FPS adapter, Desktop host/persistence/UI resources, their
tests and this document. Rollback is the corresponding commit; no installed state
or WPF preferences are changed.

Historical commit 3653143 supplies the initial overlay implementation and fixtures,
adapted to current normalized Reading data, compact UI, independent capture
requests and color picker. Its historical test claims are not current evidence.

## Verified in this restoration

The complete shared headless suite and repository Validate.ps1 passed. Additional
focused overlay tests passed after adding the capture handoff cases: overlay FPS
off keeps hardware-only tracking without capture, Monitor FPS can independently
keep capture active, and disabling the final consumer completes the worker.
Tests also cover option persistence, stale hardware suppression, six anchors,
cancelled late targets, appearance reset, keyboard opening of the real color
picker and 240 DIP layouts in all three languages. An initial keyboard fixture
incorrectly focused the ColorPicker container; the actual template button is the
keyboard entry point and now opens its flyout through Space.

Windows native fixtures passed with real owned windows: foreground preservation,
hit-test pass-through, six positions, movement, foreground loss, minimize and
restore. The native overlay render and integrated 360 DIP FPS Settings render
were inspected, together with the narrow appearance controls. Locked dependency
restore and git diff --check passed. Existing updater CS0649 warnings remain.

These fixtures use synthetic frame readings. Real games, exclusive fullscreen,
mixed-display DPI transitions and installed performance remain acceptance work;
this change is not a release or a claim of complete cross-platform parity.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
