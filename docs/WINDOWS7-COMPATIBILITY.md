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

Window snapping now retains per-monitor `GetDpiForWindow` on modern Windows
and falls back to the window's WPF device transform when the export is absent
or returns zero. A missing export is remembered per attached window, avoiding
exceptions on every drag event. Deterministic tests cover missing exports,
96/144 DPI, WPF scaling and the existing snap/release geometry. These checks
do not replace drag/resize testing on Windows 7 itself.

Package names must distinguish OS compatibility from architecture: `Win7-x64`
means Windows 7 SP1 64-bit, while `x86` would mean a separate 32-bit build.
No x86 build is implemented. Preserve the current stable updater's
`HardwarePulse-Setup.exe` contract until an explicit compatibility channel is
implemented and tested.

The first implementation shares the driver-free `WindowsSystemReadings` source
between the Framework and modern hosts. The existing collector selects this
path on Windows versions before 10, leaves the hardware library unopened, does
not create the FPS server, and publishes CPU load, physical RAM and per-interface
throughput/link readings through the existing snapshot/presentation schema.
Temperature/fan/GPU readings are unavailable in this path; this is preparation
for compatibility, not an accepted reduction of the complete product target.
Memory metadata accepts older WMI schemas and falls back to Speed/MemoryType.

Validation includes a two-sample driver-free collector run in an isolated test
directory, valid CPU/RAM/throughput mappings, absence of fabricated hardware
readings and no loaded PawnIO module. Framework and modern counter tests share
the implementation. These tests run on the development Windows 11 machine and
do not prove Windows 7 installation or UI compatibility. The installer minimum
is unchanged until a compatibility package and its dependencies are verified.

The bounded change owns the Framework/modern Windows counter source, native
collector selection and tests. It preserves current Windows 10/11 hardware
selection, privilege isolation, snapshot schema and the existing polling cadence;
no driver or OS settings are changed. Validate with `Test-WindowsAdapters.ps1`,
the shared Desktop tests and `Validate.ps1 -ModernCore`. Revert this source change
to roll back; installed settings and release assets are untouched.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->

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
- [Microsoft processor-group API: available from Windows 7](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getactiveprocessorgroupcount)
