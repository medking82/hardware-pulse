# Windows 7 compatibility work

Status: an unsigned development installer can now be built; no Windows 7 release
has been published or verified on Windows 7. The user prioritized this work over further macOS/Linux expansion
on 2026-09-16. Initial target is Windows 7 SP1 x64; physical/VM verification
availability has been requested and is not yet known.

Accepted scope (2026-09-16): the user explicitly accepts **no FPS support on
Windows 7**, provided this is clearly documented. FPS implementation/parity is
not a Win7 release gate. Keep its UI/tray/Desktop controls unavailable and omit
PresentMon. Modern Windows FPS support and the wider multi-platform objective
are unchanged; this does not waive target-OS installation and telemetry checks.

## Current evidence

- Latest stable Windows x64 release is v0.6.26. The installer explicitly sets
  `MinVersion=10.0.19045`, so it requires Windows 10 22H2 or later.
- The existing WPF host builds with .NET Framework 4.8. Microsoft lists Windows 7
  SP1 as an installation target for that runtime. This does not establish Pulse
  compatibility: native calls, sensors, installation and updates also need checks.
- The shared Avalonia host targets .NET 10. It is not the Windows 7 delivery path.
- The modern Windows installer requires PawnIO 2.2.0. Its upstream
  maintainer reports that PawnIO does not support Windows versions below 10.
  Lowering the installer minimum alone would therefore create a broken install.
- `LocalContrast.Enable` already rejects builds below 19041 because they cannot
  exclude the overlay from its own capture. Keep this guard; do not replace it
  with sampling that captures itself.
- The current collector enables CPU, GPU, motherboard, storage, memory and
  network in LibreHardwareMonitor together. Driver-dependent channels and
  driver-free counters must be separated before claiming usable Win7 telemetry.

## Delivery gates

### Runtime audit and GPU investigation

- Startup uses Task Scheduler XML schema 1.2 and existing COM registration;
  no Win8-only task settings were found. Actual Win7 registration still needs
  verification with the installer and its interactive/elevated task split.
- Window snapping now guards the newer DPI export. DWM attributes can return
  unsupported HRESULTs; material policy already falls back to an opaque surface.
  Aero-disabled and remote-session behavior remain target-OS tests.
- Network requests explicitly offer TLS 1.2/1.3. Win7 Schannel does not supply
  TLS 1.3; handshake behavior and current endpoint cipher/certificate compatibility
  remain unverified. Do not claim connectivity from a successful Windows 11 test
  or weaken endpoint certificate validation to make an older system connect.
- In pinned LHM 0.9.6, `Computer.AddGroups` creates AMD/NVIDIA GPU groups when
  only `IsGpuEnabled` is set. Intel GPU enumeration additionally requires CPU
  enumeration; its integrated GPU implementation references PawnIO. Therefore a
  GPU-only path must not be presented as universal Intel/AMD/NVIDIA support.
- `scripts/Probe-LegacyGpu.ps1` runs an isolated, development-only live probe with
  transient default settings. It confirms only AMD/NVIDIA hardware groups,
  undefined hardware control modes and absence of a loaded PawnIO module. The
  current Windows 11 probe returned real GPU temperature/fan telemetry. No Win7
  or old vendor-driver compatibility is established by that observation.
- Before production integration, resolve partial-open cleanup: upstream
  `Computer.Open` sets `_open` after `AddGroups`, while `Close` returns immediately
  when `_open` is false. An exception during group creation can leave earlier
  groups unclosed. The probe owns a short-lived process so that this investigation
  does not put that incomplete lifetime into the long-running collector. A future
  adapter must preserve basic CPU/RAM/network readings on optional GPU failure,
  avoid repeated initialization attempts, and verify both failure and close paths.

