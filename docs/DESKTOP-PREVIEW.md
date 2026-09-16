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

Development after `0.7.0-preview.1` connects Linux kernel-exposed temperature/fan
channels to a dedicated monitor panel. Polling runs on the existing worker;
discovery refreshes every 30 seconds. Missing readings show an em dash and a
device exposing no channels has an explicit empty state. Labels are supplied by
the kernel and are not guessed CPU/GPU assignments. This is not present in the
published preview. Other platforms still report this capability as unavailable.

Temperature/fan sensors, GPU telemetry, FPS capture, Desktop overlay and
click-through, blur/local contrast, global shortcuts, startup registration,
cross-process single-instance restoration, automatic updates, additional quota
providers and complete localization are not connected in this shared host.
Development also adds Live / Session Max selection using Core ReadingSession
history. CPU, selected-interface byte rates and exposed Linux sensor values
support peaks; RAM usage and quota remain current, as labelled in the UI.
Changing mode does not poll again. Network selection starts a fresh network
session; detected hwmon topology/label changes reset sensor peaks because hwmon
ids are not permanent identities. Closing the App ends the session. These changes
are not in the published `0.7.0-preview.1` archive.
Development Network settings also provide **Refresh interfaces** for newly
connected devices. A missing saved interface stays unselected; reconnecting it
and refreshing restores the selection without switching silently to another.
Development Settings → Appearance → Language now offers Auto (System), English
and Simplified/Traditional Chinese. Monitor, Settings, sensor status, Codex quota and tray
labels update in place, without restarting sampling or refreshing credentials.
The language choice is saved in the separate preview profile. Unknown system
languages fall back to English; device names and readings are never translated.
Auto distinguishes Chinese scripts and regions (Traditional for TW/HK/MO or
explicit Hant; Simplified for CN/SG or explicit Hans). Other UI languages remain
future work.
It does not infer hardware support from a successful UI launch. The existing
stable Windows App remains the feature-complete choice for its supported hardware.

CI proves native architecture, live system counters, UI lifecycle and extracted
package startup. It does not prove every physical desktop's tray interaction,
game behavior, account login, long-running memory behavior or OS security prompts.
Performance observations and limitations are in [PERFORMANCE.md](PERFORMANCE.md).
