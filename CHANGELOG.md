# Changelog

[简体中文](CHANGELOG.zh-CN.md)

Dates are release dates. Unreleased entries describe source changes, not an available download. Author: [Marck Wong](https://github.com/medking82).

## Unreleased

- Prepare a separate fixed Win7 x64 update asset identity. Legacy clients reject modern Windows installers and report when no compatible asset exists; existing modern Windows updates retain their asset name and verification rules. No Win7 package is published yet.

- Explain and disable unavailable FPS and Local Contrast controls on older Windows, including Desktop and tray entry points, while retaining saved preferences. Windows 7 compatibility is still under development.

- Window snapping falls back to WPF DPI on older Windows versions that lack `GetDpiForWindow`, preserving scaled snap distances without repeated missing-API exceptions. Windows 7 installation remains unverified.

- Prepare the Windows WPF collector for legacy Windows with driver-free CPU/RAM/network counters and older memory-metadata fields. The Framework and modern hosts share counter mapping. This does not add verified Windows 7 support or lower the installer minimum; temperature/fan/GPU and FPS compatibility remain unresolved.

- Shared Linux X11 floating monitor adds mouse-through locking with XFixes. Unlock restores the default input region and window frame; native x64/ARM64 checks pass. Native Wayland locking is not implemented.

- Shared macOS floating monitor adds AppKit mouse-through locking and reuses the Monitor/tray unlock action. Native Intel/ARM64 lifecycle and package checks pass.

- Shared Windows floating monitor supports native click-through locking. Reopening it from Monitor or the tray unlocks the same window. Windows ARM64 interactive verification remains pending.

- The shared tray/menu bar can reopen the existing floating monitor. The entry follows the App language and cannot reopen a window after quit.

- Shared Desktop adds a floating monitor with optional Always on top, using the existing CPU/RAM/network/sensor snapshots without additional sampling. One window is reused, language/theme follow Monitor, and closing Monitor closes it. Desktop-layer placement and FPS are not implemented in the shared host yet.

- Shared Desktop embeds pinned Noto Sans CJK SC/TC fonts for Chinese UI on systems without CJK fonts. Build preparation verifies size and SHA-256, packages include the OFL notice, and native smoke fails on missing catalog glyphs. No system font installation or runtime download is needed.

- Shared Desktop supports Auto (System), English and Simplified/Traditional Chinese UI, including Monitor, Settings, sensor status, Codex quota and tray actions. Language switches immediately without restarting sampling or refreshing credentials and is saved in the isolated preview profile. Auto respects Chinese script and region preferences.

## 0.6.26 — 2026-09-16

- Windows Desktop also preserves App icon colors when the chosen palette color happens to equal the text color; Local Contrast no longer mistakes that choice for monochrome mode.
- Explicit App palette selection stays separate from adaptive monochrome mode, including while Always on top is enabled.

## 0.6.25 — 2026-09-16

- Desktop icons now keep the selected App palette when Always on top or Local Contrast is enabled. Local Contrast adds a fine opposite-color outline instead of replacing palette colors with near-black or near-white.
- Text adaptation and monochrome icons remain available when App icon colors are disabled. Screenshot mode continues to freeze the current appearance.

## 0.7.0-preview.1 — 2026-09-16

- First experimental shared Desktop prerelease for Windows, Linux and macOS on x64 and ARM64, with six self-contained archives and SHA-256 sidecars.
- Live CPU/RAM, selected-interface network rates, opt-in Codex file-login quota, grouped Settings, themes and optional tray/menu-bar actions. Preview settings are separate from the installed WPF App.
- All six frozen native CI packages launched successfully. Published asset sizes and server SHA-256 digests match the verified downloads; the tag points to the tested source commit.
- This is not feature parity with the stable Windows App. Desktop overlay, FPS, hardware temperatures/fans, additional quota providers and other integration work remain. See [preview guide](docs/DESKTOP-PREVIEW.md) for signing and validation limits.
- Stable latest and the Windows automatic-update channel remain v0.6.24.

## 0.6.24 — 2026-09-15

- Reduce FPS statistics allocation by reusing a bounded sorting buffer and replacing temporary LINQ arrays with direct history traversal. Clear releases the buffer.
- Preserve Current, Average, Minimum, 1% Low, active stream selection, history limits and refresh intervals.
- Add randomized/reference, tie selection, smaller-stream reuse and steady-allocation regression tests on Framework and .NET 10.
- Add a reproducible x64 FPS microbenchmark: CPU 1.25–1.41 to about 0.23 ms/read; reported allocation 936,232 to 40 bytes/read in the fixed 14,400-frame workload. This does not establish whole-app or game performance gains.

## 0.6.23 — 2026-09-15

- Extract Windows memory and hardware inventory sampling into the Windows adapter, and move existing material policy into shared Core.
- Share numeric formatting across Cards, Desktop and overlay: preserve fixed one-decimal temperatures/utilization and view-specific voltage precision; round overlay RPM to whole numbers consistently.
- Move quota response decoding/reset formatting, bounded FPS history/statistics and settings value rules into Core. Retain Windows JSON storage, credentials, process capture and request policy.
- Test the same quota fixtures, FPS bounds/statistics and settings semantics on Framework and .NET 10, alongside native UI/transport/startup/package regression checks.
- FPS microbenchmark allocations remain unchanged; CPU ranges overlap. This is a modularization release, not a claim of reduced whole-app RAM/CPU or new ARM64/Linux/macOS support. Existing settings and process isolation are preserved.

## 0.6.22 — 2026-09-15

- Extract shared readings/session state, quota contracts/refresh lifecycle, network formatting and existing contrast analysis into Pulse.Core.dll.
- Separate snapshot parsing, sensor mapping, quota providers and LAN/Wi-Fi sampling into Pulse.Adapters.Windows.dll. Preserve credentials, request validation, sensor identifiers, refresh intervals and existing process isolation.
- Add a .NET 10 Core target and reuse the same headless tests alongside the shipped Framework build. Add standalone Windows adapter smoke checks and package dependency checks.
- Preserve the current Windows x64 app and settings. No additional process or sampling timer is introduced; this release does not claim a measured CPU/RAM reduction or ARM64/Linux/macOS/Windows 7 support.

## 0.6.21 — 2026-09-15

- Apply Local Contrast to every Desktop SVG icon using the background beneath that icon, independently of its label and value. Previously only the FPS icon adapted, using its adjacent label.
- Preserve App palette hue while adjusting brightness; use dark/light contrast in text-color mode. Remove the unconditional topmost icon darkening while Local Contrast is enabled.
- Add a thin opposite-color outline over mixed backgrounds. Reuse unchanged icons and retain Screenshot mode freezing and base-style restoration when Local Contrast is disabled.
- Verify independent icon/label sampling, dark custom palettes, stable icon reuse, Screenshot mode and style restoration alongside the complete regression suite.

## 0.6.20 — 2026-09-15

- Add Settings → General → Export Diagnostics with English, Simplified Chinese and Traditional Chinese labels. Choose where to save the local JSON report.
- Include App/OS versions, manufacturer/model, motherboard, non-network sensor readings and fan mapping. Preserve unmapped fan readings so support can distinguish mapping gaps from unavailable RPM data.
- Report missing or invalid collector snapshots without exporting arbitrary error logs. Exclude credentials, quota data, settings, network identifiers, screenshots and serial-number fields; nothing is uploaded automatically.
- Validate report contents, private metadata exclusion and Settings rendering alongside the complete regression suite.

## 0.6.19 — 2026-09-15

- Keep Desktop temperatures and processor utilization at one decimal place, including whole readings such as 54.0 °C and 4.0%.
- Keep Desktop RAM/VRAM used capacity, total capacity and utilization at one decimal place, such as 2.0 / 8.0 GB · 25.0%. App card utilization uses the same precision.
- Apply fixed one-decimal temperatures and utilization to the separate overlay. FPS and fan RPM retain integer formatting.
- Update the native WPF regression expectations for whole-valued temperature and memory readings.

## 0.6.18 — 2026-09-15

- Reduce Local Contrast overhead by reusing capture buffers, bounding analysis to 160,000 pixels, caching luminance conversion and avoiding per-label pixel copies and unchanged foreground brush replacement.
- Extract the analysis into an independent AnyCPU Pulse.Core.dll without WPF or Win32 dependencies. Windows capture and rendering remain in the native adapter; ARM64, Linux and macOS application support is not yet implemented.
- A controlled component benchmark measured about 87% less CPU time per frame and 93% less managed allocation. This does not establish lower resident memory or whole-app/game performance; see docs/PERFORMANCE.md for methodology and limits.
- Validate headless Core behavior, large captures, resize, dispose/resume, Screenshot mode and stable brushes alongside the complete regression suite. Preserve saved settings and zero-opacity Desktop input behavior.

## 0.6.17 — 2026-09-15

- Fix dragging at zero Desktop background opacity. Windows passed fully transparent pixels through before WPF received mouse input, so unlocked blank areas could not start a drag.
- Give only the unlocked editor a 1/255-alpha input surface, including resize corners. Saved opacity is unchanged; locking removes the surface and preserves true zero-opacity click-through.
- Native hit-test regression reproduces the old failure and verifies blank space and resize corners at 0% and 30%, plus transparent locked state.

## 0.6.16 — 2026-09-15

- Restore normal activation and mouse input when Desktop is unlocked. Previously the real Desktop layer retained WS_EX_NOACTIVATE even though the editor controls were visible; isolated tests did not include this layer.
- Preserve no-activation and click-through only while locked. Apply the requested lock state when the native layer is first created, avoiding a temporarily locked editor.
- Verify native extended styles through initial unlock, lock and unlock. A test using the real Desktop layer was confirmed to support both dragging and button clicks.

## 0.6.15 — 2026-09-15

- Route unlocked Desktop content through the client Preview mouse event and explicitly start WPF DragMove. Retain native edge resizing and interactive editor controls. An isolated test window recorded continuous position changes during dragging.
- Select FPS swapchains by frames received in the latest one-second window, rather than total historical frames. A large expired stream no longer masks a fresh active stream with a dash. Truly stale data remains unavailable.
- Reproduce the stale-swapchain failure and validate the correction alongside the complete regression suite.
- Fix FPS row height across ready/waiting transitions. Superscript badges and the empty dash now share a fixed line box, preventing the rows below from jumping.

## 0.6.14 — 2026-09-15

- Move unlocked Desktop panels through native caption hit-testing, so ScrollViewer content can initiate dragging without relying on a handled WPF mouse event.
- Preserve editor button clicks, scrollbar interaction, eight-edge resizing and locked click-through. Save and clamp position when native move/resize finishes.
- Validate content/button/scrollbar/edge hit targets and locked behavior in a real WPF window.

## 0.6.13 — 2026-09-15

- Give FPS its own Pulse mint SVG color (#9EDFD3) in the App shortcut and Desktop App-color mode. Local contrast darkens mint over bright backgrounds; text-color mode follows the adaptive text instead.
- Refine whole-label contrast selection and add a subtle, zero-offset opposite-color edge only where local backgrounds contain both light and dark regions. Separate enter/exit thresholds keep the edge from flickering near the boundary.
- Clear adaptive edges when local contrast is disabled or capture becomes unavailable. Screenshot mode retains the current appearance.

## 0.6.12 — 2026-09-15

- Keep Desktop FPS in one row with small NOW / AVG / MIN badges, three reserved tabular digit positions per reading, and a single dash when frames are unavailable. Minimum remains the rolling 60-second minimum, not 1% Low.
- Choose one consistent local contrast color per label and reading, with hysteresis, to avoid splitting glyphs into black and white fragments.
- Add Screenshot mode in Desktop settings and the tray: freeze text colors and allow capture for 15 seconds, then restore local contrast automatically.
- Fit locked Desktop content by growing a saved short panel and, in Auto columns, widening within the monitor work area before falling back to scrolling. Keep FPS badges inside narrow panels.

## 0.6.11 — 2026-09-15

- Show Desktop FPS as Current, Average and Minimum readings. Current uses the latest one-second frame window; Average and Minimum use the existing rolling 60-second window.
- Keep FPS values on one line. No-frame states show only an em dash, with diagnostic status in the tooltip instead of the reading. The FPS visibility/order control moves the three readings together.
- Reserve three tabular digit positions for FPS values, right-aligned without leading zeroes, so 9/99/100 transitions do not shift the value slot.
- Smooth local background luminance at a font/DPI-scaled radius before choosing text contrast. Stabilize small changes across frames while allowing large changes immediately; this reduces fine-texture speckles without blurring glyphs.
- Validate ready and waiting transitions, numeric statistics, no wrapping, and the complete regression suite.

## 0.6.10 — 2026-09-15

- Add opt-in local adaptive text contrast: an in-memory pixel mask uses black/white hysteresis, independent of SVG colors. Capture runs on a single background worker; Windows 10 build 19041+ is required. While enabled, screen capture may exclude the Desktop panel. HDR and fast-game performance still need field validation.
- Remove the outer frame of locked Desktop panels at 0% background opacity. Make Text Color actionable in automatic modes; a selection returns to manual color.
- Add an optional FPS Desktop reading using the existing collector and target selection without enabling the separate FPS overlay.
- Add a customizable Desktop toggle shortcut, default Ctrl+Alt+F10, with registration conflict checks and no App activation. Reserved Windows/F12 combinations are not accepted; game-local bindings may still overlap.
- Validate actual capture exclusion/restoration, split dark/light backgrounds, WPF brush placement, opacity, FPS separation and the existing regression suite.

## 0.6.9 — 2026-09-15

- Make resize follow the pointer without repeatedly restarting card motion, add a column threshold buffer and reuse the dragged card transform. Text remains directly rendered during size adjustments.
- Enable nearby-app snapping for Desktop, soften magnetic attraction and cache snap targets per drag. A small final capture range avoids a large jump on release.
- Notify after manually locking Desktop how to unlock through the tray; explicitly use Segoe UI with Microsoft YaHei UI / JhengHei UI fallback in App and Desktop.

- Allow 0–100% Desktop text and topmost background opacity. Auto Contrast and topmost rendering no longer override text opacity; saved values and slider labels match the rendered result. Keep 55% as the topmost background default.

## 0.6.8 — 2026-09-15

- Group settings by General, App Appearance, Desktop, App Cards, FPS and AI Quota, with wrapping navigation and responsive columns. Desktop layout, appearance and readings now share one page.
- Give topmost panels a lighter smoke background (55% default), opaque dark text and independent background opacity; trim the locked backdrop to content without changing saved geometry.
- Add separate ordinary Desktop background opacity. Keep regular Desktop preferences and click-through behavior; actual Desktop blur remains unavailable.

## 0.6.7 — 2026-09-15

- Optional Desktop Always on top: default wallpaper-level behavior is preserved; locked panels pass mouse input through, editing retains no-activate behavior, and disabling the option returns to the desktop layer.
- Apply the saved lock state immediately after the native Desktop window is created.

## 0.6.6 — 2026-09-15

- Distinguish Desktop LAN, Wi-Fi link speed and signal readings with original Ethernet, wireless and signal-bar SVG icons; preserve App palette preferences.
- New installs start with 30% background opacity, #35383B tint and system glass enabled. Saved preferences are preserved.
- Refresh the GitHub hero using actual WPF UI and SVG artwork with fictional demo readings.

## 0.6.5 — 2026-09-15

- Show independent LAN and Wi-Fi negotiated link speeds in App and Desktop; Wi-Fi rate and signal update even when LAN carries most traffic.
- Prefer confirmed physical adapters, clear disconnected/unknown link values, and keep throughput explicitly associated with its traffic adapter. Desktop connection readings can be hidden individually.

## 0.6.4 — 2026-09-15

- Launching the App shortcut again signals the existing instance to show its home instead of silently exiting. Hidden, minimized and Desktop-mode windows are restored without creating a second UI or Collector.
- Queue activation during initial startup and transfer foreground permission to the matching running executable.

## 0.6.3 — 2026-09-15

- Include Codex HTTP additional quota pools in All available display; preserve the essential Weekly-only Desktop display.
- Double-click the system tray icon or choose Show Pulse to restore the App home from hidden, minimized or Settings state, while retaining the Desktop panel.

## 0.6.2 — 2026-09-15

- Add complete App quota display and draggable provider cards with persistent order; retain essential Desktop quotas.
- Reorganize Settings into responsive columns with separate App/Desktop appearance, readable typography, consistent sliders/radio controls, centered selectors and a fixed Back action.
- Edit Desktop directly, resize from every edge or corner, lock from its editor and re-enter editing from the tray.
- Default Desktop icons to the App palette while retaining explicit user overrides.
- Correct compact Network labels and light-theme navigation icon contrast.
- Start directly in saved Desktop mode without showing or activating the App window; save preferences even before opening the App.

## 0.6.1 — 2026-09-15

- Desktop names use available row width instead of a fixed cap; narrow rows wrap without overlapping readings.
- Show available updates, download progress, retry and install actions beside Settings on the home screen.
- Make the Desktop icon palette switch visible without expanding Customize Desktop.

## 0.6.0 — 2026-09-15

- App cards adapt to one, two, or three columns with animated reordering and keyboard controls.
- Enter Desktop View directly from the monitor; customize visibility and ordering independently of App cards.
- Resize Desktop View while editing, choose automatic or capped columns, and preserve its geometry.
- Consolidate Desktop appearance controls, add a recommended style, and replace per-text contrast blocks with a rounded shared backdrop.
- Optionally follow the App palette for Desktop SVG icons.
- Synchronize FPS toggles on the monitor, Settings, and system tray, with an SVG icon.
- Show connection type and negotiated link speed on Desktop; show Wi-Fi signal percentage only when Windows supplies it. No Wi-Fi scanning or connectivity changes.

## 0.5.12 — 2026-09-15

- Show only Gemini 5-hour and Weekly quota windows for Antigravity; omit its Claude/GPT pools and duplicate or unrelated windows.
- Use shorter Gemini labels in Desktop View and identify the Gemini group in the monitor card.
- Preserve disabled or missing quota values as unavailable, with stable 5-hour then Weekly ordering.

## 0.5.11 — 2026-09-15

- Fix Antigravity quota discovery: bind the Windows process instance before calling GetOwnerSid. Projected WMI query objects could fail before any quota request. Same-user and PID-owned loopback-port checks remain in place.
- Show only the main Codex Weekly quota in Monitor and Desktop View; omit Spark, reserve and other additional pools.
- Validate against live Codex, Antigravity and Claude quota endpoints; add regression checks for current-user acceptance, different-user rejection and main Weekly selection.

## 0.5.10 — 2026-09-15

- Add independently opt-in Codex, Claude and Antigravity quota readings with provider SVG icons in Monitor and Desktop View. Five-minute background refresh; explicit missing-login, unavailable and stale states. No Token Monitor or PowerShell runtime requirement.
- Show negotiated Link Speed for the same network adapter as Download/Upload, in Mbit/s or Gbit/s. Disconnected links are labeled; old snapshots show unknown rather than an invented speed.
- Support provider accent colors and Unified Color for quota icons; Desktop icons follow Desktop contrast/color settings.
- Keep quota credentials out of settings, hardware snapshots and logs. Antigravity requires a running local language server; expired Codex/Claude login must be renewed in the owning app.
- Enable TLS 1.2 and TLS 1.3 for quota and update connections.

## 0.5.9 — 2026-09-14

- Protect Desktop Auto Contrast text and icons with local backing, at least 90% effective opacity and sharp glyph rendering. Sample both sides; use protected light text when sampling is unavailable instead of retaining stale dark text.
- Leave a 16 logical px outer margin when snapping or restoring Desktop View to screen edges.
- Choose Hardware Colors or a custom Unified Color for monitor SVG icons and temperatures in Appearance → Colors.
- Add Network download/upload readings to Monitor and Desktop, with Auto, KB/s, MB/s and Mbit/s units. Reuse the Collector's LibreHardwareMonitor network counters and show the busiest adapter without summing overlapping adapters.
- Fix an async FPS pipe timeout race discovered during regression: native completion must retain its wait event until cancellation completes.

## 0.5.8 — 2026-09-14

- Enter Desktop Mode in an editable preview from Settings or the tray; Done locks the readings and hides the editor. Disabling it returns to the monitor.
- Reorder individual desktop readings in Settings independently of monitor cards, with animated drag handles, Esc cancellation and Up/Down keyboard support. Missing channels keep their saved position.
- Snap Desktop View to all four screen work-area edges while moving. Pull away to release without Alt; Alt temporarily bypasses snapping. Ordinary windows do not attract the desktop readout.

## 0.5.7 — 2026-09-14

- Add optional Desktop Mode: transparent readings above the wallpaper and below ordinary windows, with no duplicate Collector.
- Adaptive light/dark text samples nearby wallpaper only while the desktop is active; custom color, text opacity and a contrast outline remain available.
- Explicit GPU Fan labels and separate GPU Fan 1 / 2 readings preserve zero RPM per channel.
- Show Desktop Mode VRAM usage as used / total GB and percentage; integrated GPUs identify shared GPU memory.
- Separate desktop font size, text color, row spacing and position preferences; locked readouts pass clicks through, with editing and recovery in the system tray.
- Preserve monitor settings, card order/visibility and hardware names. Rediscover the Windows desktop host when window ordering changes.
- Validated with Wallpaper Engine running and Show Desktop; mixed-DPI monitors and Explorer restart remain dedicated compatibility checks.

## 0.5.5 — 2026-09-14

- Read FPS through the installed elevated Collector while keeping the widget unelevated. Accept PresentMon's actual `msBetweenPresents` CSV header.
- Preserve Overlay preferences, automatically follow the foreground app, and reconnect manually selected apps after restart.
- Customize Overlay background color and opacity independently; text stays opaque. Fix the simplified/traditional Chinese foreground-app label.
- Install over the existing version or use the in-app updater; preferences are preserved. Windowed/borderless application presents are supported; exclusive fullscreen and generated-frame counts are not supported.

## 0.5.4 — 2026-09-14

- Keep known device names and fan-channel metadata while a snapshot is stale or unavailable. Live values remain unavailable; new live snapshots replace the retained identity.
- Refresh dynamic monitor and updater status immediately when switching language, including an already completed update check.
- Reproduced both bugs before fixes; headless and real WPF regressions now pass. See docs/PRODUCT-CHECK-0.5.4.md for the bounded product check and remaining gaps.

## 0.5.3 — 2026-09-14

- Separate lock/settings opacity and accessibility fallback rules into MaterialPolicy. Verify that temporary effective opacity never replaces the saved preference, including real WPF lock/settings roundtrips.
- Extract update orchestration from Shell into a headless UpdateCoordinator, retaining UpdateCheck's verification and installation boundary. Use assembly version metadata and test duplicate actions, retries, automatic download, scheduling and late completions after disposal.
- Extract snapshot polling, session peaks and retained capabilities into a headless ReadingSession. Cards and overlay consume the same session; WPF timers, STOP handling and existing polling frequency are preserved.
- Add independent domain tests for missing/malformed/stale snapshots, recovery, duplicate identities, collector restarts and session isolation. This is a maintenance refactor, not a claimed memory improvement.

## 0.5.2 — 2026-09-14

- Reuse fixed sensor-discovery Regex instances and frozen UI brushes to reduce repeated managed allocations without changing the two-second collection interval or sensor selection rules.
- Skip card rendering while the window is hidden or minimized. Continue collecting session peaks and tracking stale readings; refresh immediately when the window is restored.
- Add a repeatable allocation workload and timed UI replay, plus regressions for hidden-window peaks, stale state and immediate restoration. Allocation reductions do not imply the same reduction in resident RAM.

## 0.5.1 — 2026-09-14

- Fix startup registration on machines with missing scheduled tasks. Windows COM interop can report a missing task as FileNotFoundException; setup now treats that specific missing-task result as absent and creates the owned tasks.
- Rerun this installer over a failed 0.5.0 installation to repair startup registration while preserving preferences. Access-denied and unrelated errors still surface; permissions and hardware collection are unchanged.
- Add a real Task Scheduler regression for missing tasks and an isolated elevated integration test for first registration, partial-install repair, disabled-startup preservation and cleanup.

## 0.5.0 — 2026-09-14

- Replace the installed PowerShell UI, collector and startup helpers with C#/WPF. No System.Management.Automation dependency or runtime scripts are shipped; developer build/test tools may still use PowerShell.
- Preserve settings, window geometry, card order/visibility, language, read-only sensor discovery, FPS overlay and verified in-app update flow.
- Upgrade in place with an explicit obsolete-file cleanup list. Preserve Windows components, shared PawnIO and user preferences; no clean install is required. Treat Task Scheduler's omitted default XML values correctly and keep UI/collector privilege separation.
- Compare the baseline and native implementation using synthetic UI replay and sequential real hardware collection. Memory use was lower on the tested host; see [method and results](docs/PERFORMANCE-0.5.0.md), including the limits of single-run measurements.

## 0.4.9 — 2026-09-14

- Make Compact readings independent of window height; Details retains full rows while adapting spacing, with a visible selected state.
- Keep card widths stable when scrolling appears. Use an integrated narrow monitor scrollbar and a rounded keyboard-focus outline.
- Translate pump/system fan channels and explain that a zero-RPM channel does not describe all fans or pumps. No hardware reading or driver behavior changed.
- Stop the overlay timer when unused, avoiding its previous 500 ms wakeups. PowerShell runtime remains in use; no measured memory reduction is claimed.

## 0.4.8 — 2026-09-14

- Add a saved 10–16 px text-size slider in Appearance (12 by default). Sizes use WPF logical pixels and follow Windows display scaling; older large-text preferences migrate automatically.
- Keep native text rendering while resizing card typography proportionally. Tighten card spacing, reflow narrow headers and compact reading groups, and truncate long row labels with full-text tooltips instead of wrapping them into tall cards.
- Verify font persistence across a new process, temperature/header bounds at 240/310 DIP, and existing screen-size and language regressions. Very large text or Details may still require scrolling.

## 0.4.7 — 2026-09-14

- Discover Intel D3D 3D load and shared GPU memory by sensor semantics, without machine-specific identifiers. Shared memory is labeled separately from dedicated VRAM, including in the overlay.
- Adapt monitor cards to detected sensor capabilities: hide absent fan/temperature fields and unused disk columns, retain saved card preferences, and restore fields when sensors return. Null readings remain unavailable rather than becoming zero.
- Show the number of valid mapped readings without the fixed 17-field denominator.

## 0.4.6 — 2026-09-14

- Fix script-policy startup failures on Windows clients: use process-scoped RemoteSigned for the embedded host and collector. No persistent policy changes; Group Policy remains authoritative and unsigned Internet-marked scripts remain blocked.
- Keep text at native layout size in narrow windows, use Display text formatting, and tighten card spacing instead of scaling the whole interface.

## 0.4.5 — 2026-09-14

- Honor a fresh installer shutdown request even when an old startup STOP marker could not be deleted. This fixes a hidden widget retaining files during upgrade.

## 0.4.4 — 2026-09-14

- Fix edge snapping trapping slow drags: derive movement from total cursor displacement since drag start instead of Windows' rebased moving rectangle. Pull away normally without Alt; retain the 24-DIP attraction range.

## 0.4.3 — 2026-09-14

- Group Settings into collapsible sections with wrapping switch labels; center the gear and replace its dotted focus decoration.
- Keep glass translucent when inactive. Opacity now includes card and gradient layers; zero opacity disables blur.
- Lock window movement, resize and card order. Locked Monitor reduces background opacity without fading readings; unlock restores the saved appearance. Tray adds Settings, Always on Top and Lock actions.
- Strengthen left/right and other edge magnets to 24 DPI-scaled logical pixels, apply during drag, and align to visible adjacent-window frames. Alt bypass remains available.
- Add Auto (System) app language and English/Simplified/Traditional Chinese installer detection, with localized setup messages.
- Download updates inside Pulse, with optional automatic downloads, progress, verified size/SHA-256 and Install and Restart. No unattended installation and no security-setting changes.

## 0.4.2 — 2026-09-13

- Translate generated system/storage temperature descriptions, intake/exhaust labels, memory slots/configuration and RAM/VRAM usage labels in Simplified and Traditional Chinese. Preserve model identifiers and user-defined names.
- Use clearer Chinese wording for VRAM temperature, with a tooltip distinguishing memory-chip temperature from GPU core temperature.

## 0.4.1 — 2026-09-13

Promoted to stable / Latest with maintainer authorization after local regression and in-place upgrade passed. The installer binary is unchanged.

- Auto density now measures the actual card viewport in logical WPF units instead of hiding hardware names below a fixed window height.
- It preserves full information first, then reduces padding, then uses compact metric rows. Device descriptions are hidden only when those layouts still do not fit. Hidden cards are excluded from measurement.
- Details keeps full information available. Switching density through Details uses a short opacity transition that respects Windows reduced-motion settings.
- Regression covers resizing down and back up, plus the existing logical display-size matrix.

## 0.4.0 — 2026-09-13 (Pre-release)

### Added

- Adaptive compact cards keep core readings together on laptop-sized work areas; Details restores full device descriptions. Settings can hide/show each card without discarding its order.
- In-app rounded scrollbar with transparent track, hover/drag feedback and a wider interaction area, replacing default arrow buttons.
- Optional click-through game overlay for a selected foreground window, six anchors, compact/detailed layout, selectable hardware readings and local PresentMon FPS capture.
- FPS: current one-second average, rolling-60-second AVG/MIN/1% Low. Application-present metrics do not count generated frames; 1% Low requires at least 100 samples. Missing/denied telemetry is not reported as zero.
- Close-to-tray, restore and explicit Exit. Exit stops the widget, overlay, owned PresentMon capture and requests collector shutdown.
- Start with Windows checkbox controls logon triggers for both verified current-user tasks, with UAC and state readback. On-demand collector startup remains available when logon startup is off.
- Background color, adaptive foreground and 0–100% background opacity. Native glass blur remains Windows-managed.
- Optional automatic GitHub version check and manual update checks, with a download-page action. Automatic checks are opt-in; no unattended installation.
- MIT License, privacy and proposed code-signing policies. SignPath application was submitted on 2026-09-13; no Foundation certificate has been granted.
- Public demonstration image uses fictional hardware/readings.

### Fixed

- Turning off logon startup no longer disables on-demand collector startup. Upgrade preserves the logon preference; inaccessible STOP files no longer abort widget initialization.
- Light backgrounds use dark button labels and readable status colors.
- Installer requests cooperative collector shutdown directly in Inno Setup and lets Windows Restart Manager close the previous UI before replacing files. The separate PulseUpgrade.exe helper has been removed. An active collector blocks replacement instead of being ignored.
- Tray lifetime uses a dispatcher loop instead of a modal dialog, so hiding the window does not end the application.

### Validation / limits

- WPF regression covers tray restore, FPS math/filtering/staleness, six anchors, language/settings, opacity zero, drag and snap. The rebuilt installer completed an in-place upgrade with Bitdefender protection unchanged (exit 0, no reboot). The installed widget reports live sensors. This is one-machine evidence, not an antivirus certification. Real game overlay/click-through, elevated FPS and clean-machine install remain unverified.
- Exclusive fullscreen is not supported by this ordinary topmost overlay. FPS may need admin or Performance Log Users access; Pulse does not alter group membership.
- Bright/dark foreground is based on the selected color; readability over arbitrary desktop content is not guaranteed at zero opacity. Glass blur strength has no slider.

## [0.3.1] — 2026-09-13

### Changed

- Dragged cards lift subtly and follow the pointer; neighboring cards animate into the proposed order before release.
- Drop and cancellation settle with an interruptible 220 ms ease-out. Re-grabbing uses the current displayed position.
- Respect Windows client-area animation preference: reduced motion skips decorative scale and settling transitions. Keyboard menu reordering remains immediate.

### Fixed

- Canceling a preview restores the original order without saving. Successful drops save only once; stable card IDs and user labels remain unchanged.

### Validation / limits

- WPF gesture tests cover preview displacement, cancel, reduced motion, commit and interrupted settling, alongside existing regression tests.
- Pointer feel and edge autoscroll still require hands-on validation. This version does not yet fix the installer inability to close an already-running elevated collector automatically.

## [0.3.0] — 2026-09-13

### Added

- English, Simplified Chinese and Traditional Chinese selection in Settings, applied immediately and persisted across restarts.
- Translated monitor labels/status, Settings, reorder menus, tooltips and accessibility names, with English fallback for unknown language codes.
- Version consistency checks for the native app, installer, build and About text.

### Changed

- Installer checks .NET Framework 4.8 and Windows PowerShell 5.1 before proceeding.
- Missing PawnIO library or driver registration triggers its bundled installer, followed by a presence check before startup registration.
- English-first bilingual README and Release Notes.

### Validation / limits

- WPF language switching, autosave/restore, custom-name preservation and 240-pixel render checks passed; sensor/settings/snap regressions and signed builds passed.
- Windows 10, clean-machine dependency installation and installed upgrade were not validated at release. An upgrade was subsequently reported blocked by a running collector; asking the collector to stop released the occupied files.
- Self-signed Authenticode is not public CA trust; Windows/SmartScreen and antivirus warnings can remain.

## [0.2.0] — 2026-09-13

### Added

- First public GitHub Release: Windows x64 EXE installer with pinned LibreHardwareMonitor libraries, official PawnIO installer, dependency hashes and third-party notices/source archives.
- Compact glass widget for CPU, GPU, memory, NVMe and fan readings; equal DIMM/NVMe columns and live RAM/VRAM usage bars.
- Original vector icons, adaptive typography, opacity controls and solid-background fallback.
- Separate Settings page, editable hardware labels and automatic device discovery.
- Saved position, size, appearance and card order, including debounced autosave and Unicode custom names.
- Card drag ordering, keyboard/context-menu alternatives, and screen/neighbor edge alignment with Alt bypass.
- Current-user collector and delayed widget login tasks; self-signed Marck Wong app/installer signatures and public checksums.

### Fixed

- Stale readings caused by differing AppData views: shared snapshots moved to per-user ProgramData runtime storage.
- Sticky window dragging: snap only when movement ends, allowing free drag-away.
- UTF-8 middle-dot corruption, incomplete Settings gear and ambiguous GPU fan labels; distinguish telemetry channels from physical fan count.
- Replace unreliable widget Startup shortcut with a dedicated login task.

### Validation / limits

- Actual Windows 11 AMD CPU/NVIDIA GPU sensor readings and installation validated; Intel CPU/AMD GPU discovery covered by fixtures only.
- Windows 10, clean-machine installation, multiple-monitor DPI, uninstall/reinstall and the replacement widget task's next reboot remained untested.
- One CPU/GPU and up to two reporting DIMMs/NVMe drives. Missing or ambiguous sensors display a dash; SPD addresses are not physical slot identities.
- Public repository visibility does not grant an open-source license to original application code. Dependencies retain their upstream licenses.

[0.3.1]: https://github.com/medking82/hardware-pulse/releases/tag/v0.3.1
[0.3.0]: https://github.com/medking82/hardware-pulse/releases/tag/v0.3.0
[0.2.0]: https://github.com/medking82/hardware-pulse/releases/tag/v0.2.0
