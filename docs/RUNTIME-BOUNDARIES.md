# Runtime ownership

Current validation (2026-09-15): shared Core passed on Windows, Linux and macOS,
each with native x64 and ARM64 test processes. See the [six-platform CI run](https://github.com/medking82/hardware-pulse/actions/runs/34972802044)
for commit `ad5d303cd7c805adb41a8e2e5fdf402e6085d9b1`. Historical extraction notes
below describe the evidence available at their baselines. This updates Core
execution coverage only; Windows adapters and the complete App remain separate
platform work, and the published installer is still Windows x64 v0.6.24.

The native runtime has these existing boundaries:

| Owner | Responsibility | Verification |
| --- | --- | --- |
| Collector | Read-only hardware sampling and snapshot publication | Sensor fixtures and installed live snapshots |
| Pulse.Adapters.Windows / SensorProfile | Map a snapshot to readings and reject stale/invalid input | Native/legacy differential tests |
| Pulse.Core / ReadingSession | Consume a supplied reading source and retain per-session peaks and known capabilities | Pure CoreTests plus Windows snapshot ReadingSessionTests |
| Pulse.Core / QuotaSession | Per-provider refresh schedule, pending work, cancellation and late-result rejection | Core-only quota lifecycle tests and native quota integration |
| Shell and its UI partials | WPF timer, controls, rendering, visibility and user interaction | NativeTests WPF integration |
| Startup / SchedulerStore | Validate ownership and operate the app's scheduled tasks | Startup tests and isolated scheduler integration |
| UpdateCheck | Validate/download installer assets and start installation | Updater verification tests |
| UpdateCoordinator | Version selection, check schedule, operation state, retries and disposed-result handling | Headless UpdateCoordinatorTests |
| Pulse.Core / MaterialPolicy | Derive effective opacity and backdrop flags from saved preferences and current display state | Shared Core tests, headless material tests and WPF lock/settings roundtrip |
| Pulse.Core / ContrastAnalysis | Bounded luminance analysis, temporal hysteresis and local region color/edge confidence | Headless CoreTests; no WPF, Win32 or capture dependencies |
| LocalContrast | Windows capture exclusion, reusable GDI buffers and WPF mask creation | Actual capture, resize, dispose/resume and Screenshot mode integration |

ReadingSession is synchronous and is called on the UI thread. It owns no timer,
window, scheduler or driver. Its state is per Shell instance, and the UI treats
Latest as read-only. Cards and overlay consume the same session. Closing to tray
continues polling; shutdown/STOP handling and the two-second timer remain owned
by Shell. These lifecycle semantics were preserved during extraction.

`scripts/Test-ReadingSession.ps1` references the actual Pulse.Core and
Pulse.Adapters.Windows DLLs for its headless integration fixture, including the
Windows snapshot DTOs, JSON helper and SensorProfile. CoreTests separately exercise the same session with synthetic
readings and no files, WPF, WinForms, System.Web or app EXE. Both are part of Validate.ps1.

UpdateCoordinator receives an IUpdateClient; the production adapter delegates to
the unchanged UpdateCheck. Metadata checks still require a stable release and one
valid named installer asset. Hash verification and UAC launch stay in UpdateCheck.
The coordinator is called serially from the UI context and resumes on that context;
it owns no controls or timers. Its task methods contain expected operation failures
as status keys. Disposal cancels downloads and ignores late completions.

Shell.Services now renders update status, wires buttons/preferences and owns the
short-lived progress timer. The installed version comes from assembly metadata.
Headless coordinator tests use a fake client, so retries, duplicate commands and
late completion need neither network nor elevation. Existing UpdateCheck tests
remain responsible for URL/digest/download verification.

Remaining coupling: Shell.Services still includes both updater presentation and
overlay presentation; these remain WPF responsibilities. Settings, tray and
material code also share Shell state. No broader UI architecture rewrite has been
performed or implied by these two extractions.

MaterialPolicy owns no persistence or Windows calls. WPF applies its result to
the backdrop and background brushes; the slider retains the user's saved opacity.
Solid/high contrast and unsupported-backdrop fallback override temporary lock
opacity. Settings file layout and unknown-field preservation remain in Settings.

## First portable component

`src/Core/ContrastAnalysis.cs` receives caller-owned BGRA pixels. It owns only
analysis buffers and the previous-frame state. It neither captures the screen nor
owns a timer, settings, credentials, files or threads. One instance is serialized
by its caller. Region queries use the latest analyzed frame and allocate no pixel
arrays. DesktopView maps its text bounds into the bounded analysis grid.

`scripts/Build-Core.ps1` builds an AnyCPU `Pulse.Core.dll` with the existing Windows
Framework compiler. Build-Native references that DLL and the release payload
includes it. CoreTests run without loading the app or UI assemblies. This is an
OS-independent source boundary, not a claim that the current .NET Framework/WPF
application or its installer runs on ARM64, Linux or macOS.

Reading and Usage contracts plus session state now also live in Core, with
snapshot JSON/LHM mapping retained in the Windows adapter. Collector, FPS capture,
tray, startup, window layering and update asset
selection remain platform responsibilities. Preserve the existing process and
privilege boundaries while adding platform implementations; unsupported sensors
must be reported as unavailable rather than fabricated as zero.

The modern .NET target is validated alongside the existing Windows host as described
below. ARM64 requires separate validation of the collector/driver and FPS
payloads; shared UI support alone does not establish telemetry parity. Linux and
macOS require native collectors and desktop integration. These ports are not yet
implemented. See [Local Contrast measurements](PERFORMANCE.md) for this phase.

## Reading boundary contract

This extraction follows the sweep of baseline `033508f` (v0.6.21). The source
leak was ReadingSession's constructor accepting a snapshot path and Poll calling
SensorProfile directly. Reading/Usage also shared a source file with Windows wire
DTOs and System.Web JSON IO. Moving folders alone would not remove that dependency.

The stable entry point is now `ReadingSession(Func<DateTimeOffset, Reading>)`.
Shell supplies `now => SensorProfile.Read(paths.Snapshot, now)` and continues to
call Poll every two seconds. A future adapter may supply semantic readings without
reproducing LibreHardwareMonitor IDs or writing the Windows snapshot schema.
No wrapper interface, new process, timer, background task or per-poll copy is added.

The source must return a non-null normalized Reading, converting expected source
failures into an OFFLINE or STALE reading with no live values. The Windows adapter
continues to own schema 1/2 compatibility, parsing, value validation, freshness
checks and its snapshot identity. Core treats identity as an opaque string: the
same identity may update Latest but must not advance peaks. The host passes time
and serializes calls; the session does not sample independently.

On unavailable readings, the session retains the preceding availability, display
names, GPU fan count, usage capabilities and peaks. A new LIVE reading replaces
capability metadata. A collector restart keeps the current UI session's peaks;
another session starts with independent history. The existing mutable fields are
preserved for compatibility; consumers treat Latest and its dictionaries as
read-only after publication. This change does not make the session thread-safe.

Demonstrated consumers are Cards, Desktop, the separate overlay, diagnostic fan
mapping and the session/native/sensor fixtures. Diagnostics and SensorProfile use
Core reading types; raw snapshot serialization lives in Pulse.Adapters.Windows.
The differential fixture loads that real adapter DLL alongside Core.
The installer includes that real Core DLL through the existing app payload.

The four intended boundaries remain Core, platform adapters, presentation and
platform host. Reading contracts/session, quota contracts and network formatting
are extracted into Core. Other formatting and platform-specific tray/startup/capture
remain follow-up work. Existing elevated Collector isolation, FPS protocol, settings,
installation/update trust checks and all refresh intervals are unchanged.

Validation: Core-only source tests; Windows snapshot offline/live/stale, malformed
input, duplicate identity, recovery and independent-session tests; native/legacy
sensor parity; complete Validate.ps1 including WPF/Desktop, FPS, quota, startup,
diagnostic export and installer payload checks. Core remains AnyCPU built with the
Framework compiler; ARM64/Linux/macOS runtime validation is still outstanding.
The subsequent modern Core target does not port the host. No new cross-platform
app support or memory reduction is claimed.

## Quota contracts and network formatting

The next extraction uses baseline `547e2c8`. `QuotaReading` and `QuotaWindow`
previously shared QuotaData.cs with System.Web response parsing. They now live in
Core; QuotaData and provider credentials/requests now live in the Windows adapter. QuotaSession
was extracted in the subsequent phase below. Consumers are QuotaSession, QuotaView, Desktop, provider adapters and
the quota/hero fixtures. Unknown Remaining is null, distinct from zero remaining;
Windows and AllWindows retain their separate compact/full collections. Status,
timestamps, field names and mutable DTO compatibility are unchanged. This boundary
contains semantic results only, never credentials or provider JSON.

`NetworkRate.Link` and `NetworkRate.Format` now live in Core instead of sharing a
file with Shell.WireNetwork. Cards and Desktop reuse the same decimal unit,
precision, invalid-value and disconnected-state rules. CurrentCulture remains
host-owned; localization of Disconnected remains in presentation. WPF unit selection
and settings persistence remain in Native. Hardware/FPS formatting is not changed.

CoreTests cover unknown versus zero quota, separate window collections, network
unit thresholds and overrides, invalid values and a non-dot decimal culture.
Settings quota and hero CodeDom fixtures explicitly reference Pulse.Core.dll.
The existing full Validate suite remains the integration check for parser, lifecycle,
WPF and package compatibility. No process, timer, polling interval or privilege
boundary changes; no measurable performance improvement is implied by extraction.
Both this extraction and the preceding reading extraction can be reverted as
source-only commits without migrating user settings or stored data.

## Quota refresh lifecycle

From baseline `4b2feb2`, QuotaSession now resides in the real Core assembly beside
its contracts. Its existing `Func<string, CancellationToken, QuotaReading>` boundary
already isolates provider work. QuotaView supplies QuotaProviders.Read; Windows
credentials, HTTP policy, response decoding and process ownership checks stay in
Pulse.Adapters.Windows. No new interface or forwarding layer is needed.

The host serializes Enable, Tick, Refresh and Dispose calls and supplies Tick's
clock. Each enabled provider has at most one pending worker task. The next scheduled
attempt remains five minutes after its start; manual refresh makes idle providers
due on the next Tick. A request receives cancellation after 30 seconds. Cancellation
is cooperative: an adapter that ignores it remains pending, preventing overlapping
requests. Disable/re-enable changes the slot version and rejects the old result.
Dispose cancels pending work, observes its eventual exception and prevents publication.
Providers must return a non-null semantic reading or throw; generic worker faults
become Quota unavailable without exposing exception text.

CoreQuotaSessionTests run with synthetic readers and host times, no app EXE, network,
credentials or WPF. They exercise scheduling, refresh, no-overlap, disable/re-enable,
late results, fault handling and disposal; existing native tests retain parser and UI
integration coverage. The 30-second cancellation interval is preserved in source,
not shortened for tests. This extraction retains existing tasks and cancellation
timers, adding none. It is source-reversible and does not migrate settings or claim
CPU/RAM reduction or completed ARM64/Linux/macOS support.

## Modern Core build

From baseline `66a4cfa`, `src/Core/Pulse.Core.csproj` builds the same source as a
`net10.0` library with no package dependencies. The existing Framework compiler
path still builds the DLL consumed by the WPF app and installer. The two outputs
are separate; the modern DLL must not replace the Framework DLL in the installed app.

`scripts/CoreTests/Pulse.Core.Tests.csproj` references the modern library and links
the existing CoreTests and CoreQuotaSessionTests sources. Only the allocation
counter differs by runtime: Framework uses AppDomain monitoring, modern .NET uses
the current-thread GC allocation counter. Both run the same 10,000-query allocation
assertion and readings, formatting, contrast and quota lifecycle assertions. Each
target enforces its own BCL-only assembly reference allowlist.

Run `scripts/Test-CoreModern.ps1` using PowerShell 7 on Windows, optionally supplying
`-DotNetPath` to an SDK host. It defaults to an ignored repo-local SDK when present,
then PATH, and fails on a missing/incompatible SDK. `scripts/Validate.ps1 -ModernCore`
adds this check to the complete Windows suite. Normal Validate remains available
to the existing Framework-only toolchain. Compiler servers are disabled for the
modern check; no SDK is included in the app payload.

On other hosts with .NET 10 SDK, the portable check can be run directly:

```sh
dotnet run --project scripts/CoreTests/Pulse.Core.Tests.csproj --configuration Release
```

Local evidence: Windows x64, Microsoft SDK 10.0.401, downloaded from official
[.NET 10 release](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) metadata
and verified against its SHA-512. Modern build produced zero warnings/errors and
all shared tests passed. SDK/archive and build outputs are ignored. This is a
Windows execution of portable Core tests; ARM64, Linux and macOS execution, native
collectors, UI, desktop layering and FPS capture remain unverified. No runtime
performance or memory comparison between the two targets is claimed.

## Windows adapter assembly

From baseline `f2c1dfc`, `src/Adapters/Windows` builds as
`Pulse.Adapters.Windows.dll`. It contains the unchanged snapshot wire DTOs/JSON IO,
SensorProfile mapping, QuotaData response decoding, QuotaProviders and Antigravity
discovery. The assembly references Core and Framework BCL/System.Web/System.Management,
but not WPF, WinForms, the app EXE or LibreHardwareMonitor. The existing Collector
still writes its snapshot; the adapter converts it into Core Reading/Usage values.
QuotaSession still receives QuotaProviders.Read through its existing delegate.

`Build-WindowsAdapters.ps1` builds the Framework Core dependency and adapter DLL.
Build-Native references both; the installer includes both through the app payload.
ReadingSession and sensor parity fixtures consume this same adapter DLL instead
of recompiling private copies of its implementation. Native, Settings quota and
Hero fixture compilation also references the adapter explicitly. Package checks
verify ownership of the moved types and an allowlist of assembly dependencies.

No data format, freshness window, credential lookup, process-owner verification,
HTTP allowlist/redirect policy, cancellation, polling, driver or privilege behavior
changes. No extra process, worker or timer is introduced. Existing parser, redirect,
process ownership, WPF/Desktop, startup, FPS and package checks remain required.
The framework adapter is Windows-specific and is not a net10.0 adapter; future OS
adapters must produce Core contracts without depending on this DLL or its snapshot
schema. The Windows host retains Collector, FPS capture, tray/startup, backdrop,
Local Contrast capture and installation/update operations. Those boundaries still
need platform implementations before a cross-platform app can be claimed.

Rollback is a source/build rollback of this commit, with no user data migration.
The additional DLL is part of an atomic app payload; do not deploy a new app EXE
alone. This is the shared-Core/Windows-data-adapter foundation, not complete
Windows host decomposition or an ARM64/Linux/macOS release.

## Windows network sampling

From baseline `aeb5900`, WindowsNetwork and WifiSignal reside in the Windows
adapter assembly. Collector retains one WindowsNetwork instance and passes the
existing LibreHardwareMonitor Identifier mapping as a delegate. This preserves
snapshot/network-sensor joins while keeping the adapter independent of LHM.
The adapter owns interface enumeration, link classification/speed, WLAN signal
queries and the existing 30-second physical-adapter discovery cache. Calls remain
serialized by Collector's existing sampling loop; no timer or process is added.

Unavailable speed/signal stays null, and no second connection is assumed. The WLAN
query reads the connected interface only; it does not scan or change connections.
WindowsAdapterTests compile against the real adapter DLL without the app or WPF,
then perform read-only live checks of host identifier mapping, optional links,
speed/signal semantics and invalid Wi-Fi input. These are smoke checks on this
Windows machine, not simulated disconnect/reconnect or hardware coverage for every
NIC. Existing differential fixtures still verify the downstream sensor mapping.
Test-WindowsAdapters is included in Validate. Source rollback requires no settings
migration; no CPU/RAM improvement is claimed for this extraction.

## Windows memory and inventory queries

From release baseline `2988517` (v0.6.22), WindowsHardware owns GlobalMemoryStatusEx,
Win32_PhysicalMemory and disk-to-volume WMI queries. ReadMemory returns the same
used/usable physical capacity in binary gigabytes or null when unavailable.
ReadMemoryModules and ReadDisks preserve existing metadata, labels and fallback
values. The adapter contains no LHM, WPF, file publication or scheduling logic.

Collector calls inventory once after opening hardware, then calls ReadMemory once
per existing sample. Its warning handling, snapshot schema, STOP/watchdog behavior,
driver ownership and two-second cadence are unchanged. No WMI query is moved into
the sampling loop. WindowsAdapterTests exercise these methods through the real DLL
and check live memory bounds and optional inventory metadata without printing
device identifiers. Differential mapping and full Validate remain the downstream
regression checks. Missing-WMI failure injection and other Windows devices are not
covered by this local smoke test. This source extraction adds no worker/timer and
does not establish a measurable performance improvement or additional OS support.

## Portable material policy

From baseline `d78e9c1`, MaterialPolicy lives in Core unchanged. It accepts saved
opacity, lock/settings state, solid/high-contrast preferences and backdrop support;
it returns effective opacity and presentation flags. Windows Controls still owns
capability detection, brushes, backdrop calls and settings persistence. The policy
does not implement blur or desktop rendering on another OS.

CoreTests exercise the same lock/settings/unlock, zero-opacity and readable fallback
rules on Framework and .NET 10. Test-MaterialPolicy now references the real Core DLL
while retaining the existing settings/unknown-field roundtrip assertions. This is
a source-only relocation with no new allocations beyond existing policy objects,
no settings migration and no changed visual behavior. Rollback is one source commit.

## CORE-01: shared numeric formatting

From baseline `1169afc`, ReadingFormat owns temperature/utilization fixed one-decimal
precision, whole RPM/FPS, normal three-decimal voltage, explicit compact voltage,
and RAM/VRAM usage text. Cards, Desktop and the separate overlay reuse it. Views
retain units/spacing, labels, live/peak selection and unavailable-state checks;
network units stay in NetworkRate, and quota precision is unchanged.

The only intentional output change is fractional RPM in the separate overlay:
it now renders as an integer, as required by CORE-01. Cards voltage remains three
decimals and overlay voltage remains compact. Desktop FPS layout and digit widths
are unchanged. Inputs remain validated by their existing adapters/consumers;
ReadingFormat is not a new validation or capability boundary.

Shared Core tests cover whole/fractional readings, rounding, voltage variants and
host culture on Framework/.NET 10. Native tests exercise overlay formatting and
missing values; existing Cards/Desktop, stale/peak and usage checks remain.
Allowed changes are this formatter, its consumers and related tests/docs; no
settings, credentials, capture or sampling changes. One source commit reverts the
ticket with no data migration. Complete with Validate.ps1 -ModernCore evidence.

## CORE-02: portable quota decoding

From baseline `1533e9e`, QuotaDecoder in Core owns decoded response mapping and
reset text for Codex, Claude and Antigravity. It accepts ordinary dictionary/list/
numeric/string values and a host observation time. Windows QuotaData retains
JavaScriptSerializer parsing with the same 1 MiB and recursion-32 limits. Providers
and views call Core directly; credentials, URL allowlists, redirects, process
ownership, request cancellation and refresh scheduling retain their existing owners.

Compact/full selection, aliases, zero versus missing, disabled windows, numeric
string rejection, deduplication and reset conversion are unchanged. The native
JSON fixtures also run in CoreTests on Framework and .NET 10 through test-only JSON
bridges; neither JSON parser enters the Core assembly. The modern Core allowlist
includes System.Memory, a standard BCL dependency emitted for string operations.
Full native validation retains provider trust-boundary and UI regression coverage.

This extraction adds no timer, network call or settings migration, and makes no
performance or new-platform support claim. Rollback is the atomic source commit.

## CORE-03: portable FPS history

From baseline `afb115c`, FrameHistory and FrameMetrics live in Core. FrameMetrics
retains its existing global type name for host/PowerShell compatibility. FrameHistory
accepts frames and host monotonic timestamps; the host owns synchronization. It
preserves 16 streams, 90,000 frames per stream, 60-second retention, one-second
active-stream selection, Average, Minimum and slowest-one-percent Low semantics.
Low remains NaN below 100 observations. No statistics algorithm is optimized here.

FrameCapture retains CSV/PID filtering, Stopwatch, PresentMon lifecycle, error status
and locking. It maps unavailable history to the existing host status. FpsTransport
and its process identity/privilege checks are unchanged. The legacy differential
harness compiles the shared history source; no legacy scripts enter the package.

Core tests run identical deterministic observations on both runtimes, including
stale large streams, history/stream bounds, invalid frames, reset and Minimum vs
Low. Native validation retains CSV filtering and FPS transport coverage. This adds
no worker/timer, does not claim lower allocations or actual ARM64/non-Windows FPS
capture, and can be reverted as one atomic commit.

## CORE-04: portable settings values

From baseline `de500fd`, SettingsValues in Core owns typed access, invariant number
coercion/clamping, nested map creation and card-order filtering. The Windows Settings
class extends it with the existing JSON load/fallback and atomic save. Consumers
retain their Settings API and shared Data map; unknown fields are not copied away.
Languages, settings defaults/migrations, UI bindings and storage paths are unchanged.

Core tests on Framework/.NET 10 cover strict string/bool typing, numeric-string
coercion, non-finite fallback, clamp boundaries, shared-map mutation, order filtering
and unknown-field retention. Native/material tests retain JSON roundtrip, saved
preferences and migration assertions. No new timer, settings schema or platform
storage implementation is introduced; rollback is the atomic source commit.

## FPS statistics allocation optimization

After `51ef6d8`, FrameHistory uses direct queue traversal and one reusable double
buffer for Low sorting. The buffer grows only as required, never above 90,000
entries (720,000 payload bytes), and Clear releases it. The host retains its
existing synchronization and clock; no capture cadence, IPC, settings or UI
policy changes. Frame objects and the existing bounded queues are unchanged.

Recent-count stream selection retains first-enumerated ties. Current/Average use
the same observation order; Minimum uses maximum duration and Low sums the slowest
one percent in descending order. A smaller selected stream sorts only its populated
buffer prefix. Tests cover randomized reference values, stream ties/tail reuse,
existing bounds/stale/reset semantics and steady-read allocations. The allocation
check fails before the change and passes on Framework/.NET 10 afterwards.

Measure-FpsHistory.ps1 compares optimized x64 builds of the frozen baseline source
and current source with identical observations; see PERFORMANCE.md. Whole-app and
game performance remain separate measurements. Rollback is this source change.

## Shared display-order normalization

From baseline `b653792`, SettingsValues.Order(key, defaults) owns normalization
for hardware Cards, Desktop metrics and quota Cards. The host supplies supported
keys in default order; Core preserves the first saved occurrence of supported
case-sensitive string keys, ignores other entries, then appends missing keys.
The method returns an independent array without rewriting the saved settings map.
The existing parameterless Order API retains its raw type-filtering behavior.

Cards, DesktopOrder and QuotaView consume this same Core method. Their supported
keys/defaults, visibility, drag handlers, persistence and localization remain in
the Windows host. No settings migration, timer, process, platform API or new
configuration is introduced. This removes three copies of the normalization rule;
it does not implement a cross-platform view or establish a performance gain.

Shared tests run on Framework and .NET 10, covering missing/malformed input,
retired keys, duplicates, case sensitivity, added defaults, empty supported sets
and input/result independence. Full native validation retains Cards/Desktop/quota
ordering and drag regression checks. Allowed scope is this Core overload, its
three consumers, tests and this document; rollback is the atomic source commit,
with no user-data migration. The extraction alone does not require a new installer.

## Portable column layout

From baseline `fac8a12`, ColumnLayout in Core owns the width/column calculation
used by ResponsivePanel for Cards, quota Cards, Settings sections and Desktop.
It returns logical width, column count and cell width as a value type. The host
supplies minimum column width, requested columns and the previous measured count.
The existing maximum of three columns, 10-unit gap and 16-unit auto-growth
hysteresis are unchanged. Initial measurement has no previous-count hysteresis;
manual selection still falls back to what fits, and shrinking remains immediate.

ResponsivePanel retains child measurement, row/independent-column placement,
visibility filtering, animation, hit testing and drag state. Font/DPI conversion,
Desktop fit-to-screen behavior and settings persistence remain host-owned. The
Core calculation accepts the same host-constrained inputs as the former private
method; this is not a new input-validation boundary. It creates no worker, timer
or reference-type result. No smoother animation or reduced CPU/memory is claimed
from extraction alone.

Framework and .NET 10 tests cover an explicit grow/shrink width sequence around
both thresholds, initial/manual modes, maximum columns, unbounded measurement,
positive cell width at a zero-width viewport and font-dependent Desktop minimums.
Full Validate retains actual WPF width/column, Desktop overflow, settings and drag
regressions. Scope is this calculation, its ResponsivePanel consumer and tests;
rollback is one atomic source commit without a settings migration. Native ARM64,
Linux and macOS views remain unimplemented. No standalone installer is needed for
this behavior-preserving boundary change.

## Windows FPS capture adapter

From baseline `6d758dd`, FrameCapture is compiled into Pulse.Adapters.Windows
instead of the App EXE. Its implementation and global type name are unchanged.
It owns PresentMon process startup/stop, CSV decoding, target PID filtering,
clock and serialized access to the existing Core FrameHistory. FpsTransport,
process identity/privilege checks, protected-tool selection, timers and UI remain
in the Windows host. No process, permissions, command arguments or polling
cadence is added or changed.

Build-Native consumes the adapter type through its existing reference. Package
validation rejects an App-owned duplicate and requires adapter ownership. The
legacy Overlay differential source path and isolated Settings fixture copy are
updated; no PowerShell script is added to the shipped payload. WindowsAdapterTests
reference Core explicitly for FrameMetrics and exercise quoted CSV, malformed/PID
filtering, header/reset behavior, Core freshness and stopped state without launching
PresentMon. Existing full native checks retain FPS IPC/peer/cancellation coverage.
Actual full-screen game capture remains a separate device/integration check.

This extraction makes the Windows capture boundary explicit; it does not provide
ARM64 PresentMon binaries or Linux/macOS capture and does not claim a performance
improvement. Rollback is the atomic source/build change; any delivered build must
include the matching App/Core/adapter payload. No settings or wire-format migration
is required. A standalone installer is not needed for this behavior-preserving step.

Legacy validation limitation: Test-Settings' tray/FPS math/CSV filtering/anchor
checks passed, then its old PowerShell Overview failed at 1080p/150% with
22.6667 logical units of overflow. Repeating the same isolated fixture with the
pre-move FrameCapture/Overlay source location reproduced that exact failure.
The capture file's Git blob hash matches the pre-move source. This is retained as
an unrelated legacy UI limitation; the full native Validate suite passed. No
claim is made that the complete legacy Settings suite passed.

## Cross-platform Core CI

`.github/workflows/core.yml` runs the same .NET 10 Core assertion executable on
Windows, Ubuntu and macOS, each with x64 and ARM64 GitHub-hosted runners. Core
source/tests/workflow changes trigger it; workflow_dispatch supports an explicit
check. The matrix disables fail-fast so one platform failure does not hide other
results. Actions are pinned to commits, checkout credentials are not persisted,
permissions are contents-read only, and each job is limited to 15 minutes. It
neither accesses hardware/credentials nor builds, signs or publishes the App.

The workflow installs SDK 10.0.401, prints dotnet --info and requires the actual
Core test process architecture to match the matrix. A runner label alone is not
proof of native execution. Local .NET 10 tests accept the same PULSE_TEST_ARCH
expectation; the Framework test path stays unchanged. Core's existing dependency,
allocation, format, session, quota, settings and layout assertions run on every
matrix entry. Workflow run logs are the evidence for the exact tested commit;
adding this file does not itself establish that any remote job passed.

Runner labels follow the [GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).
Passing these tests establishes shared Core execution only. WPF, Windows adapters,
PresentMon/driver payloads, platform collectors, native Desktop integration and
installers still need independent OS/device validation. CI introduces no runtime
changes or installer release. Rollback is the workflow and test-host assertion
commit; no user settings or repository secret changes are required.

## Windows adapter CI

`.github/workflows/windows-adapters.yml` builds the Framework Core and Windows
adapter assemblies with the existing csc toolchain, then runs WindowsAdapterTests
on Windows x64 and ARM64 hosted runners. RuntimeInformation records the actual
test-process architecture and Framework version; PULSE_TEST_ARCH rejects an
unexpected runtime architecture. The additional RuntimeInformation reference is
test-only and does not alter the adapter or App dependencies.

Coverage includes live read-only physical RAM bounds, optional WMI module/disk
metadata and network links, invalid WLAN input, and synthetic FPS CSV/PID/reset/
freshness behavior. No driver, PresentMon process, credentials, quota HTTP request,
installer or App window is used. A runner may have no Wi-Fi or physical sensor
inventory; optional metadata remains optional. Passing does not establish CPU
sensor/temperature/fan coverage or game FPS capture on ARM64.

The workflow uses pinned checkout with credentials disabled, contents-read
permissions, relevant-path triggers, explicit dispatch, non-fail-fast jobs and a
15-minute timeout. It introduces no runtime/defaults or package changes. Local
full Validate remains required; remote logs establish coverage only for their
exact tested commit. Revert the workflow and test-only architecture probe to roll
back, without changing user settings, repository secrets or permissions.

The first adapter CI run [34973589831](https://github.com/medking82/hardware-pulse/actions/runs/34973589831)
passed x64 but rejected the ARM64 job because its Framework AnyCPU EXE ran as
X64. This is the documented Framework compatibility default, not a passed native
ARM64 test. See the [.NET team's architecture tour](https://github.com/dotnet/core/issues/7709).
Test-WindowsAdapters now offers -NativeArm64, restricted to an ARM64 Windows host,
which launches only its test EXE via start /machine arm64 /b /wait under the hidden
runner. It preserves the test exit code, without registry edits, elevation or an
App launch change. The test still requires RuntimeInformation to report Arm64.
The AnyCPU adapter/Core DLL build remains unchanged. Local x64 validation covers
the existing launch; a wrong-host preflight rejects native ARM64 before building.

The native-launch follow-up [34974094478](https://github.com/medking82/hardware-pulse/actions/runs/34974094478)
failed before adapter execution: cmd.exe rejected mixed-separator launch paths.
A minimal local .cmd probe reproduced the filename-syntax error; canonical Windows
paths for cmd.exe and its launcher passed. The test launcher now normalizes these
paths. This correction changes no adapter/runtime implementation; both failed CI
attempts remain available and are not counted as platform passes.
