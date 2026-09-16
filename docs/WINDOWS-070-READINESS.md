# Windows x64 shared-host 0.7.0 release gate

Decision, 2026-09-16: Windows x64 0.7.0 stable must ship the shared host after its
Windows features and acceptance are complete. A WPF version bump or promotion
of the existing preview does not satisfy this decision. Windows delivery may
proceed independently of macOS. Preserve the installed WPF app and its settings
while developing and verifying the separate preview profile.

The current Windows WPF implementation is the behavior reference. Core is already
extracted; do not restart componentization. Complete bounded changes in sequence
and verify each owner before freezing the final release commit.

| Gate | Current shared-host evidence | Remaining acceptance |
| --- | --- | --- |
| Antigravity quota (P02) | Windows provider reused; independent opt-in and Monitor/floating projection; full Validate, headless UI, native Windows session and extracted x64 package pass | Repeat on the final stable commit; live account compatibility is distinct from fake-source regression |
| Hardware | CPU/RAM/selected network plus read-only existing collector snapshot integration; the WPF SensorProfile supplies GPU, temperatures, fans, voltages, NVMe, VRAM usage and link/signal data to both views | Ship and manage the collector with the x64 shared installer; verify final payload and real hardware without changing privilege separation |
| Cards/Desktop layout | Fixed basic cards and floating rows | Visibility, order, adaptive columns; stable resizing and live updates |
| FPS | Not connected | Existing PresentMon process/capture ownership, settings, Monitor/Desktop display and real capture verification |
| Desktop integration | Floating geometry, lock, topmost, tray, opacity, colors | Desktop layer placement, global shortcut, background sampling, Local Contrast and screenshot behavior |
| Lifecycle | Close-to-tray and one sampler after restore | Single instance, startup/restore mode and registration-conflict feedback |
| Updates/distribution | Separate preview archives | Windows stable installer/update asset selection, settings migration policy, install/uninstall and rollback verification |
| Release quality | Existing tests are incremental evidence | Exact-commit native checks, CPU/RAM/soak/background restore measurements, separate-language documentation and verified downloadable stable assets |

Windows ARM64 interaction remains unverified and is not automatically included in
the x64 release. The active release objective explicitly excludes ARM64.
macOS/Win7 gates retain their separate status. Do not relabel
unverified or missing behavior as supported to close this table.

## CPU and memory acceptance

The user explicitly requires lightweight CPU/RAM behavior. Compare the final
shared host against the WPF baseline on the same Windows hardware, window size,
refresh cadence and enabled features. Measure Monitor, floating Desktop and
hidden tray modes separately; include quota on/off, FPS and Local Contrast
on/off, long-running memory growth and restore/disposal behavior. Report UI,
collector and helper costs separately and together. Do not infer improvement
from framework choice, file count, installation size or unmatched benchmarks.

Reuse snapshots and acquisition owners. Disabled optional features must not read
their source. Diagnose material regressions before stable release; do not conceal
them with forced GC, working-set trimming or reduced feature coverage. The
existing short Monitor measurement is useful incremental evidence, not a full
parity, soak or performance acceptance result.

P02 working-tree Monitor sample (2026-09-16, Windows x64, 16 logical CPUs):
10-second warmup followed by 60.99 seconds with 61 live CPU/RAM/network polls,
quota off and no floating/FPS/Local Contrast. Whole-machine CPU averaged 0.123%;
working set moved from 139.15 to 141.97 MiB; managed allocation was 1.22 MiB with
no GC collections during the measured interval. Raw local evidence is
`vendor/measure-p02-monitor.log`. This single short sample is not a WPF comparison
or evidence of long-term memory stability.

## P02 admission evidence

This assessment covers P02 only: source reuse and presentation wiring preserve
the existing credential/privilege boundary. Later Windows gates require their
own grounded classification; this is not release approval.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"sensitive","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
