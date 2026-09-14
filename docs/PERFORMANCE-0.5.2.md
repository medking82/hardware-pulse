# 0.5.2 UI allocation comparison

Measured on one Windows host, 16 logical processors, 2026-09-14. Baseline is
0.5.1 commit `05289e83cdbfff28c270d2e62ed7053b5c2fc695`, with the same new test
harness. Candidate adds fixed-pattern Regex reuse, frozen brush reuse and no
card rendering for hidden/minimized windows. Collector code and its two-second
sampling interval are unchanged. No forced GC or working-set trimming is used.

## Accelerated allocation workload

Run `NativeTests.exe <isolated-state-directory> perf` from each built app directory.
After 20 warm-up updates, each case writes and reads 300 fresh synthetic snapshots,
updates the WPF shell and pumps the dispatcher. Visible and then hidden cases run
in the same process. AppDomain allocation accounting includes the common fixture
writer and harness. It measures allocated bytes, not retained/live objects.

| 300 updates | 0.5.1 allocated bytes | 0.5.2 allocated bytes | Reduction |
| --- | ---: | ---: | ---: |
| Visible | 137,566,440 | 83,967,296 | 39.0% |
| Hidden | 137,563,496 | 37,012,040 | 73.1% |

An intermediate Regex-only run allocated 88,488,352 bytes visible and 88,480,008
hidden, isolating its contribution. Burst CPU times were noisy and did not show
a consistent reduction; they are not a steady-state CPU or battery-life result.

## Timed visible UI replay

Run `scripts/Measure-NativeUi.ps1 -AppDirectory <built-app-directory>` with
PowerShell 7 after compiling the NativeTests harness. Each sequential run uses
a 310 x 690 DIP window, 10-second warm-up, 30-second sample period and fresh
synthetic readings every two seconds. CPU divides process time by elapsed time
and 16 logical processors. Memory is the average of 15 samples.

| Metric | 0.5.1 | 0.5.2 |
| --- | ---: | ---: |
| Whole-machine CPU | 0.0226% | 0.0161% |
| Working Set, MiB | 113.86 | 115.97 |
| Private Bytes, MiB | 91.05 | 91.12 |

The test shows no meaningful resident/private memory reduction. The short CPU
sample does not establish a stable percentage improvement. Allocation reduction
is the demonstrated benefit; it must not be described as a 39–73% RAM reduction.
This UI-only comparison excludes live collector memory, game capture and reboot
behavior. Existing sensor differential tests and installed live snapshots cover
reading regressions, not cross-machine performance.
