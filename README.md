# Hardware Pulse

[English](README.md) · [简体中文](README.zh-CN.md)

![Hardware Pulse actual App and Desktop UI with fictional demo data](docs/showcase/pulse-hero.png)

*Actual WPF UI and shipped SVG icons, rendered with fictional demo data on a composed background. [Render source](scripts/Render-Hero.ps1).*

[Changelog](CHANGELOG.md)

[Visual quick guides](docs/QUICKSTART.md) — Desktop move/resize/lock · Screenshot mode · App controls and FPS

[MIT License](LICENSE) · [Privacy policy](PRIVACY.md) · [Code signing policy](SIGNING.md)

[Dependency maintenance and Linux/macOS roadmap](docs/DEPENDENCIES.md)

**Experimental shared Desktop:** [0.7.0-preview.1 — Windows / Linux / macOS, x64 / ARM64](https://github.com/medking82/hardware-pulse/releases/tag/v0.7.0-preview.1). Self-contained downloads with live CPU/RAM/network and opt-in Codex quota. This preview does not replace the stable Windows App; [feature gaps, signing status and launch guide](docs/DESKTOP-PREVIEW.md).

**[Download 0.6.29 EXE](https://github.com/medking82/hardware-pulse/releases/download/v0.6.29/HardwarePulse-Setup.exe)** · [0.6.29 release notes](https://github.com/medking82/hardware-pulse/releases/tag/v0.6.29)

**0.5.0** migrates the installed UI, collector and startup helpers to C#/.NET without a PowerShell runtime dependency. [Measured comparison](docs/PERFORMANCE-0.5.0.md). The installer remains self-signed; The SignPath Foundation application was declined; no publicly trusted certificate is claimed.

See [Desktop Mode](docs/DESKTOP-MODE.md) for wallpaper integration, appearance controls and validation limits.

Use **Ctrl+Alt+F10** to show/hide Desktop without opening App. Customize or disable it in Settings → Desktop; enable Always on top for games.

Latest: **0.6.29** preserves the Windows WPF UI and adds an installed Antigravity CLI quota fallback, explicit CLI source labels, sanitized update failure codes and consistent card-heading scaling. Existing settings, hardware, FPS and Desktop behavior are retained.

Appearance → Colors selects Hardware Colors or a custom Unified Color for Monitor icons and temperatures. Cards → Network Speed Unit selects Auto, KB/s, MB/s or Mbit/s (decimal units; 1 MB/s = 8 Mbit/s). Network shows the busiest adapter by combined download/upload rate, with its name visible, and is not the sum of all adapters. Desktop reading order includes Download and Upload.

Settings in 0.6.29: Settings → AI Quota enables independent Codex,
Antigravity and Claude quota readings in Monitor and Desktop Mode. Only remaining
percentages and reset times are read, every five minutes. Token Monitor is not
required. Sign in through Codex/Claude Code first; keep Antigravity running.
Each provider is off by default. Expired login must be renewed in its owning app.

Version 0.6.29 also displays negotiated Network Link Speed for the selected
adapter, in Mbit/s or Gbit/s. This is the adapter connection rate, not a measured
internet speed or the current Download/Upload throughput. Missing speed is shown
as unknown, and disconnected adapters are labeled.

[English](README.md) · [简体中文](README.zh-CN.md)

**[Download Latest EXE](https://github.com/medking82/hardware-pulse/releases/latest/download/HardwarePulse-Setup.exe)** · [Release Notes](https://github.com/medking82/hardware-pulse/releases/latest)

Author:**[Marck Wong](https://github.com/medking82)**

<a id="en"></a>

## English

### Updates and desktop placement

- General offers optional background update checks and downloads. The installer is validated against the fixed GitHub repository, expected size and SHA-256 before **Install and Restart** launches it. Installation requires a click and Windows elevation; failure or cancellation can be retried.
- **Lock Position and Size** disables window movement, resizing and card reordering. Locked Monitor uses one-quarter of your saved background opacity and disables blur; Settings remains readable. Unlock in Settings or the tray to restore the previous appearance. Solid/high-contrast preferences take precedence. This does not embed Pulse into the desktop layer.
- App language defaults to **Auto (System)**, with English fallback; installer supports English, Simplified and Traditional Chinese, preselected from Windows UI language.

The published version is **0.6.29** with multilingual UI and animated card reordering. Use the download link above for the latest installer.

A compact hardware widget by **[Marck Wong](https://github.com/medking82)** for **Windows 10 22H2 / Windows 11 x64**.

[Download Latest EXE](https://github.com/medking82/hardware-pulse/releases/latest/download/HardwarePulse-Setup.exe) · [Release Notes](https://github.com/medking82/hardware-pulse/releases/latest)

CPU, GPU, memory, NVMe and fan monitoring with live RAM/VRAM usage.
WPF glass background, original SVG icons, Segoe UI typography, adjustable background opacity,
width-adaptive layout and persistent card order. No HWiNFO, browser, Codex or cloud service is
required to run it. Windows .NET Framework 4.8 is required. The installed C#/WPF runtime does not load PowerShell or run scripts.

### Use

- **Settings → Language** switches instantly between English, Simplified Chinese and Traditional Chinese and saves your selection. Auto (System) is the default; explicit choices are preserved. Device models, custom names and units stay unchanged.

- Drag the six-dot handle in a card header to reorder it. Release to save; Esc cancels.
- Right-click a card for Move Up / Move Down; Shift+F10 opens the menu from its focused handle.
- Drag the Pulse title to move the window. During dragging and on release, edges snap within 24 DPI-scaled logical pixels to screen edges or adjacent windows, including top/bottom alignment. Hold Alt to bypass. Windows do not follow each other.
- Live shows current readings; Session Max collects peaks since the widget opened.
- Open the gear for Settings: opacity, Solid Background, Larger Text, Always on Top and Hardware Names. Leave a name blank for automatic device information.
- Glass stays translucent when inactive. Unsupported composition uses a solid fallback.
- DIMMs and the two NVMe readings use equal columns. RAM/VRAM usage stays live in Session Max; GB uses binary units and OS/driver-reported usable capacity, which may be smaller than installed capacity.
- GPU Fan Speed lists reported RPM channels, not the number of physical fans.

Discovery matches semantic sensor names/types within one CPU, one GPU, up to two temperature-reporting DIMMs and two NVMe drives. NVIDIA is preferred on multi-GPU systems, then discrete AMD, then integrated graphics. Unknown or ambiguous readings show a dash. Voltage never falls back to VID.

DIMM brand, model and installed slots come from SMBIOS. SPD #1/#3 are sensor addresses, **not A2/B2**: identical modules cannot be assigned to physical slots by model name alone. Occupied slots are reported separately. Assign custom slot names only after confirming their sensors. NVMe volume letters require an unambiguous disk model match; fan headers do not identify physical case placement.

The 9700X / RTX 5080 / B850M Mortar machine has live validation. Its verified SYS1/SYS3 mapping and existing owner's labels are retained. Other CPU/GPU fixtures have automated coverage; other physical machines remain untested.
Sensors are read-only; this app does not tune fan curves or Curve Optimizer.

### In-place upgrade

Install 0.6.29 over the existing version; a clean install is not required. Setup stops the old collector, replaces the app and its two owned startup tasks, and removes an explicit list of obsolete app scripts/source files. Preferences and desktop geometry remain in LocalAppData. Windows PowerShell and shared PawnIO remain installed. An interrupted or failed upgrade may require rerunning setup; file cleanup is not a transactional rollback.

### Installer

Setup checks .NET Framework 4.8 before installation. If the PawnIO library or driver registration is missing, it runs the bundled official installer and checks again before registering startup. Missing or damaged Windows components require Windows repair; setup does not change Windows features or security settings. These checks establish installation presence, not successful driver loading under every security policy.

The release asset `HardwarePulse-Setup.exe` (version 0.6.29) bundles the application, pinned LibreHardwareMonitor libraries,
license notices/source archives and official PawnIO 2.2.0 prerequisite installer. No runtime downloads. The target Windows versions include .NET Framework 4.8; setup checks that requirement.
The installer requires UAC elevation and is intended for installation by the current administrator
account. It installs protected code in Program Files and registers the current-user interactive
collector task plus a separate limited-permission widget task delayed 10 seconds after login. Shared PawnIO and user preferences remain after uninstall.
Do not use an alternate administrator account to install for a standard user in this initial version.

Preferences are in `%LocalAppData%\HardwarePulse`. Shared sensor snapshots are in `%ProgramData%\HardwarePulse\<UserSID>\runtime` to avoid package-local AppData views hiding collector updates. Close/reopen keeps
geometry, opacity, pin state, Solid Background, Larger Text and card order. Geometry and preferences autosave 750 ms after changes settle, so they do not rely on a normal close before restart. A collector failure is visible as STALE/OFFLINE.

The release app and installer use a **self-signed Authenticode certificate, CN=Marck Wong**. This is not a public-CA-verified publisher identity: Windows/SmartScreen can still block or warn, and antivirus detection is independent of signing. No Root/TrustedPublisher certificate or security exclusion is installed. The non-exportable private key stays in the author's Windows certificate store and is never distributed. Releases include checksums and the public certificate for inspection. The current self-signed build is not timestamped.
See [Microsoft signing options](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options).

### Build

Use PowerShell 7 on Windows 11 (build tool only):

```powershell
./scripts/Validate.ps1
./scripts/Build.ps1 -Installer
# Optional: use your own code-signing certificate in CurrentUser\My.
./scripts/Build.ps1 -Installer -SigningCertificateThumbprint <thumbprint>
```

Downloads are locked by SHA-256 in `dependencies.lock.json`. The build bootstraps the pinned
Inno Setup compiler in `vendor/inno` and invokes console children without windows. Build outputs,
downloaded dependencies, personal settings and sensor logs are excluded from Git. App icon SVG
and its matching Windows ICO are in `assets`; runtime line icons render SVG path geometry in WPF.

Text uses UTF-8 throughout. PowerShell scripts use UTF-8 with BOM for Windows PowerShell 5.1;
JSON readers/writers use UTF-8 explicitly. `.editorconfig` and validation enforce this boundary.

Third-party source locations and notices are under `licenses`. The full upstream source archive
for LibreHardwareMonitor is bundled without changes. Dependencies retain their upstream licenses.
Original Hardware Pulse code is licensed under the [MIT License](LICENSE).

### Validation boundary

Sensor parsing regressions, script/XML validation, native compilation, installer compilation,
real WPF Settings navigation, autosave/restore, discovery fixtures, usage units, snap geometry and current-machine installation are checked locally. The supported minimum window is 240 x 340 logical pixels; smaller heights scroll. Windows 10 compatibility is based on the API baseline and fallback, not a Windows 10 machine test. Clean-machine installation, multi-monitor DPI changes, uninstall/reinstall and a fresh reboot of the new widget task remain unverified. See [validation notes](VALIDATION.md).
