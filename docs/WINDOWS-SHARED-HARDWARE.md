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

## Admission

This assessment covers read-only snapshot consumption and presentation only.
Collector lifecycle, peer identity and installer changes need their own assessment.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
