# Local Contrast performance

## Reproducible component measurement

Measured on Windows x64 with 16 logical processors on 2026-09-15. The baseline is
the unchanged v0.6.17 build (84e6e31); the candidate extracts ContrastAnalysis into
Pulse.Core and optimizes the Windows adapter. The installed application was not
replaced for this measurement.

`scripts/Measure-LocalContrast.ps1` creates isolated WPF windows over a synthetic
black/white/gray gradient, excludes the foreground from capture, warms up eight
frames and measures 120 frames with a 100 ms target interval. At the tested DPI,
320 x 850 DIP becomes a 480 x 1275 pixel capture. It writes aggregate statistics
to stdout and does not write captured pixels or modify application settings.

Run each build in a fresh Windows PowerShell 5.1 STA host through Run-Hidden.ps1
from PowerShell 7. Pass the corresponding complete app directory with `-AppPath`.
Keep other benchmark/build activity idle during the comparison. CPU percentage
is normalized over all logical processors; allocation includes harness overhead.

| Measurement | v0.6.17 baseline | Candidate |
| --- | ---: | ---: |
| Analysis grid | 480 x 1275 | 240 x 638 |
| Mean capture + analysis wall time | 104.155 ms | 13.554 ms |
| Process CPU time per frame | 103.385 ms | 13.932 ms |
| Whole-machine CPU during sampling | 5.8367% | 0.7730% |
| Managed allocation per frame | 2,616,370 bytes | 176,469 bytes |
| Mean working set | 175.64 MiB | 176.98 MiB |
| Mean private memory | 183.29 MiB | 186.39 MiB |

This short component benchmark shows about 87% lower CPU time per frame and 93%
less managed allocation. It does **not** demonstrate lower resident memory,
whole-app/game CPU cost, battery savings or performance on other machines.
Working set and private memory were slightly higher in this sample; no forced GC
or working-set trimming was used. Repeat longer installed-app measurements before
setting a product-wide resource budget.

## Changes and preserved behavior

- Reuse native bitmaps, Graphics instances and the managed pixel buffer until
  capture dimensions change; dispose native buffers on disable or resize.
- Bound analysis to 160,000 pixels. Capture still covers the full panel, while
  spatial smoothing uses the corresponding scaled radius. Text is rendered at
  its original resolution, independently of the analysis grid.
- Cache the composited luminance lookup tables and query region histograms via a
  summed-area table instead of copying pixels for every label and value.
- Retain foreground brushes when the chosen color is unchanged.

The capture interval, palette thresholds, hysteresis, thin-edge policy, capture
exclusion and saved preferences remain unchanged. Regression checks cover actual
dark/light capture, bounded large captures, resize, dispose/resume, Screenshot
mode, stable brush reuse and the existing Desktop input/zero-opacity behavior.

## Remaining measurements

Measure the installed UI, collector and FPS helper separately over longer periods
with identical locked state, panel size, visible metrics and foreground game.
Include Local Contrast on/off, sustained resizing, mixed high-frequency textures
and DPI/monitor changes. Full-screen game latency, GPU usage and ARM64/platform
performance remain unmeasured. A monitor cannot have zero host overhead; optional
features should have explicit ownership and measured costs before adding cadence
or caching complexity.

## Core extraction FPS microbenchmark (2026-09-15)

Baseline: release commit `29885172976a2c0820efd493fcadd8b967caab60` (0.6.22).
Candidate: `1c9c93c`, after CORE-01 through CORE-04. Both use optimized Framework
builds and the same FrameCapture API, without starting PresentMon. Each fresh
process receives 14,400 frames at 240 FPS over 60 seconds, performs 20 warmup reads,
then 200 reads at timestamp 60. Three interleaved baseline/candidate process pairs;
no forced GC or working-set trimming. All reads check Ready, Count and Current.

| Measurement | Baseline range | Candidate range |
| --- | --- | --- |
| CPU ms/read | 1.25–1.41 | 1.17–1.41 |
| Elapsed ms/read | 1.27–1.37 | 1.31–1.43 |
| Allocated bytes/read | 936,272 | 936,272 |
| Final process private bytes | 24,047,616–24,080,384 | 24,109,056–24,129,536 |

Ranges overlap and allocations are identical. This bounded statistics workload
shows no clear CPU regression or improvement; the extra Core assembly is present
in the candidate process. Absolute values are specific to this synthetic workload.
It does not measure live capture, UI/collector RAM, local contrast, game frametime,
long-duration leaks or another architecture/OS. PERF-01 remains incomplete until
those matched scenes are measured. Raw local evidence: vendor/core-batch-bench.

## FPS reusable statistics buffer (0.6.24)

Run `scripts/Measure-FpsHistory.ps1` in PowerShell 7. Its default baseline is
`51ef6d8dc61534c5896ccc18a869bb945202eebc` (0.6.23). Both sides compile optimized
Framework x64 executables with the same harness. Each fresh process receives
14,400 frames at 240 FPS over 60 seconds, warms up with 20 reads, then measures
200 reads at timestamp 60. Three interleaved pairs, no forced GC/working-set trim.
Native frame capture is not started. Raw results stay in the printed vendor path.

| Measurement | Baseline range | Reusable-buffer range |
| --- | --- | --- |
| CPU ms/read | 1.25–1.41 | 0.2344 |
| Elapsed ms/read | 1.27–1.47 | 0.23–0.26 |
| Reported allocated bytes/read | 936,232 | 40 |
| Final process private bytes | 24,059,904–24,096,768 | 17,174,528–17,231,872 |

Allocation uses Framework AppDomain monitoring; the small remaining value should
be treated as a reported average, not an exact object-size inventory. This shows
substantially lower statistics-path allocation and CPU in the measured workload.
The buffer retains up to 720,000 payload bytes per FrameHistory and is released on
Clear. No refresh interval changed. Whole-app UI/collector RAM, Local Contrast,
long-duration leaks and game frametime are not established by this microbenchmark;
the remaining PERF-01 matched scenes still need measurement.
