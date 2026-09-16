# Shared Desktop preview

The 0.7.0 preview is the new shared host for Windows, Linux and macOS, on x64
and ARM64. The current [preview.3 release](https://github.com/medking82/hardware-pulse/releases/tag/v0.7.0-preview.3) excludes Windows ARM64 while its native input verification remains incomplete. It is an experimental, separate App rather than a replacement for the
stable Windows WPF installer. Platform feature parity is not complete.

## Available

Source after preview.3 adds close-to-tray on Windows/macOS when a native tray menu
exporter is available. User window-close hides Monitor; sampling and the floating
monitor continue. Open Pulse restores the same window, and Quit Pulse exits even
while hidden. Programmatic close and OS/application shutdown are never intercepted.
Linux retains close-to-exit until tray reachability can be established. This source
change is not in the preview.3 download.

Source after preview.3 also adds independent Claude and Antigravity quota
opt-ins. Windows Antigravity uses the current user's running local server;
macOS/Linux Antigravity source discovery is unavailable. Monitor and floating
Desktop share each provider snapshot. These additions are not in the published
preview.3 packages. Windows 0.7.0 stable now targets the shared host only after
the [Windows release gates](WINDOWS-070-READINESS.md) pass.

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

Source builds must first run `python scripts/prepare_desktop_fonts.py` from the
repository root. This fetches immutable, SHA-256-pinned Noto Sans CJK SC/TC assets
into ignored `vendor/desktop-fonts`. The package builder performs this step
automatically. Fonts are embedded in the App and accompanied by the OFL license;
users do not need to install Chinese fonts or download them at runtime.

Choose the archive matching your OS and CPU architecture. Extract the entire
archive into its own folder; do not copy just the executable or mix versions.
The .NET runtime is included. Each archive has a SHA-256 sidecar and an internal
manifest listing the source commit, preview version and file hashes.
Development packages also include separate English (`README.txt`) and Simplified
Chinese (`README.zh-CN.txt`) launch guides. Copies inside macOS Contents/Resources
remain available when the App is moved out of the extracted archive folder.

| Package | Native CI environment | Launch |
| --- | --- | --- |
| win-x64 | Windows Server 2022 x64 | Pulse.Desktop.exe |
| win-arm64 | Windows 11 ARM64 input check incomplete | Not included in preview.3 |
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

## Added in preview.3

The current source adds macOS CPU model and physical/logical core counts, plus
per-device GPU utilization and driver-reported GPU core counts. Static identity
is cached. GPU counters and core counts are unavailable on the CI VMs; physical
GPU coverage still needs device testing. Read-only AppleSMC temperature/fan
channels are also connected, with Intel CI readings and explicit unavailable
state on the Apple Silicon VM. These changes are included in the preview.3 download.
See [macOS sensor boundaries](MAC-SENSORS.md).

## Added in preview.2

Linux kernel-exposed temperature/fan channels appear in a dedicated panel. Polling
uses the existing worker; discovery refreshes every 30 seconds. Missing readings
show an em dash. Device labels come from the kernel, not guessed CPU/GPU assignments.
Other platforms still report this sensor capability as unavailable.

Live / Session Max uses Core ReadingSession history for CPU, selected-interface
rates and Linux sensors; RAM and quota remain current. Network settings can refresh
interfaces without restarting. A missing saved interface stays unselected.

The floating monitor reuses existing readings and enabled Codex quota. It remembers
position, size and Always on top. Settings > Desktop groups font size, background
opacity and optional system blur, with achieved-backend status. macOS native blur
and lock restoration passed Intel/Apple Silicon CI. Blur strength is OS-controlled.
Windows x64, macOS and Linux X11 support native input pass-through locking; reopen
from Monitor or tray/menu bar to unlock. Locking hides editing controls.

Settings > Appearance > Language offers Auto (System), English and Simplified or
Traditional Chinese, including embedded CJK fonts. Changes apply immediately to UI
and tray labels, without restarting sampling or refreshing credentials. Preferences
use the separate preview profile. Device names and readings are preserved.

## Not included yet

GPU telemetry, FPS capture, complete desktop-layer integration, adjustable blur
radius/local contrast, global shortcuts, startup registration, cross-process
single-instance restoration, automatic updates and additional quota providers
remain incomplete. Native Wayland interaction is not verified. Windows ARM64 input
verification remains blocked by an unrelated runner window; no package for that
platform is included in preview.2. Other UI languages remain future work.
The stable Windows App remains the feature-complete choice for supported hardware.

CI proves native architecture, live system counters, UI lifecycle and extracted
package startup. It does not prove every physical desktop's tray interaction,
game behavior, account login, long-running memory behavior or OS security prompts.
Performance observations and limitations are in [PERFORMANCE.md](PERFORMANCE.md).
