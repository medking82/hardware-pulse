# Shared Windows hardware integration

The x64 shared host now consumes the existing collector's current-user runtime
snapshot through `WindowsSnapshotReadings`. It reuses `SensorProfile.Parse`
and the Windows DTOs instead of introducing another sensor mapping or loading
LibreHardwareMonitor in the UI. `ReadingSession` owns peaks and stale topology;
the existing Monitor worker is the sole caller. Floating Desktop receives the
same display snapshot and performs no file reads or acquisition.

Modern JSON input is limited to 8 MiB and depth 64. Missing, malformed, changed,
unsupported-schema and stale input is unavailable, never fabricated as zero.
Valid zero RPM remains zero. Current VRAM usage and physical link rates remain
current in Session Max; historic sensor peaks remain explicitly separate.
Hardware names remain data and are never interpreted as paths or commands.

The source includes CPU/GPU temperatures, GPU utilization, motherboard/core
voltages, fan channels, memory-module/NVMe temperatures, VRAM usage, physical
LAN/Wi-Fi link rates and Wi-Fi signal. The adapter retains the WPF discovery
rules and their device limitations. CPU/RAM/selected network system counters
continue to function independently of collector availability. A missing
collector does not grant permission to elevate or start a second one.

Collector startup, distribution and lifecycle remain a release gate. This
increment can use an already-running installed collector; it does not install,
launch or modify one. The preview profile and installed WPF settings remain
separate. Rollback reverts only this adapter/host wiring.

Regression uses synthetic snapshot files and checks the existing mapping,
JSON bounds, zero, current/peak separation, stale clearing in both windows,
stable controls and narrow layout. A separate read-only local check observed
`LIVE` with 23 valid metrics; no hardware identifiers or raw snapshots were
exported. Full `Validate.ps1 -ModernCore`, Desktop headless regression and native
Windows session checks passed. Local evidence is retained in
`vendor/validate-windows-hardware-final.log`,
`vendor/test-windows-hardware-final.log` and
`vendor/test-windows-hardware-native.log`. These validate the adapter and both
consumers, not the still-missing standalone collector distribution.

A live Monitor sample after a 10-second warmup measured 61 polls over 61 seconds:
0.128% whole-machine CPU on 16 logical CPUs, working set 149.49 to 158.77 MiB,
19.52 MiB managed allocations, one Gen0 collection and no Gen1/Gen2 collections.
Quota, FPS and Local Contrast were off. The source was the existing collector;
these figures measure the UI process only, not combined collector cost.
`vendor/measure-windows-hardware.log` retains the sample. It is neither a matched
WPF comparison nor long-running memory acceptance.

During full validation an existing WPF zero-opacity input assertion failed once.
Diagnostic HWND/process/style output was added without changing the assertion.
The narrow diagnostic run and three subsequent runs passed. The initial failure
is retained at `vendor/validate-windows-hardware.log`; its root cause remains
unproven. Do not label this as a fixed product bug or silently discard the failure.

## Explicit live-source acceptance

`Pulse.Desktop.Tests --windows-hardware-live-native` exercises the existing
current-user collector through `WindowsSnapshotReadings.Default()` in native
Monitor and floating windows. `--windows-hardware-live` uses the headless
backend. These opt-in commands are not included in ordinary CI: they require
fresh real temperature and fan channels and never start, elevate or configure
a collector. The test's Monitor uses a disabled sampling worker and no settings
store; one test-owned `ReadingSession` supplies both consumers.

Four readings 2.2 seconds apart must stay LIVE, contain finite values and show
at least three distinct source identities. Every hardware row's value is checked
against the shared display snapshot in each window, including its matching
Desktop row ID. A future-clock read then requires STALE, preserved row topology,
all live values cleared to em dashes and retained session history. This advances
only the test's clock argument; the collector, its source file and installed
application are untouched. Logs whitelist generic metric IDs and formatted
values; no raw snapshot, device label, SID or source identity is exported.

The native run passed locally (`vendor/test-live-hardware-native.log`). All four
reads had 20 hardware presentation metrics and source ages 1.85–1.93 seconds.
Observed CPU temperature was 53.8–55.3 °C, GPU temperature 46.9–47.7 °C and
CPU fan 743–785 RPM. Both GPU fans reported valid zero RPM. Voltage, memory-module
and motherboard temperatures, two system fans, two NVMe temperatures, VRAM
usage and LAN/Wi-Fi link/signal rows also matched in both consumers. The final
stale check cleared all 20 live values.

This verifies real collector ingestion and value propagation through native
controls. It does not calibrate sensor accuracy against an independent device,
establish visual legibility on every display, install the new worker or prove
final packaged lifecycle/performance. Synthetic regression remains the
independent expected-value check for mapping and units. Allowed follow-up scope
is the explicit acceptance entry points and this evidence; production source,
privileges, settings and sampling remain unchanged. Rollback is a source revert.
The build had zero warnings/errors (`vendor/build-live-hardware.log`), original
focused hardware regression passed (`vendor/test-live-hardware-regression.log`),
and repository validation passed (`vendor/validate-live-hardware.log`).

## Admission

This assessment covers read-only snapshot consumption and presentation only.
Collector lifecycle, peer identity and installer changes need their own assessment.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
