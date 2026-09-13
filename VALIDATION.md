# Local validation — 2026-09-13

## 0.3.0 candidate

- English, Simplified Chinese and Traditional Chinese switch immediately in isolated real WPF sessions. Language selection autosaves and restores in a second process; custom Unicode names and stable card order IDs survive switching.
- Rendered and inspected 240-pixel Simplified Chinese monitor and Traditional Chinese Settings captures. Test sessions also render all three languages at that width.
- Script/sensor/settings/snap regression checks, UTF-8 BOM checks and version consistency checks pass. The native EXE and 0.3.0 installer compile and carry the existing self-signed author certificate (untrusted chain, not public CA trust).
- This candidate has not upgraded the installed app or replaced GitHub release 0.2.0. Earlier installed-machine results below describe the previous version.

## Earlier installed validation

- PowerShell syntax, XAML/SVG parsing and sensor regression passed.
- Snap geometry tests passed: work-area edges, adjacent window edges, negative monitor coordinates,
  non-overlapping targets, movement outside the 12-pixel attraction threshold and dragging away from a snapped edge.
- Original SVG icons and Windows ICO render in the installed native widget.
- Header drag reordered the installed CPU card; saved order restored across launches.
- Compact UI supports a 240-pixel minimum logical width; the current layout renders all five cards
  without horizontal overflow at that width when enough vertical space is available.
- Installed EXE setup completed on the target Windows 11 computer. Its collector supplies 16 live
  mapped readings from ProgramData, and the installed widget displays continuously advancing data.
- The known legacy collector task is disabled. A reboot confirmed collector startup, but not the original shortcut-based GUI startup. The replacement ordinary-permission widget task was subsequently launched through Task Scheduler and opened the live UI. Its next real login/reboot remains untested.
- Schema 2 discovery supplies 17 live readings, both GPU RPM channels, RAM/VRAM usage, Kingston part/occupied-slot metadata and correct NVMe volume letters on this machine.
- Separate Settings navigation was inspected in the real installed WPF app. WPF tests cover autosave while the window remains open, Unicode names restored in a new process, atomic settings replacement, card visibility and SVG stroke bounds.
- Windows PowerShell 5.1 source files containing non-ASCII text require a UTF-8 BOM. Settings JSON is read explicitly as UTF-8. Regression tests cover the middle-dot separator.

The earlier stale-data failure was caused by a consumer seeing an old AppData snapshot while the
elevated producer wrote fresh data. Using a shared ProgramData sensor directory resolves that
observed split. UI preferences remain per-user; raw snapshots/settings are never committed.

Manual screen-edge snapping was not verified: the UI automation driver rejected a drag endpoint
outside the captured widget bounds. A sticky live-drag regression was corrected by snapping on WM_EXITSIZEMOVE only; dragging is never intercepted. The snap
calculation is covered by deterministic tests. Multi-monitor DPI changes, following another app,
clean-machine installation, uninstall/reinstall and actual reboot remain untested. Following another
app is not implemented: snapping aligns the widget when it is dragged.

Version 0.2.0 app/installer carry a SHA-256 Authenticode signature from the user's explicitly requested self-signed CN=Marck Wong certificate. Windows reports the untrusted root chain, as expected. The private key is non-exportable in CurrentUser/My, not in the repository. No Root/TrustedPublisher certificate or security exclusion was added. No timestamp is claimed.

A development-time PowerShell SVG generation command was blocked by Bitdefender's heuristic scanner. That command was not retried or excluded; the SVG was edited directly as source. This does not establish a malware verdict or a clean-antivirus guarantee for the app. The installed app continued running.

Windows 10 22H2 compatibility uses the supported OS API baseline and a solid backdrop fallback; no Windows 10 host is available for live verification. Fresh-machine driver installation, uninstall/reinstall, multi-monitor DPI changes and a second complete reboot remain outside the verified boundary.
# Prerequisite checks (local candidate)

The installer now checks .NET Framework 4.8 and Windows PowerShell 5.1 before installation, and requires both the PawnIO library and driver registration before skipping its bundled installer. A missing prerequisite after PawnIO setup prevents startup registration.

Inno Setup compilation and `scripts/Validate.ps1` pass. Read-only checks on the development host confirm .NET Release 533509, PowerShell 5.1, the PowerShell executable, PawnIOLib.dll and PawnIO service registration. This does not exercise missing-dependency installation, damaged Windows components or blocked driver loading; those require a disposable Windows machine. The candidate has not replaced the published 0.2.0 installer.
