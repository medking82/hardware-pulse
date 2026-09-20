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

### Follow-up diagnostics and partial-sample retention

A subsequent 90-second dynamic on probe (10-second warmup, 45 samples) completed
without reproducing unavailable: CPU 0.516%, mean private memory 121.87 MiB,
working set 145.05 MiB, 2164 background updates. App SHA-256:
`4d82edd7bcd2f7fc42ed8e941f73bcbf9f6b1dd3bfbd8b47a2836d6cfd5aa553`.
This is an unpaired observation, not evidence of a fix or reduced memory usage.
Local evidence: vendor/dynamic-diagnostic-90s.log.

Measure-NativeUi now retains samples.json on success and failure. Each measured
sample includes elapsed seconds, cumulative process CPU seconds, working-set
bytes and private bytes. The envelope records binary/harness hashes, processor
count, completion and failure state. It is written after sampling, avoiding disk
writes for telemetry in the measured loop. stderr.log remains the exception
record. Abrupt termination of the measuring PowerShell process itself cannot
ensure this finally-based persistence.

Verification: a two-second monitor smoke retained one successful sample; a
controlled early exit through the isolated harness's BENCH-STOP marker failed
measurement as expected, preserved 12 partial samples and emitted no result.json.
This tests evidence retention, not reproduction of the contrast defect. Installed
settings and runtime behavior are unchanged. The intermittent capture/render
failure still requires a specific exception or null-image cause before a fix.

## Linux procfs polling measurement

The Linux prototype now reads only the aggregate first line of `/proc/stat`;
CPU parsing, RAM polling and host cadence are unchanged. The previous full-file
strategy remains reproducible in the benchmark through the existing injected
`File.ReadAllText` source. This comparison isolates source reading rather than
comparing unrelated builds.

Run LinuxAdapterTests with `--live --measure` on Linux. It alternates three pairs
of full-stat / first-line CPU+RAM readers, each with 100 warmup and 2,000 measured
polls. Tiered compilation is disabled in CI for repeatability. JSON log lines
record process architecture, runtime, CPU count, procfs text size, managed bytes
allocated per poll, elapsed/process CPU time per poll, GC counts and invalid
readings. Invalid readings fail; noisy timing does not use a pass/fail threshold.

