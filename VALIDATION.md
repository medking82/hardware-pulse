# Local validation — 2026-09-13

## 0.5.3 maintenance — 2026-09-14

- Final Validate.ps1 and WPF lock/settings/save tests passed. Saved opacity remains unchanged; fallback and unknown settings preservation are covered.
- The signed 0.5.3 installer upgraded this host without a reboot. Settings remained byte-identical, installed EXE matched the build hash, and the collector produced fresh snapshots. UI/collector retained limited/elevated tokens.

- UpdateCoordinator extraction: headless fake-client tests cover stable/invalid metadata, repeated actions, six-hour check scheduling, download retry, ready-cache reuse, install cancellation and ignored late check/download results after disposal. Verification/process-launch code in UpdateCheck is unchanged. This checkpoint was tested before packaging.
- Validate.ps1 passed, including the new headless domain tests compiled without WPF/WinForms/app references. Missing, malformed and stale input, fresh recovery, duplicate sequence identity, collector restart, usage capability replacement and isolated session histories are covered.
- Native WPF tests still pass for hidden-window peaks/staleness, immediate restore, cards, language, settings and updater behavior. STOP/collector startup logic and timer intervals were not moved. No new performance improvement is claimed.
- See [runtime ownership](docs/RUNTIME-BOUNDARIES.md) for the extracted boundary and remaining updater coupling.

## 0.5.2 UI allocations — 2026-09-14

- `scripts/Validate.ps1` passed sensor differential, native WPF/startup, package, snap and updater checks. New regression checks cover peaks and stale state while hidden, and immediate fresh readings on restore. Existing appearance tests exercise cloned opacity brushes with shared frozen palette brushes.
- Before/after workloads use the same harness and synthetic readings. Allocations fell 39.0% visible and 73.1% hidden over 300 accelerated updates. A separate timed replay found Private Bytes near 91 MiB in both versions, so no substantial steady-state RAM reduction is claimed. See [method and results](docs/PERFORMANCE-0.5.2.md).
- The final signed 0.5.2 installer upgraded this host without a reboot. Installed executable hash matched the build, settings remained byte-identical, the new collector produced advancing snapshots, and UI/collector retained limited/elevated tokens respectively. This does not verify every friend's hardware.

## 0.5.1 missing-task registration — 2026-09-14

- Reproduced the reported 0x80070002 FileNotFoundException with the unmodified SchedulerStore by querying a GUID-named nonexistent task through the real Task Scheduler COM adapter. This fails before driver loading. A disabled Scheduler service would fail earlier at Connect; access denied has a different HRESULT. Missing app/driver files do not explain this isolated reproduction.
- Added the failing real-COM regression before fixing the adapter. Catch only FileNotFoundException with HRESULT 0x80070002, alongside the existing COMException case. Other failures continue to propagate; task ownership, privileges and sensor collection are unchanged.
- `scripts/Validate.ps1` passed package, sensor differential, native WPF/startup, snap and updater checks. `scripts/Test-SchedulerIntegration.ps1`, run elevated under Windows PowerShell 5.1, passed first registration into an empty GUID folder, partial-install repair, disabled preference preservation and owned cleanup. It did not modify or run production tasks.
- The signed 0.5.1 installer upgraded this host from 0.5.0 successfully without a reboot. Installed executable SHA-256 matched the build; preferences remained identical. New widget and collector processes had limited/elevated tokens respectively, and the collector produced fresh snapshots.
- The two friends' post-fix readings and fresh-machine driver behavior remain unverified. This fixes the demonstrated registration failure, not every possible sensor compatibility issue. The installer remains self-signed.

## 0.5.0 native migration — 2026-09-14

- `scripts/Validate.ps1` passes native package/cleanup checks, seven native-versus-legacy sensor cases, WPF settings/font/card/lock/language/stale-data checks, FPS math/filtering, overlay anchors, snap and updater validation. Baseline scripts remain for comparison only.
- A signed 0.5.0 installer upgraded the existing 0.4.9 installation on this Windows 11 host. Setup logged success without a reboot. The old collector exited; the obsolete top-level PS1/C# files were absent afterward, and the installed executable hash matched the tested build.
- The running native widget had a limited token, the collector an elevated token, and the widget had no System.Management.Automation module. The UI reported 17 live mapped readings with advancing timestamps. The native collector had independently matched all 17 baseline capability keys in the live comparison.
- Saved font, opacity, names and card preferences survived. The pre-upgrade saved state had lock enabled and 20% opacity, so its initial locked background used 5% effective opacity. The user subsequently unlocked/moved the live window; exact live-position equality is therefore not claimed. Isolated migration tests verify saved geometry/order and unknown settings survive restoration.
- The installed `--disable-startup` and `--enable-startup` helpers both succeeded against the real Task Scheduler; the original task XML was then restored. Default-value omission and trigger element order have regression coverage.
- Claude Native Review completed for frozen packet `e778f74508a39c1ce8ac4ea4a4694ec0af9116686378d2316504229d8cef2433` (0 P0, 0 P1, 1 P2, 1 P3). The P2 trigger-order concern was corrected to follow the schema, although the local TaskDefinition parser accepted the earlier order. The P3 install-cancellation message was corrected. Deterministic checks and the installed startup roundtrip passed afterward; no second model review was run for these bounded fixes.
- See [performance methodology and results](docs/PERFORMANCE-0.5.0.md). Single-host comparisons do not establish battery-life or live-game improvements. Fresh-machine driver installation, Windows 10 hardware, multi-monitor DPI transitions, uninstall/reinstall and a full reboot remain unverified for 0.5.0. Signing is self-signed, not public CA trust.

The sections below are historical records, not the current runtime or package description.

## 0.3.1 candidate
Historical validation below describes the earlier candidate. Version 0.3.1 was subsequently published.

WPF gesture tests pass for preview displacement without order mutation, cancel without save,
reduced-motion reset, exactly one successful-drop callback, reverse reordering, and re-grab
during settling followed by cancellation. Existing sensor/settings/snap/language regression
also passes. Hand-operated pointer feel and edge autoscroll remain unverified. The installed
app and published 0.3.0 release are not replaced by this source update.

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
