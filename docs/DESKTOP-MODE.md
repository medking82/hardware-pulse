# Desktop Mode

Desktop Mode is an optional, transparent readout in the existing UI process. It shares
the `ReadingSession`, two-second UI poll and installed Collector with the monitor.
It does not start another collector, PowerShell host or browser, replace the wallpaper,
or install a Wallpaper Engine plugin.

## Use

Settings → Desktop contains layout, appearance and reading visibility/order on
one page, adapting to multiple columns in a wide window. Ordinary Desktop and
topmost background opacity are separate from text opacity. Topmost defaults to
a light smoke tint at 55%, dark opaque text and no text blur. Its locked backdrop
fits content while saved window geometry remains available for editing.
This is alpha transparency, not a backdrop blur effect. Desktop blur strength is
not implemented; App glass uses Windows-controlled blur through its existing
Solid Background option.

`Always on top` is optional and off by default. The normal layer remains above
the wallpaper / Wallpaper Engine and below ordinary apps. Enabling the option
places the same panel above apps without creating another Collector. Locked
panels pass mouse input through and do not activate; enter Edit Desktop from the
tray to move or resize, then use the panel's Lock action to restore passthrough.
The saved preference is `desktopAlwaysOnTop`. Exclusive fullscreen games may
hide this ordinary Windows overlay; game-level interaction has not been verified.

Settings → Desktop Mode enables an editable preview and opens its settings. The tray entry uses the same flow. Font Size (10–32 logical px), Text Color,
Text Opacity (30–100%), Auto Contrast and Row Spacing are independent of the ordinary monitor. Windows display scaling
applies to logical px. Card visibility and hardware names use the existing settings.
Reading Order independently arranges individual desktop rows: drag a handle with animated
reordering, press Esc to cancel, or focus the handle and use Up/Down. Missing channels retain
their place, and monitor card order is unchanged. Animation follows Windows reduced-motion settings.
Unsupported readings disappear; stale readings show dashes and a collector status.

Move on Desktop unlocks the readout and hides the editor. Drag the shaded area to
position it, with snapping 16 logical px inside all four screen work-area edges. Pull away to release without
Alt; holding Alt temporarily bypasses snapping. Other app windows are not snap targets.
The system tray's Lock Desktop enables click-through and removes the
editing background. Edit Desktop unlocks the readout and reopens Settings; Done locks the readout and hides
the editor. Disable Desktop Mode to return to the ordinary monitor. Reset Desktop
Position recovers a misplaced display.

The persistent keys `desktopEnabled`, `desktopLocked`, `desktopFontSize`,
`desktopSpacing`, `desktopColor`, `desktopAutoContrast`, `desktopTextOpacity`, `desktopOrder`, `desktopLeft`, and `desktopTop` are separate from
the monitor's layout keys. Entering starts unlocked; Done locks it. Desktop Mode itself is off
for existing and new installations unless explicitly enabled.

Auto Contrast samples six pixels in transparent padding on both sides at most once per two seconds while the desktop is foreground and the readout is locked. Samples are processed locally and never stored or transmitted. Light/dark selection has hysteresis; when sampling is unavailable, protected light text replaces a potentially stale dark choice. Local backing behind labels, values and icons preserves readability on mixed backgrounds without blurring the text layer. Auto mode clamps effective opacity to at least 90%; the stored preference is preserved. Custom color mode allows the full 30–100% range and disables sampling. Nearby samples do not represent every glyph's background.

Download and Upload participate in Reading Order. Their units follow Cards → Network Speed Unit. Network uses the same Collector, shows one busiest adapter, and does not add counters from overlapping adapters. Monitor icon/temperature color settings are separate from Desktop Auto Contrast.

## Window boundary

`DesktopLayer` places only Pulse's transparent top-level tool window immediately above
Explorer's visible desktop icon host (`Progman` or `WorkerW` containing
`SHELLDLL_DefView`). With the normal wallpaper arrangement this is above Wallpaper
Engine and below ordinary applications. It does not reparent into Explorer, inject
code, send undocumented wallpaper commands, or change other applications' windows.

A foreground-change hook updates ordering; the existing UI poll rediscovers the host
after shell/wallpaper changes. If no host exists, the readout hides and retries. The
system tray stays available. Closing the view removes the hook. Locked adds
`WS_EX_TRANSPARENT`; `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW` keep the readout out of
activation and taskbar flows.

Explorer's desktop host arrangement is an implementation detail, not a documented
Wallpaper Engine integration API. Changes to Windows shell layout, virtual desktops,
or third-party desktop replacements may need further compatibility work. Do not
claim compatibility with all of those environments from a single-machine test.

## Validation

- `scripts/Test-Native.ps1`: shared readings, no window buttons, independent typography,
  preview entry, independent ordering/persistence, drag cancellation, lock/unlock, return from editing, disable, saved appearance, negative-coordinate
  monitors and off-screen recovery. Writes a Desktop Mode render to its test state.
- `scripts/Test-DesktopLayer.ps1 -Interactive`: opt-in live desktop test; briefly toggles
  Show Desktop and restores windows. Checks desktop host discovery, actual native
  four-edge snap hooks, click-through flags, non-topmost placement, ordering behind an ordinary test window,
  and visibility after Show Desktop. Uses fixture readings and no Collector.
- Tested locally with Wallpaper Engine running. Mixed-DPI multi-monitor hardware,
  Explorer restart, wallpaper switching and virtual-desktop switching still require
  dedicated interactive coverage; host rediscovery alone is not proof of those cases.

The interactive test is separate from `Validate.ps1` so ordinary validation does not
minimize a user's applications.
