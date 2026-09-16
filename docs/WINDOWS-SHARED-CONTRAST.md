# Windows shared Local Contrast

The shared Desktop settings now offer opt-in Local Contrast and a 15-second
screenshot mode. The default is off. Its preference stays in the shared preview
profile; installed WPF settings and collector privileges are unchanged.

## Ownership and invariants

`WindowsBackgroundCapture` owns self-exclusion and GDI resources. It accepts only
an owned live HWND and reads that window's client bounds. Windows 10 version 2004
or later is required; a failed exclusion is unavailable, never a capture of the
widget itself. A pre-existing nonzero display affinity is not overwritten.
Disabled, hidden, minimized and disposed readers return no frame. Disposal
restores the affinity only while the adapter's exclusion is still present.

Capture is memory-only. Client bounds are capped at four million source pixels;
the reusable downsampled BGRA buffer is capped by Core's 160,000-pixel limit.
It is borrowed only inside the serialized read callback and cleared on release.
Resize replaces native and managed buffers; GDI drawing is flushed before reads.
No image is written to disk, sent over the network or included in logs.

`WindowsLocalContrast` owns a 100 ms UI cadence with at most one background
capture/analysis operation. Existing `ContrastAnalysis` owns luminance, smoothing,
temporal hysteresis and region decisions. Results include shades, not image
buffers. Generation and window geometry checks reject stale results after
disable, hide, screenshot, move or close. Theme/text colors return on unavailable
capture. Text opacity and icon palette preferences remain effective; adaptive
icons and mixed-region edge protection follow the reference WPF behavior.

Hide, disable and close stop the timer and dispose capture resources. Analysis
buffers are also released from the controller when stopped. Screenshot mode
temporarily restores capture visibility, then resumes after 15 seconds if
enabled and visible. Windows High Contrast suppresses capture; the existing
Monitor cadence retries after it ends. No additional hardware polling is added.

The bounded change covers the modern Windows capture adapter, shared Desktop
presentation/preferences/catalogs and their tests. Core analysis, WPF capture,
scheduled tasks, drivers, packaging and installed profiles are unchanged.
Rollback removes this wiring and preference consumer while preserving settings.

## Acceptance evidence and gaps

`WindowsCaptureTests` places a red owned window above a blue fixture and checks
that actual capture returns the blue background. It also checks foreign-window
rejection, no read before enable/after hide/dispose, buffer reuse/resize, affinity
restoration, released-buffer clearing and repeated GDI lifetime balance.

`LocalContrastTests` uses actual black/white backgrounds and verifies light/dark
readings, text opacity, icon palette/adaptive tint, hide/show, real 15-second
screenshot timeout, disabled effects and opt-in persistence. Test capture pixels
remain in memory. Tests neither capture unrelated screen bounds nor change the
user's Windows accessibility settings.

Validation: DesktopTests Release build, `--capture-native`, `--contrast-native`,
full headless, full `--native-session`, then `scripts/Validate.ps1 -ModernCore`.
The suites run sequentially. Local evidence is in `vendor/test-contrast-full.log`,
`vendor/test-contrast-native.log` and `vendor/validate-contrast.log`.

Final installed multi-display/HDR/accessibility acceptance, animated wallpapers,
matched WPF CPU/RAM comparison and long-running resource stability remain release
gates. Incremental native fixtures do not establish those results.

Native contracts: [display affinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity)
and [DIB section lifetime and synchronization](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection).

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"sensitive","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
