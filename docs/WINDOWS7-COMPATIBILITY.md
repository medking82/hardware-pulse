# Windows 7 compatibility work

Status: assessment in progress; no Windows 7 compatible release has been produced
or verified. The user prioritized this work over further macOS/Linux expansion
on 2026-09-16. Initial target is Windows 7 SP1 x64; physical/VM verification
availability has been requested and is not yet known.

## Current evidence

- Latest stable Windows x64 release is v0.6.26. The installer explicitly sets
  `MinVersion=10.0.19045`, so it requires Windows 10 22H2 or later.
- The existing WPF host builds with .NET Framework 4.8. Microsoft lists Windows 7
  SP1 as an installation target for that runtime. This does not establish Pulse
  compatibility: native calls, sensors, installation and updates also need checks.
- The shared Avalonia host targets .NET 10. It is not the Windows 7 delivery path.
- The current installer unconditionally requires PawnIO 2.2.0. Its upstream
  maintainer reports that PawnIO does not support Windows versions below 10.
  Lowering the installer minimum alone would therefore create a broken install.
- `LocalContrast.Enable` already rejects builds below 19041 because they cannot
  exclude the overlay from its own capture. Keep this guard; do not replace it
  with sampling that captures itself.
- The current collector enables CPU, GPU, motherboard, storage, memory and
  network in LibreHardwareMonitor together. Driver-dependent channels and
  driver-free counters must be separated before claiming usable Win7 telemetry.

## Delivery gates

1. Audit WPF startup, native window/material APIs, task registration, tray,
   network, quota transport and PresentMon against Windows 7 SP1.
2. Establish an explicit capability/fallback policy. Do not invent missing
   temperatures/fan speeds or silently install an incompatible driver. Any
   alternative sensor backend needs its own compatibility and security evidence.
3. Produce an isolated compatibility package with correct .NET prerequisite
   messaging and update-channel behavior; never send incompatible newer runtime
   packages to an installed Win7 client.
4. Verify installation, launch, resize/drag/lock, restart, telemetry availability,
   upgrade/uninstall and missing dependencies on an actual Win7 SP1 x64 system.
5. Publish only after these gates pass, with an explicit feature list. Current
   Windows 10/11 stable delivery remains independently releasable.

## Sources

- [Microsoft .NET Framework 4.8 release and Windows 7 SP1 requirements](https://devblogs.microsoft.com/dotnet/announcing-the-net-framework-4-8/)
- [Microsoft current .NET Windows installation requirements](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [LibreHardwareMonitor maintainer discussion of PawnIO OS support](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/discussions/1904)
