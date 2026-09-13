# Hardware Pulse

A compact hardware widget by **[Marck Wong](https://github.com/medking82)** for **Windows 10 22H2 / Windows 11 x64**.

[Download Latest EXE](https://github.com/medking82/hardware-pulse/releases/latest/download/HardwarePulse-Setup.exe) · [Release Notes](https://github.com/medking82/hardware-pulse/releases/latest)

CPU, GPU, memory, NVMe and fan monitoring with live RAM/VRAM usage.
WPF glass background, original SVG icons, Segoe UI typography, adjustable background opacity,
width-adaptive layout and persistent card order. No HWiNFO, browser, Codex or cloud service is
required to run it. Windows .NET Framework 4.8 and Windows PowerShell 5.1 are OS prerequisites.

## Use

- Drag the six-dot handle in a card header to reorder it. Release to save; Esc cancels.
- Right-click a card for Move Up / Move Down; Shift+F10 opens the menu from its focused handle.
- Drag the Pulse title to move the window. After release, edges snap within 12 pixels to screen edges or adjacent windows, including top/bottom alignment. Hold Alt to bypass. Windows do not follow each other.
- Live shows current readings; Session Max collects peaks since the widget opened.
- Open the gear for Settings: opacity, Solid Background, Larger Text, Always on Top and Hardware Names. Leave a name blank for automatic device information.
- Windows 10 uses a solid fallback where the Windows 11 backdrop API is unavailable.
- DIMMs and the two NVMe readings use equal columns. RAM/VRAM usage stays live in Session Max; GB uses binary units and OS/driver-reported usable capacity, which may be smaller than installed capacity.
- GPU Fan Speed lists reported RPM channels, not the number of physical fans.

Discovery matches semantic sensor names/types within one CPU, one GPU, up to two temperature-reporting DIMMs and two NVMe drives. NVIDIA is preferred on multi-GPU systems, then discrete AMD, then integrated graphics. Unknown or ambiguous readings show a dash. Voltage never falls back to VID.

DIMM brand, model and installed slots come from SMBIOS. SPD #1/#3 are sensor addresses, **not A2/B2**: identical modules cannot be assigned to physical slots by model name alone. Occupied slots are reported separately. Assign custom slot names only after confirming their sensors. NVMe volume letters require an unambiguous disk model match; fan headers do not identify physical case placement.

The 9700X / RTX 5080 / B850M Mortar machine has live validation. Its verified SYS1/SYS3 mapping and existing owner's labels are retained. Other CPU/GPU fixtures have automated coverage; other physical machines remain untested.
Sensors are read-only; this app does not tune fan curves or Curve Optimizer.

## Installer

The release asset `HardwarePulse-Setup.exe` (version 0.2.0) bundles the application, pinned LibreHardwareMonitor libraries,
license notices/source archives and official PawnIO 2.2.0 prerequisite installer. No runtime downloads. The target Windows versions include .NET Framework 4.8 and Windows PowerShell 5.1; setup checks the .NET requirement.
The installer requires UAC elevation and is intended for installation by the current administrator
account. It installs protected code in Program Files and registers the current-user interactive
collector task plus a separate limited-permission widget task delayed 10 seconds after login. Shared PawnIO and user preferences remain after uninstall.
Do not use an alternate administrator account to install for a standard user in this initial version.

Preferences are in `%LocalAppData%\HardwarePulse`. Shared sensor snapshots are in `%ProgramData%\HardwarePulse\<UserSID>\runtime` to avoid package-local AppData views hiding collector updates. Close/reopen keeps
geometry, opacity, pin state, Solid Background, Larger Text and card order. Geometry and preferences autosave 750 ms after changes settle, so they do not rely on a normal close before restart. A collector failure is visible as STALE/OFFLINE.

The release app and installer use a **self-signed Authenticode certificate, CN=Marck Wong**. This is not a public-CA-verified publisher identity: Windows/SmartScreen can still block or warn, and antivirus detection is independent of signing. No Root/TrustedPublisher certificate or security exclusion is installed. The non-exportable private key stays in the author's Windows certificate store and is never distributed. Releases include checksums and the public certificate for inspection. The current self-signed build is not timestamped.
See [Microsoft signing options](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options).

## Build

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
Public repository visibility alone does not grant an open-source license to the original Hardware Pulse code.

## Validation boundary

Sensor parsing regressions, script/XML validation, native compilation, installer compilation,
real WPF Settings navigation, autosave/restore, discovery fixtures, usage units, snap geometry and current-machine installation are checked locally. The supported minimum window is 240 x 340 logical pixels; smaller heights scroll. Windows 10 compatibility is based on the API baseline and fallback, not a Windows 10 machine test. Clean-machine installation, multi-monitor DPI changes, uninstall/reinstall and a fresh reboot of the new widget task remain unverified. See [validation notes](VALIDATION.md).
