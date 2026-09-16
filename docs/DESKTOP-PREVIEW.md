# Shared Desktop preview

The 0.7.0 preview is the new shared host for Windows, Linux and macOS, on x64
and ARM64. It is an experimental, separate App rather than a replacement for the
stable Windows WPF installer. Platform feature parity is not complete.

## Available

- Live CPU utilization, physical RAM and download/upload rates for one selected
  network interface, refreshed once per second. Windows CPU currently requires
  a single processor group; macOS RAM is a documented used-memory estimate.
- Resizable monitor cards; System, Light and Dark themes; grouped Settings.
- Optional Codex quota using an existing file login. It is off on a new profile;
  enable it explicitly in Settings. Credentials are read-only and never packaged.
- Open Pulse and Quit Pulse in tray/menu bar where the desktop supports it.
  Closing the window exits; the App does not hide itself on close.
- Separate preview settings remember size, theme, selected network and Codex
  opt-in. Existing installed Windows Pulse settings are not migrated or changed.

## Packages and launch

Choose the archive matching your OS and CPU architecture. Extract the entire
archive into its own folder; do not copy just the executable or mix versions.
The .NET runtime is included. Each archive has a SHA-256 sidecar and an internal
manifest listing the source commit, preview version and file hashes.

| Package | Native CI environment | Launch |
| --- | --- | --- |
| win-x64 | Windows Server 2022 x64 | Pulse.Desktop.exe |
| win-arm64 | Windows 11 ARM64 | Pulse.Desktop.exe |
| linux-x64 | Ubuntu 24.04 x64, X11/Xvfb | ./Pulse.Desktop |
| linux-arm64 | Ubuntu 24.04 ARM64, X11/Xvfb | ./Pulse.Desktop |
| osx-x64 | macOS 15 Intel | Pulse Preview.app |
| osx-arm64 | macOS 15 Apple Silicon | Pulse Preview.app |

Windows can extract with `tar -xf <archive.tar.gz>` from an ordinary terminal.
Linux needs its normal desktop graphics/font libraries; self-contained .NET
does not bundle the OS. Wayland-native interaction has not been validated.
Windows binaries are not publisher-signed. The macOS bundle is not Developer ID
signed or notarized; download quarantine/Gatekeeper behavior is not established
by CI launch tests. No security settings are changed by the package.

macOS notices and dependency information are inside Contents/Resources so they
remain with the App when it is moved. Windows/Linux notices are in licenses/.

## Not included yet

Temperature/fan sensors, GPU telemetry, FPS capture, Desktop overlay and
click-through, blur/local contrast, global shortcuts, startup registration,
cross-process single-instance restoration, automatic updates, additional quota
providers and complete localization are not connected in this shared host.
It does not infer hardware support from a successful UI launch. The existing
stable Windows App remains the feature-complete choice for its supported hardware.

CI proves native architecture, live system counters, UI lifecycle and extracted
package startup. It does not prove every physical desktop's tray interaction,
game behavior, account login, long-running memory behavior or OS security prompts.
Performance observations and limitations are in [PERFORMANCE.md](PERFORMANCE.md).
