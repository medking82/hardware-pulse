# 0.5.0 runtime comparison

Measured on one Windows development host on 2026-09-14, with 16 logical processors.
Baseline: 0.4.9, commit `b0abadc04a32cf1dd689c11600e987141fe78097`.
Candidate: the 0.5.0 native migration before release. Small subsequent startup ownership,
localization and regression-test changes do not establish a new benchmark measurement.

Each case ran sequentially, using a 10-second warm-up and approximately 60 seconds of
measurement. CPU is process CPU time divided by elapsed time and logical processor count.
Memory figures are mean MiB (1,048,576 bytes), not total system memory usage.

| UI replay, 310 × 690 DIP | 0.4.9 | Native candidate |
| --- | ---: | ---: |
| CPU, whole-machine percentage | 0.1550% | 0.0289% |
| Working Set | 195.72 MiB | 114.39 MiB |
| Private Bytes | 150.72 MiB | 91.11 MiB |
| Process launch to WPF ContentRendered | 4,172 ms | 1,015 ms |
| Valid mapped readings | 7 | 7 |

The replay uses identical synthetic CPU/GPU/pump/storage input refreshed every two seconds,
with isolated state and no live hardware collection. The native harness has an additional
two-second stop-check timer. This excludes live collector costs, game capture and battery
power. The native window is exercised by a test host; this is not installed-app cold boot.

| Live collector worker | 0.4.9 | Native candidate |
| --- | ---: | ---: |
| CPU, whole-machine percentage | 0.1470% | 0.1388% |
| Working Set | 180.83 MiB | 88.20 MiB |
| Private Bytes | 158.15 MiB | 65.30 MiB |
| First snapshot | 5,460 ms | 4,615 ms |
| Mapped readings / capabilities | 17 / 17 | 17 / 17 |

The installed collector was stopped before sequential read-only runs and restored afterward.
Both implementations returned the same capability keys. Instantaneous values naturally differ
across time. This excludes the UI and the baseline's passive executable wrapper.

The substantial observed improvement is lower memory use; the collector CPU difference is small.
These single runs do not prove battery-life gains, lower game latency, results on every PC, or
zero resource usage. Do not add the independent UI and collector runs and label that sum a
measured whole-app result. Driver/library costs remain after removing PowerShell.

Reproduce with `scripts/Compare-UI.ps1` and `scripts/Compare-Collectors.ps1`, preserving a built
0.4.9 package at ignored `vendor/baseline-0.4.9` first. The collector benchmark requires elevation
and briefly stops the installed Pulse. Raw snapshots, machine identifiers and local result
directories are deliberately not published.
