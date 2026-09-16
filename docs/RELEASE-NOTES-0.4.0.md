# Hardware Pulse 0.4.0 — Pre-release

Compact Windows hardware monitoring by [Marck Wong](https://github.com/medking82).

Download **HardwarePulse-Setup.exe** under Assets. Windows 10 22H2 / Windows 11 x64 are the supported targets. The package bundles application libraries, PresentMon and the PawnIO installer; Windows .NET Framework 4.8 and PowerShell 5.1 are checked during setup. No HWiNFO installation is required.

## Changes

- Native Inno Setup upgrade preparation replaces the separate PulseUpgrade.exe helper that Bitdefender quarantined. The collector exits before file replacement; Windows Restart Manager handles the previous UI.
- Compact adaptive cards, a rounded in-app scrollbar, Details mode and persistent card visibility.
- Close to tray, explicit Exit and Start with Windows controls.
- Background color, adaptive text contrast, 0–100% background opacity and optional GitHub update checks. Update actions open the download page; installation is not automatic.
- Experimental windowed/borderless game overlay with six positions, selectable metrics and PresentMon current/AVG/MIN/1% Low FPS.
- English, Simplified Chinese and Traditional Chinese UI; preserved settings and card order.

## Validation and limits

Local upgrade from a running previous version completed with exit 0 and no reboot, with Bitdefender protection unchanged. Live sensors, cooperative shutdown/relaunch and startup on/off were verified. WPF, sensor, FPS calculation, drag, settings and logical-layout regression tests passed. This is one-machine evidence, not antivirus certification or validation on every laptop.

This installer is **self-signed**, not signed by a publicly trusted certificate. SignPath application approval is pending. Windows may show an unknown-publisher/reputation warning. No security exclusion is required by this installer. Report any detection with its exact name and file hash; do not disable protection to test it.

Live-game FPS, clean-machine installation and reboot behavior need further testing. Exclusive fullscreen is unsupported; FPS capture may require administrator or Performance Log Users access. Hardware and driver support varies. Native glass blur is Windows-managed, without a blur-radius slider. Linux/macOS remain planned.

The maintainer authorized this testing release with a new-model-review waiver; the earlier partial dual-review failure remains recorded. See [validation](https://github.com/medking82/hardware-pulse/blob/v0.4.0/docs/RELEASE-0.4.0.md) and [full changelog](https://github.com/medking82/hardware-pulse/blob/v0.4.0/CHANGELOG.md).

[简体中文](RELEASE-NOTES-0.4.0.zh-CN.md)
