# Shared Windows FPS restoration

Restore the existing FPS integration from `3b1ea5b` while retaining the 0.6.27
presentation baseline. This boundary covers opt-in acquisition, target selection,
Reset, shared Monitor/Desktop readings and Settings persistence. Game-overlay
appearance, installer/worker registration and real-game acceptance remain separate
required release work. No installed files, scheduled tasks or personal profiles change.

The modern Windows adapter links the existing FpsTransport and OverlayTarget. The
transport change adds modern-platform guards and excludes FpsServer from the modern
client assembly; Framework collector behavior, fixed messages, SID/session ownership,
process-birth checks and exact peer executable checks are unchanged. The client
expects `worker/HardwarePulse.Collector.exe` under the AppHost directory. It does not
launch a privileged process or accept a caller-supplied executable path.

One FpsPanel session owns the adapter off the UI thread. Disabled profiles start no
discovery, sampling or pipe. Reset and target selection clear old values; re-enabling
waits for prior disposal. Both views consume its immutable snapshot. Desktop follows
the original Current / Average / Minimum grouping and default position after network
metrics. App keeps 1% Low unavailable until the source supplies it. Session Max must
not turn FPS into a hardware maximum.

Presentation uses compact shared card colors and a separate FPS Settings section,
with a synchronized quick toggle. The original oversized prototype card is not the
visual baseline. Existing WPF UI, Panel.xaml and artwork remain untouched; only the
transport compile guards described above change inside Native.

Required checks: three-stage FPS lifecycle fixture, owner quick/Settings synchronization,
one-source Desktop projection, 240/360 DIP renders, modern protocol identity tests,
full shared headless/native-session suites and repository Validate.ps1. Synthetic
readings do not prove actual game capture. Missing matching collector remains explicit.

These checks now pass locally. The owner fixture also closes during a held poll and
verifies disposal exactly once with no late reading. An initial Desktop-order failure
was resolved by placing FPS after network readings, as Native/DesktopOrder specifies,
without changing the existing reorder assertion. The 240 DIP/16 DIP-font render exposed
a clipped target label; data-templated wrapping now preserves its full text and theme
foreground. Both that Settings render and the 360 DIP Monitor render were inspected.
The final targeted FPS fixture passes after the template correction. Existing updater
CS0649 warnings remain in Validate.ps1; no new warnings were introduced.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