These are tight-loop adapter measurements, not an App working-set benchmark or
an estimate of game frametime. Allocated bytes are not retained RAM. The adapter
still has no timer or worker, and is not loaded by the Windows installer.
Observed on 2026-09-15, commit
`1ee8614141f6478c4c6d0d84ecb33c1b98bb9235`,
[CI evidence](https://github.com/medking82/hardware-pulse/actions/runs/34976904596):
Ubuntu x64 and native ARM64 fixtures/live CPU, RAM and loopback tests all passed.
Both runners exposed 4 processors, .NET 10.0.12, and had zero invalid benchmark
readings. Medians of three runs:

| Runner | Managed bytes/poll: full / first-line | Elapsed microseconds/poll: full / first-line | Process CPU microseconds/poll: full / first-line |
| --- | --- | --- | --- |
| x64 | 36,494 / 29,638 | 22.85 / 21.27 | 22.87 / 21.28 |
| ARM64 | 31,319 / 29,214 | 28.20 / 23.67 | 28.21 / 23.66 |

Managed allocation fell approximately 18.8% and 6.7%, respectively. proc/stat
contained 1,259 characters on x64 and 483 on ARM64; savings depend on host data.
The tight loop often polls faster than CPU counters advance, so most iterations
correctly omit CPU load while retaining valid RAM. This is not a production
refresh-rate recommendation. All runs recorded zero GC collections inside the
measurement interval. The first full-file timing in both pairs was slower than
later runs; medians are descriptive evidence, not a guaranteed timing benefit.
The Windows full validation passed; no installer or running App was changed.
Raw local evidence: vendor/linux-polling-ci-34976904596.log.

## macOS adapter baseline harness

Run `Pulse.Mac.Tests.dll --measure` on macOS after building the Release test
project, with `DOTNET_TieredCompilation=0`. The CI runs it on native Intel and
Apple Silicon. Three rounds measure CPU, RAM and loopback Network separately:
100 warm-up calls followed by 1,000 polls, logging managed allocation, elapsed
and process CPU time per poll, GC counts and reading availability. Tight-loop
CPU samples can omit load when Mach counters have not advanced; source errors
and invalid RAM/Network readings fail the run.

A separate 60-second window polls all three through Core ReadingSessions once
per second, sampling process working set and managed heap every ten seconds.
It records process CPU normalized by logical CPU count and allocation scoped to
adapter/session polling. Console serialization is outside the timed window.
Explicit collections occur before and after, never during the paced window;
post-collection managed size is reported separately. Process CPU includes harness
sampling and runtime background work. Buffered observations remain live at the
final collection, so the retained delta is not exclusively adapter state.

This is a short diagnostic-host baseline, not the Windows App or a macOS UI
measurement, a long-duration leak test, or a guarantee about game frametime.
Working-set growth can reflect retained runtime pages; allocation is not retained
RAM. The Network sample uses `lo0` and records only interface count, not names,
addresses or machine identity. No timing/memory threshold determines success.

Baseline recorded on 2026-09-15 at
`6e5ac6075e5484a4ae3000a7c0363ddfbe2ce935`,
[CI run 34983064052](https://github.com/medking82/hardware-pulse/actions/runs/34983064052).
All native fixtures, CLI checks and measurements passed, with zero invalid
measurement readings. Local full Windows validation also passed. Both hosts used
.NET 10.0.12; Intel exposed 4 processors / 9 interfaces, ARM64 3 / 11. Median of
three tight-loop rounds:

| Source | Intel bytes/poll | ARM64 bytes/poll | Intel elapsed ms/poll | ARM64 elapsed ms/poll |
| --- | --- | --- | --- | --- |
| CPU | 713.6 | 713.6 | 0.00538 | 0.00154 |
| RAM | 817.6 | 817.6 | 0.00894 | 0.00301 |
| Network | 177,392.1 | 215,729.5 | 5.137 | 2.110 |

All tight-loop CPU readings omitted load because counters had not advanced;
these CPU timings cover native reading and baseline handling, not publication
of a usable load value. All paced CPU readings were valid.

| Paced 60-second window | Intel x64 | Apple Silicon ARM64 |
| --- | --- | --- |
| Process CPU, percent of all exposed CPUs | 0.2174% | 0.1309% |
| Working set, start / end MiB | 32.813 / 32.867 | 53.359 / 53.047 |
| Adapter + session managed allocation, total bytes | 10,743,328 | 13,043,712 |
| Managed heap after collection, start / end bytes | 391,360 / 376,672 | 398,224 / 375,312 |
| Natural Gen0 / Gen1 / Gen2 collections | 1 / 0 / 0 | 2 / 0 / 0 |

The dominant measured source is Network. The baseline implementation enumerates
all interfaces on every read before obtaining the selected interface statistics.
That is a concrete optimization candidate, not proof that enumeration accounts
for the entire cost. A follow-up should isolate enumeration, preserve fresh byte
counters and missing/reappearing interface behavior, and use paired measurements
before claiming improvement. No runtime optimization is included in this baseline.
The observed heap rises between natural collections and then falls; this short
window neither demonstrates a leak nor establishes long-term memory stability.
Raw local evidence: `vendor/mac-polling-ci-34983064052.log`.

### Selected interface comparison

The follow-up harness retains that enumeration-per-poll strategy in an injected
counter source. Each round now measures `network-enumerated` and
`network-selected` with alternating order, identical warm-up/poll counts and
the same interval logic. The selected path retains the interface reader but
requests a new statistics object each poll. Read failure invalidates selection;
fixtures verify re-resolution, fresh counters and reset recovery. The paced
window uses the selected path. This comparison isolates source lookup without
changing the host polling cadence or introducing a background subscription.

Paired result at `a9e9af2741e55e35dce9786b9b83b179885015e8`,
[CI run 34984104231](https://github.com/medking82/hardware-pulse/actions/runs/34984104231):
native Intel/ARM64 loopback, CPU/RAM, recovery fixtures and CLI contracts all
passed. Local full Windows validation passed. Three-round medians, same-process
enumerated / selected comparison (.NET 10.0.12, 9 / 11 interfaces respectively):

| Runner | Managed bytes/poll: enumerated / selected | Elapsed ms/poll: enumerated / selected | Process CPU ms/poll: enumerated / selected |
| --- | --- | --- | --- |
| Intel x64 | 177,392.128 / 1,049.632 | 3.58650 / 0.05761 | 3.58418 / 0.05720 |
| Apple Silicon ARM64 | 215,729.472 / 1,049.632 | 1.62272 / 0.02023 | 1.61636 / 0.02032 |

Managed allocation fell approximately 99.4% / 99.5%. Selected-source measured
loops had zero GC collections and all 1,000 readings per round were valid.
Warm-up includes initial resolution, so these numbers describe steady reads,
not first selection or recovery cost. Missing interfaces still require lookup.

The selected-source 60-second paced window recorded 0.00628% / 0.01046% process
CPU normalized across 4 / 3 exposed CPUs, with 162,720 / 162,856 allocated bytes
for all adapter/session polls. Working-set endpoint changes were +53,248 /
+32,768 bytes; no natural GC occurred. Post-collection managed heap was 361,856 /
349,344 bytes versus 393,928 / 388,744 before the window. These process figures
are descriptive; the prior paced run used different runner instances and is
not a paired before/after CPU or working-set experiment. The directly paired
evidence above supports the per-poll improvement, not an App RAM-saving claim.
Raw local evidence: `vendor/mac-network-selection-ci-34984104231.log`.

## Shared Desktop native window baseline

The preview host accepts `--measure-session` (mutually exclusive with
`--smoke-test`). It opens the normal Monitor window, excludes ten seconds of
warm-up, then observes at least sixty seconds of its ordinary one-second polling.
It prints one `BENCH_DESKTOP` JSON record and exits. Personal settings are not
loaded, Codex remains disabled, no forced GC occurs, and no interface name or
account data is emitted. The original Windows baseline uses `--demo`; those results represent
static demo rendering and must not be compared as live Windows telemetry costs.

The observer captures process CPU, working set, managed allocation/heap and GC
counts only at the start/end. CPU percentage divides process CPU seconds by
elapsed seconds and exposed logical CPUs. Working set is resident process memory,
not managed heap or archive size; start/end differences alone cannot establish
a leak. Managed heap uses GetTotalMemory(false), so it is not a post-GC retained
heap measurement. Small observer overhead is included in the process deltas.

CI measures the extracted self-contained Linux/macOS AppHost, including the
actual window backend and live adapters; Linux uses X11 under Xvfb. Windows
measures the framework-dependent demo host. The record includes measured poll
counts and CPU/RAM/network availability counts so an idle or missing source
cannot silently be interpreted as equivalent work. CI has no arbitrary CPU or
RAM pass threshold: these are descriptive baselines on hosted runners, not a
gaming overlay or long-running resource guarantee.

### First native shared-window baseline

Commit `e0ce91ac741a2bd254ac521d8bd63b6aa25e4d1e`,
[run 35024372783](https://github.com/medking82/hardware-pulse/actions/runs/35024372783)
(2026-09-16 local) passed all six jobs and four extracted package launches.
Every measured poll had CPU, RAM and selected-network readings available.
Intervals lasted 60.02–61.00 seconds, with 60–61 polls.

| Host / scenario | Logical CPUs | CPU % of machine | Working set start → end MiB | Allocated MiB | Gen0 / Gen1 / Gen2 |
| --- | ---: | ---: | ---: | ---: | --- |
| Linux x64 / live Xvfb | 4 | 0.565 | 176.89 → 187.28 | 3.384 | 19 / 19 / 19 |
| Linux ARM64 / live Xvfb | 4 | 0.593 | 183.23 → 194.28 | 3.317 | 18 / 18 / 18 |
| macOS Intel / live | 4 | 0.728 | 91.46 → 91.56 | 1.024 | 0 / 0 / 0 |
| macOS ARM64 / live | 3 | 0.653 | 158.67 → 164.27 | 1.035 | 0 / 0 / 0 |
| Windows x64 / static demo | 4 | 0.038 | 86.69 → 85.60 | 0.084 | 0 / 0 / 0 |
| Windows ARM64 / static demo | 4 | 0.083 | 94.89 → 68.09 | 0.087 | 0 / 0 / 0 |

These are single hosted-runner observations, not paired optimization results.
The Linux full-GC frequency and resident-memory increase need attribution before
changing polling or rendering policy. The observer does not force GC; this result
alone does not identify the caller or prove a leak. Windows demo costs exclude
live hardware polling and must not replace the installed WPF baseline above.

GC attribution uses opt-in `--measure-session --diagnose-gc`. An EventListener
counts runtime GCStart_V2 depth/reason/type events across the entire process,
including warm-up, and emits `DIAG_GC` on exit. It records no stacks or object
contents. Event delivery can lag the final snapshot; these counts need not equal
the narrower benchmark interval. Diagnostic overhead is included, so compare
demo/live diagnostic runs to each other rather than treating them as unchanged
baseline measurements. Normal launches create no listener.

At `212ca94`, [run 35025216071](https://github.com/medking82/hardware-pulse/actions/runs/35025216071)
passed all six jobs. Linux x64 and ARM64 static demo recorded zero collections;
live diagnostics recorded 18 Gen2 collections in the measurement interval and
21 across startup/warm-up/measurement, all runtime reason 7 (`induced_noforce`).
This rules out ordinary allocation-triggered GC for those events, but does not
alone identify the inducing caller. The pinned Avalonia X11 implementation
defaults UseRetainedFramebuffer to false, disposing its native surface after
each software blit; its UnmanagedBlob reports GC.AddMemoryPressure on allocation.

The next paired experiment keeps live telemetry in both runs and compares that
transient surface with retained framebuffer reuse, on the same Linux runner.
`--transient-framebuffer` is a Linux-only diagnostic control requiring
`--measure-session --diagnose-gc`; normal launches use retention. Hardware
rendering selection is unchanged. Retention trades a persistent window-sized
buffer for avoiding repeated native allocation; resize replaces the buffer.
Sources: Avalonia 12.1.2
[X11Window.cs](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Avalonia.X11/X11Window.cs),
[X11FramebufferSurface.cs](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Avalonia.X11/X11FramebufferSurface.cs),
[UnmanagedBlob.cs](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Avalonia.Base/Platform/Internal/UnmanagedBlob.cs),
and .NET [GC reason enum](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/gc/gc.h).

### Retained framebuffer result

At `63cad3f6c53ef6bcb98484742817e662ae1b899d`,
[run 35026048603](https://github.com/medking82/hardware-pulse/actions/runs/35026048603)
passed all six jobs, including four extracted package launches. Within each Linux
runner, the diagnostic transient control ran first, then retention; both had 61
valid live CPU/RAM/network polls over approximately 61 seconds.

| Architecture | Transient → retained CPU % of machine | Gen2 collections in interval | End working set MiB, transient → retained |
| --- | --- | --- | --- |
| x64 | 0.6706 → 0.6057 | 19 → 0 | 188.71 → 186.54 |
| ARM64 | 0.5722 → 0.5056 | 18 → 0 | 196.23 → 191.60 |

Transient diagnostics recorded only reason 7 collections; retention recorded
none, including warm-up. The independently launched retained self-contained
packages also recorded zero collections. This isolates repeated X11 software
framebuffer allocation/memory-pressure reporting as the source of these induced
collections under Xvfb. Retention removes that churn without reducing polling.
Paired CPU fell approximately 9.7% / 11.6% in this single sequential experiment;
this is not a universal savings estimate or randomized repeated benchmark.

Retained managed heap rose naturally without collection (about 3.2 MiB during
the interval), and working set still grew within each run. Endpoint differences
do not establish lower steady-state RAM or absence of a leak. Longer runs and
real desktop/GPU environments remain unmeasured. Hardware renderer policy and
the shipped Windows WPF runtime are unchanged.

## Windows WPF 0.6.35 ten-minute UI observation

Measured on 2026-09-21 (UTC+8), release source `a42a809`, with 16 logical
processors. Run `scripts/Measure-NativeUi.ps1 -AppDirectory build/native/app
-Seconds 600 -Scene monitor` through the Windows hidden runner after building
the native app and test harness. The isolated state uses synthetic hardware
snapshots updated every two seconds; hardware collection, FPS and quota reads
are disabled. The window is 310 x 690 DIP (465 x 1035 pixels), over the host
desktop, with Local Contrast disabled. No user settings are changed.

After ten seconds of warm-up, 300 samples covered 605.43 seconds. CPU is
normalized over all logical processors; memory is process memory, not managed
heap size. No forced GC or working-set trimming was used.

| Measurement | Observation |
| --- | ---: |
| Average CPU, percent of whole machine | 0.1092% |
| Mean working set | 123.88 MiB |
| Working set range | 123.21–124.25 MiB |
| Mean private memory | 94.12 MiB |
| Private memory range | 93.68–94.77 MiB |
| First / last 30 samples, mean working set | 123.45 / 124.22 MiB |
| First / last 30 samples, mean private memory | 94.10 / 94.18 MiB |

The harness completed successfully and its owned process exited. This single
short observation does not demonstrate long-term absence of leaks, an
improvement over 0.6.27, total installed-app cost, real collector cost, quota
process cost, desktop-layer integration, or gaming impact. It is a baseline for
future matched measurements, not a performance guarantee.

Evidence identity (SHA-256):

- Application: `4A7884F6150925FD31B3C08DEB0AE8972E66F7338394E3AC35FE5BB8B059F057`
- Core: `D3F1733880E98C71352E747B071E75760B6BF4481388E75902484E50D687939F`
- Windows adapter: `6E406D6591F9B2C8135B997CA34C792C29B911FF95F755D74FD61E7A2F56247F`
- Harness: `E02DE3A4AF4DA329F1766E236B25700D9B47F2F8D9A0E9BECAE41FCAB8F1A491`

Raw local results and samples are retained in
`vendor/ui-measure-fb29c783e54b41159aaa61dc6f888a75`; these machine-local artifacts
are not distributed in the installer or committed to Git.

## WPF refresh work reduction after 0.6.36

Baseline source: `64040491acbd1e5e467adf6b4b0f1fad8b4aeb58` (0.6.36).
Measured on 2026-09-21, Windows x64, 16 logical processors. The candidate retains
the WPF cards, glass material, fonts and saved preferences. App density is
recomputed on geometry/capability changes instead of every poll. The 500 ms FPS
tick updates only its existing Desktop row; identical FPS text retains its runs.
The two-second hardware/ordinary Desktop refresh and 100 ms Local Contrast timer
are unchanged. SensorProfile keeps per-snapshot discovery but avoids temporary
match arrays; it adds no persistent topology cache or new dependency.

Two fresh-process baseline/candidate pairs ran sequentially, with the order
reversed in the second pair. No forced collection or working-set trimming was
used. These are bounded component measurements, not installed-app savings or
long-term leak evidence.

### App refresh workload

The existing `NativeTests.exe <isolated-state> perf` workload warms up 20 polls,
then processes 300 synthetic changing snapshots with dispatcher pumping. The
window is 310 x 690 DIP. Collection, FPS, quota requests and Local Contrast are
excluded. This concentrated workload does not represent the normal polling rate.

| Measurement across two runs | Baseline | Candidate |
| --- | ---: | ---: |
| CPU time / 300 refreshes | 2,922–3,000 ms | 1,688–1,906 ms |
| Reported managed allocation / 300 refreshes | 274.52–274.55 MB | 122.94–123.02 MB |
| Endpoint working set | 124.79–125.06 MiB | 127.75–128.04 MiB |
| Endpoint private bytes | 97.52–101.63 MiB | 131.52–132.58 MiB |

Paired CPU time fell 35–44% and allocation about 55%. The burst endpoint memory
was higher, so this test does **not** show lower resident or committed RAM.
Allocation totals and process memory are distinct quantities.

### App at the normal refresh interval

To check the burst memory result against normal use, `Measure-NativeUi.ps1`
ran the monitor scene for 60 seconds per fresh process after ten seconds of
warm-up, candidate first, then baseline. Synthetic snapshots update every two
seconds at 310 x 690 DIP; each run collected 30 samples. Collector, FPS, quota
requests and the Desktop layer are excluded; Local Contrast is disabled in
this App-only scene. The Desktop Local Contrast measurements are below.

| Measurement | Baseline | Candidate |
| --- | ---: | ---: |
| Average CPU, percent of whole machine | 0.0951% | 0.0145% |
| Mean working set | 122.51 MiB | 122.62 MiB |
| Mean private bytes | 94.32 MiB | 93.76 MiB |

This single pair showed lower UI CPU at the normal interval and similar process
memory. The elevated burst endpoint private bytes did not recur in this short
normal-interval observation. Neither result establishes long-term RAM behavior
or savings for the complete installed application.

### Desktop FPS with Local Contrast enabled

The same local `FpsUiBench.cs` harness runs in fresh processes against each app.
It uses a fixed black/white gradient, a locked 400 x 850 DIP Desktop, 16 DIP text,
6 DIP spacing, Local Contrast enabled, synthetic FPS every 500 ms and synthetic
hardware snapshots every two seconds. It calls each runtime's FPS update path,
warms up ten seconds and samples for about 30 seconds. Real ETW, collector and
quota work are excluded. Both paths processed 79 total FPS ticks per run.

| Measurement | Baseline pair 1 / 2 | Candidate pair 1 / 2 |
| --- | ---: | ---: |
| Average CPU, percent of whole machine | 0.723 / 0.574% | 0.612 / 0.584% |
| Reported managed allocation | 31.18 / 30.97 MB | 23.30 / 23.67 MB |
| Mean working set | 138.47 / 137.22 MiB | 136.78 / 135.56 MiB |
| Mean private bytes | 135.82 / 136.36 MiB | 134.19 / 134.61 MiB |

Allocation fell 24–25%. CPU improved in one pair and slightly increased in the
other; these short observations do not establish a repeatable CPU reduction for
the complete Local Contrast scene. The roughly 1.6–1.8 MiB memory differences
are also insufficient to promise a steady-state RAM reduction.

### Snapshot matching

A local headless harness parses the same 300-sensor host snapshot 3,000 times
after 50 warm-up parses; it prints only aggregate measurements. Schema and
matching semantics remain covered by native/legacy differential fixtures,
including duplicate candidates, null capabilities and changed hardware identity.
Allocation fell from 343.46 MB to 330.67 MB (3.7%); CPU ranges overlap at
406–422 ms, so no parser CPU improvement is claimed.

### Evidence and checks

`Validate.ps1 -ModernCore` passed, including WPF interaction, real Local Contrast
capture, Desktop layout, sensor parity, quota lifecycle and package checks.
New checks cover unchanged-poll density churn and FPS unchanged text, value
changes, stale/recovery, font changes and hidden rows.

Release preparation for 0.6.37 also makes pair/usage visibility invalidation
explicit and adds a Memory capability/usage transition fixture. The measurements
above identify the pre-release optimization binaries; they are not new
measurements of the signed 0.6.37 installer.

Local raw evidence is retained under `vendor/perf-monitor-*`, `perf-steady-*`,
`perf-fps-*`, `perf-sensor-*` and `perf-runtime-hashes.json`. The local
reproduction sources are `vendor/FpsUiBench.cs`, `SensorPerf.cs` and
`Run-PerfIteration.ps1`; they and host measurements are not installer payloads.

Measured SHA-256 identities:

- Baseline app: `98D9D2574CBADC62ABE490CD5B1125EDD0ACE4676C01FFC1C514899D72262F32`
- Candidate app: `F1851A24E45EBF6F43542821ED8BE8953AD0039A57D77BDBD0A3E4E0A594B8D1`
- Baseline Windows adapter: `4B8E4BB38E8C2D66DA6242FCB635AA5DF283184B3A5F6968983C881DAEE6E05D`
- Candidate Windows adapter: `81662DA0F3F6FBBE8589E66F92AC2EF5696B08AF60151D2005F959013FD94F5D`
