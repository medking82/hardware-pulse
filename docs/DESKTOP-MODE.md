# Desktop Mode

Desktop Mode is an optional, transparent readout in the existing UI process. It shares
the `ReadingSession`, two-second UI poll and installed Collector with the monitor.
It does not start another collector, PowerShell host or browser, replace the wallpaper,
or install a Wallpaper Engine plugin.

## Use

Settings → Desktop Mode enables the display. Font Size (10–32 logical px), Text Color
Text Opacity (30–100%), Auto Contrast and Row Spacing are independent of the ordinary monitor. Windows display scaling
applies to logical px. Card visibility/order and hardware names use the existing settings.
Unsupported readings disappear; stale readings show dashes and a collector status.

Move on Desktop unlocks the readout and hides the editor. Drag the shaded area to
position it. The system tray's Lock Desktop enables click-through and removes the
editing background. Edit Desktop reopens Settings; Done locks the readout and hides
the editor. Disable Desktop Mode to return to the ordinary monitor. Reset Desktop
Position recovers a misplaced display.

The persistent keys `desktopEnabled`, `desktopLocked`, `desktopFontSize`,
`desktopSpacing`, `desktopColor`, `desktopAutoContrast`, `desktopTextOpacity`, `desktopLeft`, and `desktopTop` are separate from
the monitor's layout keys. Locked is the desktop default; Desktop Mode itself is off
for existing and new installations unless explicitly enabled.

Auto Contrast samples three pixels in transparent padding at most once per two seconds while the desktop is foreground and the readout is locked. Samples are processed locally and never stored or transmitted. Light/dark selection has hysteresis, and a contrasting outline helps on mixed backgrounds. Nearby samples do not guarantee contrast behind every glyph; lower opacity also reduces readability. A saved custom color is preserved on upgrade, and choosing a custom color disables Auto Contrast.

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
  lock/unlock, return from editing, disable, saved appearance, negative-coordinate
  monitors and off-screen recovery. Writes a Desktop Mode render to its test state.
- `scripts/Test-DesktopLayer.ps1 -Interactive`: opt-in live desktop test; briefly toggles
  Show Desktop and restores windows. Checks desktop host discovery, actual native
  click-through flags, non-topmost placement, ordering behind an ordinary test window,
  and visibility after Show Desktop. Uses fixture readings and no Collector.
- Tested locally with Wallpaper Engine running. Mixed-DPI multi-monitor hardware,
  Explorer restart, wallpaper switching and virtual-desktop switching still require
  dedicated interactive coverage; host rediscovery alone is not proof of those cases.

The interactive test is separate from `Validate.ps1` so ordinary validation does not
minimize a user's applications.
