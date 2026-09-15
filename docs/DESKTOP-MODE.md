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
Text Opacity (0–100%), Auto Contrast and Row Spacing are independent of the ordinary monitor. Windows display scaling
applies to logical px. Card visibility and hardware names use the existing settings.
Reading Order independently arranges individual desktop rows: drag a handle with animated
reordering, press Esc to cancel, or focus the handle and use Up/Down. Missing channels retain
their place, and monitor card order is unchanged. Animation follows Windows reduced-motion settings.
Unsupported readings disappear; stale readings show dashes and a collector status.

Move on Desktop unlocks the readout and hides the editor. Drag the shaded area to
position it, with snapping 16 logical px inside all four screen work-area edges. Pull away to release without
Alt; holding Alt temporarily bypasses snapping. Nearby visible app windows are also snap targets. Attraction ramps up as an edge approaches; Alt bypasses snapping.
The system tray's Lock Desktop enables click-through and removes the
editing background. Edit Desktop unlocks the readout and reopens Settings; Done locks the readout and hides
the editor. Disable Desktop Mode to return to the ordinary monitor. Reset Desktop
Position recovers a misplaced display.

The persistent keys `desktopEnabled`, `desktopLocked`, `desktopFontSize`,
`desktopSpacing`, `desktopColor`, `desktopAutoContrast`, `desktopTextOpacity`, `desktopOrder`, `desktopLeft`, and `desktopTop` are separate from
the monitor's layout keys. Entering starts unlocked; Done locks it. Desktop Mode itself is off
for existing and new installations unless explicitly enabled.

Auto Contrast samples six pixels in transparent padding on both sides at most once per two seconds while the desktop is foreground and the readout is locked. Samples are processed locally and never stored or transmitted. Light/dark selection has hysteresis; when sampling is unavailable, protected light text replaces a potentially stale dark choice. Local backing behind labels, values and icons preserves readability on mixed backgrounds without blurring the text layer. Auto Contrast and topmost mode respect the full 0–100% text and background opacity ranges. Custom color mode disables sampling. Nearby samples do not represent every glyph's background.

Download and Upload participate in Reading Order. Their units follow Cards → Network Speed Unit. Network uses the same Collector, shows one busiest adapter, and does not add counters from overlapping adapters. Monitor icon/temperature color settings are separate from Desktop Auto Contrast.

## Local adaptive text contrast and Desktop FPS

Settings → Desktop → Local adaptive text contrast is opt-in. It uses an in-memory
pixel mask to choose black or white beneath each part of the text, with hysteresis
instead of a fixed switching delay. SVG colors remain independent. One background
capture at a time is scheduled at 100 ms intervals. Moving/resizing discards obsolete
frames; unsupported capture or panels above four million physical pixels fall back
to standard text. Windows 10 build 19041+ is required. Pulse is excluded from screen
capture while enabled, so screenshots/recordings may omit the panel. Turning it off
restores capture visibility. HDR and fast-game performance are not yet field-validated.

A locked panel at 0% background opacity has no outer frame. Row separators remain.
Text Color can always be selected; accepting a color disables both automatic modes.
In Desktop Readings, enable FPS to reuse the existing FPS target and collector without
opening the separate game overlay. FPS is off in Desktop by default. Missing capture
data displays a waiting state rather than a fabricated zero.

Desktop toggle shortcut defaults to Ctrl+Alt+F10. Click its button to enter a custom
combination; Escape cancels. Registration failure preserves the existing binding.
Use at least two of Ctrl/Alt/Shift plus a letter, digit or F1–F11; Windows-key and
F12 shortcuts are reserved. Disable the shortcut to release it. Toggling shows a
locked Desktop or hides it without opening App or requesting focus. Enable Always
on top beforehand to see it over a game. Registration checks detect registered
global conflicts, not every game's internal key bindings.

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
