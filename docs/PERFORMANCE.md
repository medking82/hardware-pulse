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