Evidence: pinned [Computer.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Computer.cs),
[Control.cs](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/Control.cs),
and [Microsoft .NET Framework TLS guidance](https://learn.microsoft.com/en-us/dotnet/framework/network-programming/tls).

### Development installer

`scripts/Build.ps1 -Installer -Win7Compatibility` compiles the existing WPF
runtime into `dist/HardwarePulse-Win7-x64-Setup.exe`. It does not publish,
install or launch it. The package permits Windows 7 SP1 x64 (NT 6.1 SP1), rejects
NT 6.2 and later, requires .NET Framework 4.8, omits the PawnIO prerequisite
and excludes the PresentMon executable. LHM managed dependencies remain for
runtime assembly resolution; the legacy collector does not open its hardware
backend. CPU/RAM/network availability is verified on the development OS only.

The compiler preprocessor separates these options from the modern installer.
Both variants preserve the AppId, installation path, cooperative upgrade stop,
task-registration identity and uninstall behavior. Every installer build now
compiles and checks both expanded variants with `Test-InstallerVariants.ps1`.
The modern output alias is not overwritten by a compatibility build.
The unsigned local installer is development evidence, not a supported download.

The compatibility installer explicitly shows its welcome page, with English,
Simplified Chinese or Traditional Chinese text selected by the installer language.
It states that FPS and Local Contrast are unsupported and that the current build
provides CPU usage, memory and network data, with temperature/fan/GPU readings
unavailable. Modern installer pages are unchanged. Compiled variant checks cover
the notice and its isolation; target-OS visual verification remains outstanding.

Scope: installer compile-time branches, build selection and compiled-variant
checks. No task permissions, driver registration, installed application or OS
settings are changed on this workstation. Validation includes both expanded
compiler branches, a real compatibility EXE build, the modern alias hash guard
and full repository validation. Rollback: revert these source changes; existing
published packages are unchanged. Actual install/upgrade/uninstall tests on NT
6.1 SP1 remain required before release.

Inno's documented [MinVersion](https://jrsoftware.org/ishelp/topic_setup_minversion.htm)
and [OnlyBelowVersion](https://jrsoftware.org/ishelp/topic_setup_onlybelowversion.htm)
directives define the OS interval; the pinned 6.7.3 compiler accepts both variants.

The WPF UI uses the Windows adapter's OS capability policy for FPS capture and
capture exclusion. On legacy Windows the FPS quick switch, tray entry, overlay
FPS selection and Desktop FPS selection are disabled with an explanation;
Local Contrast is also disabled below Windows 10 build 19041. Existing saved
preferences are retained rather than migrated to false. The collector and runtime
guards remain in place; disabled controls are not the sole enforcement boundary.
The isolated native UI test injects a Win7 version to exercise these states,
without changing the workstation OS or installed user settings. This remains
simulated capability coverage, not an actual Win7 runtime test.

This increment is limited to the Windows capability policy, WPF consumers,
translations and regression tests. Preserve modern Windows behavior, ordinary
Desktop monitoring, stored preferences and installer/update contracts. Validation:
Framework adapter tests, native WPF tests and `Validate.ps1 -ModernCore`.
Rollback is a source revert; no installed state or release assets are changed.

Window snapping now retains per-monitor `GetDpiForWindow` on modern Windows
and falls back to the window's WPF device transform when the export is absent
or returns zero. A missing export is remembered per attached window, avoiding
exceptions on every drag event. Deterministic tests cover missing exports,
96/144 DPI, WPF scaling and the existing snap/release geometry. These checks
do not replace drag/resize testing on Windows 7 itself.

Package names must distinguish OS compatibility from architecture: `Win7-x64`
means Windows 7 SP1 64-bit, while `x86` would mean a separate 32-bit build.
No x86 build is implemented. The current stable updater's
`HardwarePulse-Setup.exe` contract is preserved for modern Windows. The WPF
host selects the fixed `HardwarePulse-Win7-x64-Setup.exe` identity on legacy
Windows in both the metadata coordinator and download verifier. Both use the
same stable release endpoint and version ordering. A release without the legacy
asset displays an explicit unavailable-update status and never falls back to
the modern installer. A future stable release must include each supported
channel's correctly versioned package before publishing it as latest.

Channel tests exercise mixed assets, modern/legacy selection, duplicate and
missing assets, cross-channel URL rejection, SHA-256 requirements and download
rejection before any request starts. Repository, HTTPS, redirect-host, size,
digest and pre-install revalidation rules remain unchanged. Scope is the
updater, WPF construction, tests and text; no installer is launched or published.
Reverting this increment restores the former modern-only channel contract.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->

The first implementation shares the driver-free `WindowsSystemReadings` source
between the Framework and modern hosts. The existing collector selects this
path on Windows versions before 10, leaves the hardware library unopened, does
not create the FPS server, and publishes CPU load, physical RAM and per-interface
throughput/link readings through the existing snapshot/presentation schema.
Temperature/fan/GPU readings are unavailable in this path; this is preparation
for compatibility. Their compatibility work remains open. FPS is explicitly
unsupported on Win7 by the user's accepted scope above.
Memory metadata accepts older WMI schemas and falls back to Speed/MemoryType.

Validation includes a two-sample driver-free collector run in an isolated test
directory, valid CPU/RAM/throughput mappings, absence of fabricated hardware
readings and no loaded PawnIO module. Framework and modern counter tests share
the implementation. These tests run on the development Windows 11 machine and
do not prove Windows 7 installation or UI compatibility. The modern installer
minimum is unchanged; the separate compatibility package remains unverified on
its target OS.

The bounded change owns the Framework/modern Windows counter source, native
collector selection and tests. It preserves current Windows 10/11 hardware
selection, privilege isolation, snapshot schema and the existing polling cadence;
no driver or OS settings are changed. Validate with `Test-WindowsAdapters.ps1`,
the shared Desktop tests and `Validate.ps1 -ModernCore`. Revert this source change
to roll back; installed settings and release assets are untouched.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->

1. Audit WPF startup, native window/material APIs, task registration, tray,
   network and quota transport against Windows 7 SP1. Verify FPS stays disabled
   and PresentMon is absent from the compatibility package.
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
