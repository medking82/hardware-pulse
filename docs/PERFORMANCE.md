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

## UI and Local Contrast baseline (0.6.24)

Frozen runtime: `d58919f9f41215fae7f065727144c0b654e8af73`, version 0.6.24.0.
App SHA-256: `b9ab53d19b1d6d6964ea574b1e57463f763e90f0c24ded66abbc30c2a69f745a`.
These are current-version baselines, not a before/after optimization comparison.
CPU percentages below use all 16 logical processors as 100%.

Monitor: three sequential fresh NativeTests bench processes, isolated state,
310×690 DIP window, 10-second warmup and requested 30-second measurement
(actual 30.27–30.36 s), 15 memory samples per run. A synthetic snapshot changes
every two seconds. This scene excludes live collector, FPS and Local Contrast.

| Monitor harness measurement | Three-run range |
| --- | --- |
| Whole-machine CPU | 0.103–0.116% |
| Mean working set | 123.22–123.33 MiB |
| Mean private bytes | 94.69–94.82 MiB |

Local Contrast: three subsequent fresh PowerShell 5.1 STA processes, fixed
black/white/gray gradient, transparent front window, 320×850 DIP at 150% scaling
(480×1275 captured pixels; 240×638 analysis mask). Eight warmup captures followed
by 120 captures, target interval 100 ms; actual elapsed 13.35–13.49 s.

| Capture/analysis harness measurement | Three-run range |
| --- | --- |
| Whole-machine CPU | 0.688–0.779% |
| Mean capture elapsed | 12.21–12.56 ms/frame |
| CPU time | 12.37–13.93 ms/frame |
| Reported allocation | 181,111–181,316 bytes/frame |
| Mean working set | 173.41–174.00 MiB |
| Mean private bytes | 180.72–184.38 MiB |

The capture harness includes PowerShell/WPF host overhead, excludes complete
Desktop rendering, and runs a synthetic gradient. Its process RAM is not the
incremental cost of enabling Local Contrast. Do not add these two harness rows
or treat them as the installed App's current usage. Short runs cannot establish
absence of leaks. Live collector, matched Desktop contrast-on/off scenes,
resizing, monitor/DPI changes and game frametime remain unmeasured by this batch.

Reproduce using Measure-NativeUi.ps1 (PowerShell 7) and Measure-LocalContrast.ps1
(PowerShell 5.1 STA), run sequentially. They now emit runtime hashes, scope and
warmup/interval metadata and reject invalid sample durations/counts. UI sampling
fails if its test process exits early. Measurement-only metadata changes have no
installed runtime effect. Raw baseline evidence is kept locally under vendor as
ui-baseline-0.6.24.json and contrast-baseline-0.6.24.json.

## Matched Desktop Local Contrast off/on (0.6.24)

Use `Measure-NativeUi.ps1 -AppDirectory <built-app> -Scene desktop` and
`-Scene desktop-contrast`. NativeTests must be rebuilt with the scene-aware
harness. The wrapper rejects a mismatched ready marker and records actual DIP/
pixel bounds, requested/active contrast, runtime and harness hashes. The test
process checks contrast availability during the run and exits if it changes.

Runtime source remains `d58919f9f41215fae7f065727144c0b654e8af73` (0.6.24).
The measured build has App SHA-256
`0ec363603cc51ffd629ea433ef72dc05b5e45cb588e7d3cc9176c49f58a597dc`;
all six runs used the same App/Core/adapter binaries. NativeTests uses isolated
state and the shared synthetic Snapshot fixture; no installed preferences change.

Three fresh-process off/on pairs, sequentially. Each warms up 10 seconds, then
measures 30 seconds with 15 memory samples. Fixed locked, topmost DesktopView,
320×850 DIP / 480×1275 pixels, one column, 16 DIP font, 6 DIP spacing, 100% text
opacity, 0% background opacity, Auto Contrast off. Only Local Contrast differs.
Both use the same static black/white/gray gradient window behind the panel and
synthetic sensor updates every two seconds. CPU uses all 16 logical processors.

| Pair | CPU off | CPU on | CPU delta (percentage points) | Private-memory delta | Working-set delta |
| --- | --- | --- | --- | --- | --- |
| 1 | 0.029% | 0.487% | +0.458 | +13.51 MiB | +22.84 MiB |
| 2 | 0.032% | 0.406% | +0.374 | +13.54 MiB | +21.84 MiB |
| 3 | 0.016% | 0.432% | +0.416 | +15.44 MiB | +23.03 MiB |

These are per-process mean-memory differences, not retained-object sizes or a
whole-system impact estimate. Short-run CPU varies and no confidence interval is
claimed. This includes real DesktopView rendering and Local Contrast's async
capture/analysis timer. Isolation bypasses the Windows wallpaper-layer adapter;
live collector, quota network calls and game/FPS capture are absent. The backdrop
is static. GPU/DWM cost, moving high-frequency backgrounds, resizing, DPI changes,
long-duration leaks and game frametime still need separate matched measurements.

This measurement-only iteration changes no runtime, sampling interval, defaults
or release binary. Local raw evidence: vendor/desktop-paired-0.6.24.json. The
original monitor benchmark remains available through the default `monitor` scene.

## Moving-background probe (0.6.24; incomplete comparison)

Use `Measure-NativeUi.ps1 -Scene desktop-dynamic` and
`-Scene desktop-dynamic-contrast` with the same AppDirectory. Both animate the
same gradient endpoints with a sinusoidal offset (amplitude 0.35, period eight
seconds), requested Dispatcher interval 33 ms. The completion marker records
actual background updates; the wrapper rejects a nonmoving dynamic scene and
retains stderr locally. The harness reports capture/render first-chance exceptions
without adding instrumentation to the shipped App.

The initial three-pair attempt stopped in pair 2 when LocalContrastAvailable
became false. Do not treat the partial run as a completed performance baseline:

| Completed sample | CPU | Mean private memory | Mean working set |
| --- | --- | --- | --- |
| Pair 1 off | 0.077% | 143.40 MiB | 128.58 MiB |
| Pair 1 on | 0.448% | 120.79 MiB | 147.59 MiB |
| Pair 2 off | 0.081% | 142.96 MiB | 128.61 MiB |

All used 10 seconds warmup, 30 seconds measurement, 16 logical processors,
320x850 DIP / 480x1275 pixels and the static comparison's other settings.
Runtime App SHA-256 was
`233007e88c0bdab86eb803e4f90fd3448a28237d9d90e11de34e4693b1b508ae`.
The animation ran 894-897 updates per completed process. The background renderer
is in the measured process; GPU/DWM work is not included. Private-memory reversal
between these short fresh-process runs is not proof of retained-memory savings.

After adding diagnostic logging, one 30-second on run passed (0.515% CPU,
120.54 MiB private, 148.84 MiB working set, 895 background updates). It does not
replace the failed pair or prove the intermittent problem fixed. The observed
failure can arise from capture returning no image, a capture-task exception or
an ArgumentException while applying adaptive styles; the previous harness did
not record enough evidence to distinguish these. Root cause and a stable
reproduction remain open. No runtime fix or release is claimed.

Local partial evidence: vendor/desktop-dynamic-paired-0.6.24.json;
diagnostic attempt: vendor/dynamic-diagnostic.log. Complete the matched dynamic
comparison only after resolving the unavailable-state observation. Game frametime,
wallpaper-layer integration and long-duration memory remain separate checks.
