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

Verified follow-up: [run 34974501739](https://github.com/medking82/hardware-pulse/actions/runs/34974501739)
passed both adapter jobs at `d068ef7a1b3a2dfc27760fbcbb42641f68166388`.
RuntimeInformation reported Arm64 / .NET Framework 4.8.9337.0 on Windows build
26200 and X64 / .NET Framework 4.8.9339.0 on build 20348. Both passed live RAM,
optional inventory/network and synthetic FPS parsing/reset/freshness assertions.
The native ARM64 launch fix resolved the harness issue without changing adapter
code. This is hosted-runner evidence for that subset, not physical Wi-Fi, LHM
sensor/driver, PresentMon capture, WPF/Desktop or ARM64 installer validation.

## Linux CPU / RAM adapter prototype

`src/Adapters/Linux/Pulse.Adapters.Linux.csproj` is a .NET 10 library referencing
only Core. `LinuxReadings.Read(now)` supplies the existing `ReadingSession`
delegate boundary: `values["cpuLoad"]` and `usage["ram"]`. It owns per-instance
CPU counter history, not a timer or process. Hosts must poll serially. The
Windows build and installer do not load this prototype.

CPU load uses aggregate `/proc/stat` interval deltas, excludes idle/iowait from
busy time, and does not double-count guest time. First samples, zero-length
intervals, counter decreases (including iowait), and recovery after read errors
need a new baseline; they do not fabricate zero load. RAM uses
`MemTotal - MemAvailable`, converted from KiB to the existing GiB usage contract.
Missing/inconsistent fields are unavailable, with no MemFree-only fallback.
One source can fail while the other remains live; errors are retained on the
Reading. Values describe host resources, not container cgroup limits.
See the [kernel procfs documentation](https://docs.kernel.org/filesystems/proc.html).

`dotnet run --project scripts/LinuxAdapterTests/Pulse.Linux.Tests.csproj -c Release`
runs synthetic fixtures on any .NET 10 host. Adding `-- --live` requires Linux
and checks actual CPU/RAM reads through Core ReadingSession. The Linux adapter
workflow runs this on Ubuntu x64 and ARM64, checking process architecture.
This is a data-source prototype, not a Linux App release: UI, tray, desktop layer,
Network, temperatures, fans, FPS and installation remain outside this module.
Validation on 2026-09-15: Linux x64 and native ARM64 fixtures plus live procfs /
ReadingSession integration both passed at commit
`d95e8882058fce6c5109b4e6037d86a92ec53d59` in
[Linux adapter CI](https://github.com/medking82/hardware-pulse/actions/runs/34975685365).
Windows fixture execution and the full `Validate.ps1 -ModernCore` regression also
passed. This does not yet establish polling overhead or container-aware metrics.
## Linux Network reader prototype

`LinuxNetworkReadings(interfaceName)` supplies the same `ReadingSession` delegate
boundary for one explicitly selected interface. It reads `/proc/net/dev` once
per poll, reports `netDown` / `netUp` in bytes per second, and retains the selected
name in `names["Network"]`. The host owns interface selection and serial polling;
no worker, shell command, privilege change or automatic aggregation is added.
The monotonic clock is independent of the Reading's wall-clock timestamp.

Warmup, nonpositive intervals, reset counters, missing interfaces and read errors
produce unavailable readings rather than fake zero traffic. Recovery establishes
a new baseline. Unchanged counters over a valid interval represent actual zero.
The source reflects the process's network namespace. Link speed, physical-link
classification, Wi-Fi signal and automatic routing/interface selection are not
implemented by this throughput reader. See the
[kernel statistics interface](https://docs.kernel.org/networking/statistics.html).

LinuxAdapterTests includes deterministic reset/recovery, exact interface matching,
64-bit counter precision and clock tests. Its existing `--live` path also sends a
single local UDP packet over loopback and verifies positive RX/TX via Core
ReadingSession; it makes no external network request. The Ubuntu x64/ARM64 workflow
runs these assertions. Windows App behavior and packaging remain unchanged.
Network validation (2026-09-15): deterministic fixtures and live loopback traffic
passed on Ubuntu x64 and native ARM64 at
`14fe5ad972155ade4c5de205d4f280112dec6349` in
[Linux adapter CI](https://github.com/medking82/hardware-pulse/actions/runs/34976314145).
The complete Windows `Validate.ps1 -ModernCore` regression also passed. Physical
NIC/Wi-Fi coverage and production polling overhead remain unmeasured.
## Run the Linux headless probe

The prototype host `src/Hosts/LinuxProbe` composes the Linux CPU/RAM and optional
Network adapters through separate Core ReadingSessions. It collects a baseline,
waits one second, prints one JSON snapshot, then exits. No background service,
settings writes, auto-start, WPF dependency or privileged access is introduced.

On Linux with the .NET 10 SDK, from the repository root:

```sh
dotnet build src/Hosts/LinuxProbe/Pulse.Linux.Probe.csproj -c Release
dotnet src/Hosts/LinuxProbe/bin/Release/net10.0/Pulse.Linux.Probe.dll
dotnet src/Hosts/LinuxProbe/bin/Release/net10.0/Pulse.Linux.Probe.dll --interface eth0
```

Replace `eth0` with the desired interface's exact name. Omitting it skips Network
rather than choosing or summing interfaces. `--help` describes usage and exits
without reading procfs. Exit codes are 0 for all requested metrics available,
2 for invalid arguments, 3 for a partial/unavailable snapshot, and 4 for a non-Linux
OS. Partial results are still emitted as JSON, with each source's error retained.

The schema-1 diagnostic envelope includes process architecture, explicit units,
Core readings and a `complete` flag. CPU load is percent, RAM used/total is GiB,
and network throughput is bytes/second. This is an experimental diagnostic
contract, not the Windows collector wire format. Unsupported temperature/fan/link
speed metrics are omitted. Source timestamps are wall-clock observations;
network rate calculations use their own monotonic clock.

The workflow builds the host and runs `scripts/test_linux_probe.py` against the
actual executable on both Linux architectures, covering JSON shape, live CPU/RAM,
loopback selection, missing-interface partial success, help and argument errors.
This is a framework-dependent CLI prototype, not a Linux desktop App/installer;
Windows remains the published product. No raw host snapshots are committed.
Probe validation (2026-09-15): native Linux x64 and ARM64 end-to-end CLI tests
passed at `b82d35f5b9763d47202c95164cfecd26826fda4e` in
[CI run 34977552722](https://github.com/medking82/hardware-pulse/actions/runs/34977552722),
alongside adapter fixtures/live tests. Local Windows CLI rejection/help tests and
full `Validate.ps1 -ModernCore` also passed. Physical network selection and UI
integration remain outside this prototype's verification.
## macOS CPU adapter prototype

`src/Adapters/Mac/Pulse.Adapters.Mac.csproj` targets .NET 10 and references only
Core. `MacCpuReadings.Read(now)` plugs into ReadingSession and produces aggregate
`cpuLoad` percent from Mach user/system/nice versus total interval ticks. It owns
no timer, worker or driver and does not claim RAM, temperature, fan or FPS support.

Native interop uses libSystem's `host_statistics(HOST_CPU_LOAD_INFO)` with four
32-bit counters. `mach_task_self()` is an exported-variable macro, so the adapter
reads `mach_task_self_` rather than attempting to import a nonexistent function.
Each `mach_host_self` send-right acquisition is balanced by
`mach_port_deallocate` in a finally block. Nonzero native return codes and invalid
counts become unavailable readings. First/unchanged samples do not fabricate
zero load. Counter decreases, including 32-bit wrap, skip one interval and reset
the baseline; successful idle intervals still report real zero. The host polls
one instance serially. Non-macOS default construction is rejected before native
calls; injected fixtures remain portable.

References: Apple XNU [host_info.h](https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/host_info.h),
[mach_init.h](https://github.com/apple-oss-distributions/xnu/blob/main/libsyscall/mach/mach/mach_init.h),
and [mach_host_self](https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/man/mach_host_self.html).

`dotnet run --project scripts/MacAdapterTests/Pulse.Mac.Tests.csproj -c Release`
runs fixtures; `-- --live` additionally requires macOS, reads CPU through a Core
session, and performs 1,000 native acquire/read/release cycles, checking errors.
The macOS adapter workflow checks native process architecture on Intel and Apple
Silicon runners. Windows App packaging and the published installer are unchanged;
this library is not a complete macOS App or signed/notarized distribution.
macOS CPU validation (2026-09-15): native Intel x64 and Apple Silicon ARM64
fixtures, live Core session and 1,000 Mach acquire/read/release cycles passed at
`6700ecb21b5aee83909d6884d89ef041bcdaf0ae` in
[CI run 34978438192](https://github.com/medking82/hardware-pulse/actions/runs/34978438192).
Windows fixture execution and full `Validate.ps1 -ModernCore` passed. This is
real system API coverage on hosted macOS runners; retained memory, long-duration
polling cost, RAM and desktop UI have not been validated by this CPU-only module.
## macOS RAM adapter prototype

`MacMemoryReadings` supplies `usage["ram"]` to Core ReadingSession with an explicit
"Used memory estimate" label. Estimated used bytes are
`(internal_page_count - purgeable_count + wire_count + compressor_page_count) * kernel_page_size`.
This accounts for non-purgeable anonymous pages, wired pages and actual physical
compressor storage. It excludes file-backed cache and does not add uncompressed
compressor-equivalent pages or swap storage. It is not a claim of exact Activity
Monitor parity or a substitute for memory pressure. Inconsistent counters produce
unavailable readings rather than clamping to a plausible percentage.

Physical capacity comes from read-only `sysctlbyname("hw.memsize")`; page size
comes from `host_page_size`, not a hard-coded 4 KiB assumption. The established
38-integer prefix of `host_statistics64(HOST_VM_INFO64)` contains all required
counters. CI compiles static offset/width assertions against each native macOS
SDK before running the C# live reader. CPU and RAM share `MacMach` for balanced
host-port acquire/release; each caller retains finally-based release.

References: Apple XNU [vm_statistics.h](https://github.com/apple-oss-distributions/xnu/blob/main/osfmk/mach/vm_statistics.h)
defines the counters; Apple's [Activity Monitor memory guide](https://support.apple.com/guide/activity-monitor/actmntr1004/mac)
distinguishes used, wired, compressed, cached and pressure concepts. The formula
above is Pulse's documented estimate derived from available counters.

MacAdapterTests covers 4/16 KiB pages, units, invalid/overflowing counters, failure
and recovery. `--live` additionally checks 100 actual RAM reads through a Core
session, alongside the existing CPU native tests. There is no new background
worker, privileged access, UI or installer integration. Long-running overhead
and comparison against Activity Monitor still require separate measurement.
RAM validation (2026-09-15): native Intel x64 / Apple Silicon ARM64 SDK ABI checks,
RAM fixtures and 100 live RAM reads through Core all passed at
`7bea6893258dc0695304aee611918ae8dc679b09` in
[CI run 34979454819](https://github.com/medking82/hardware-pulse/actions/runs/34979454819).
CPU fixtures and 1,000 live native CPU calls remained passing after the Mach
ownership extraction. Local Windows fixtures and full validation passed too.
This establishes native ABI/read coverage, not Activity Monitor parity or a
long-duration resource/performance claim.
## Run the macOS headless probe

The macOS host in `src/Hosts/MacProbe` composes the CPU and RAM adapters through
Core ReadingSessions. Linux and macOS compile the same `Hosts/Common/ProbeRuntime`
source for one-second baseline/final sampling and JSON output. This host-only
code deliberately keeps timing and serialization outside Core; it adds neither
a shared background process nor a runtime plugin framework. Linux's existing
CLI arguments, schema and output fields are preserved.

On macOS with the .NET 10 SDK, from the repository root:

```sh
dotnet build src/Hosts/MacProbe/Pulse.Mac.Probe.csproj -c Release
dotnet src/Hosts/MacProbe/bin/Release/net10.0/Pulse.Mac.Probe.dll
dotnet src/Hosts/MacProbe/bin/Release/net10.0/Pulse.Mac.Probe.dll --interface en0
```

The probe emits one schema-1 diagnostic envelope (`platform: macos`) with `cpu`
and `memory` source readings, architecture, units and `complete`, then exits.
RAM retains the adapter's explicit estimate label. Errors stay attached to their
source; unavailable requested metrics yield exit 3 instead of fabricated values.
Other exit codes match Linux: 0 complete, 2 invalid arguments, 4 unsupported OS.
`--help` works without native reads. Optional `--interface NAME` selects exactly
one interface and adds a `network` reading plus its `bytes/second` unit. Use an
interface name present on that Mac; `en0` above is only an example. No-argument
output retains its CPU/RAM-only fields. A missing interface produces partial
JSON and exit 3 while preserving valid CPU/RAM readings. Nothing is saved or installed.

Both platform workflows watch shared host changes and execute their actual CLI
process tests. Windows locally verifies build/help/argument/OS guards; native
Linux and macOS runners verify real output and architecture. This remains a
framework-dependent diagnostic prototype; desktop UI, signing and installer
release are separate work.
Shared-host validation (2026-09-15): macOS Intel/Apple Silicon CLI tests passed in
[run 34980241663](https://github.com/medking82/hardware-pulse/actions/runs/34980241663),
and unchanged Linux x64/ARM64 CLI contracts passed in
[run 34980241703](https://github.com/medking82/hardware-pulse/actions/runs/34980241703),
both at `6372b01904fe13b4988d77ac63114989867cb2d2`. Local Windows builds, CLI guard
tests and full native validation passed. No desktop UI or distribution-signing
coverage is implied by this end-to-end headless result.

## Shared Network intervals and macOS adapter

`Core/NetworkInterval` owns byte-counter deltas, monotonic elapsed time and reset
semantics for Linux and macOS. Adapters own interface selection, source IO and
clock acquisition. Warm-up, zero/backwards time, decreasing counters and source
failure never fabricate traffic; a valid idle interval remains zero. Integer
counters are subtracted before floating-point conversion. No polling timer or
additional process is introduced.

`MacNetworkReadings` uses .NET's BSD interface statistics for one exact interface
name, returning `netDown`/`netUp` in bytes per second through the existing Reading
contract. Missing interfaces and failed reads reset the baseline. It does not
aggregate Wi-Fi/LAN/VPN links or claim link speed and signal support. The native
fixture sends UDP traffic over `lo0`; Linux retains its existing `lo` fixture.
The macOS CLI composes this adapter only when `--interface NAME` is requested.
Short polling/resource baselines are recorded in [PERFORMANCE.md](PERFORMANCE.md);
long-duration resource stability is not yet established.

macOS now retains only the selected interface reader. Its BSD `GetIPStatistics`
call obtains fresh counters by name on every poll; cached interface speed/status
metadata is not consumed. Expected read failures discard the selection, and the
next poll resolves it again. Source errors reset the Core interval as before;
decreasing counters from a same-name replacement also establish a new baseline.
Fixtures cover fresh counters without repeated resolution, failed selection,
missing/reappearing sources and counter reset. No new timer or event subscription
is needed. The old enumeration-per-poll path remains in the benchmark only for
paired comparison against the selected-reader path.

Network validation (2026-09-15) passed at
`a27f97a13cbe46d785ec75ad745863e02d6f9100`: macOS Intel/Apple Silicon native
loopback and existing CPU/RAM/CLI checks in
[run 34981727987](https://github.com/medking82/hardware-pulse/actions/runs/34981727987),
unchanged Linux x64/ARM64 checks in
[run 34981727961](https://github.com/medking82/hardware-pulse/actions/runs/34981727961),
Core on all six OS/architecture targets in
[run 34981728005](https://github.com/medking82/hardware-pulse/actions/runs/34981728005),
and Windows adapter regression in
[run 34981727948](https://github.com/medking82/hardware-pulse/actions/runs/34981727948).
Local platform fixtures and full Windows native validation also passed.

## Shared Codex quota flow

`Adapters/Common/CodexQuota.Read(login, request, cancellation)` composes a
bounded decoded login graph, an injected authenticated request and the existing
Core QuotaDecoder. It returns the existing QuotaReading and preserves known
QuotaFailure status keys; unexpected exception details are not returned. It
owns no file paths, credential store, HTTP stack, endpoint, timer or persistence.
Cancellation propagates, including before login access and after a completed
request, so a cancelled response is never decoded as a fresh result.

Windows QuotaProviders supplies its existing bounded auth.json reader and fixed
Codex endpoint/header composition. Its HTTP allowlist, redirect rejection, TLS,
timeouts, response bounds and cancellation/abort code remain unchanged. Claude
and Antigravity retain their existing IO paths. Common source is compiled into
the existing Windows adapter DLL, preserving the installed payload structure;
modern hosts can reference Pulse.Adapters.Common instead. Core remains free of
credentials and transport dependencies.

The same synthetic Codex flow fixtures run against the actual Windows adapter
assembly under Framework 4.8 and the modern Common adapter under .NET 10. They
cover optional account, absent/invalid token, safe failure statuses, response
decoding and cancellation before/during/after IO. No real credential or external
quota request is used. Native quota integration retains HTTP allowlist/redirect
and WPF/session checks. The six-platform workflow runs Common adapter fixtures
alongside Core, and the Windows adapter workflow watches Common source changes.
Linux/macOS credential and HTTP implementations remain subsequent work; this
extraction alone does not enable live quota on those platforms.

Validation at `d12700c4f16d193132513057441c0fcbb9e4a954` (2026-09-15):
Core and shared Codex flow passed on Windows/Linux/macOS x64 and ARM64 in
[run 34985433053](https://github.com/medking82/hardware-pulse/actions/runs/34985433053).
The Framework Windows adapter build and the same flow fixtures passed on x64
and native ARM64 in
[run 34985432904](https://github.com/medking82/hardware-pulse/actions/runs/34985432904).
Local full validation passed, including existing native quota/session, HTTP
allowlist and redirect checks. These are synthetic credential/transport tests,
not verification of a live account or Linux/macOS credential discovery.

## Linux Codex quota IO

`LinuxCodexQuota` supplies the shared Codex flow with a read-only auth.json source
and a reusable HttpClient. Default construction requires Linux and resolves
`CODEX_HOME/auth.json`, falling back to the user's `.codex/auth.json`. The
explicit path/handler constructor supports isolated fixtures without inspecting
real login state. The host owns adapter disposal and refresh cadence; this
adapter is not automatically enabled or composed into LinuxProbe.

The request is GET to the fixed Codex usage HTTPS endpoint with the existing
Bearer and optional account headers. The production handler disables redirects
and cookies, retains normal certificate validation, and has no token refresh or
credential writeback. A ten-second linked deadline covers headers and response
body; caller cancellation propagates while a deadline produces Quota unavailable.
Login and streamed response input are capped at 1 MiB, JSON depth at 32, and
UTF-8 BOM is accepted. Decoded dictionaries/lists feed the existing Core decoder.
Known HTTP status mapping is retained; unexpected exception text is never shown.

Linux adapter fixtures cover synthetic file-to-request-to-decoder composition,
token/account headers, missing and oversized login, bounded/invalid/deep JSON,
unknown-length oversized response, redirect status rejection, actual production
handler flags, timeout, caller cancellation and disposal. They make no external
HTTP request and never use a real account. Live endpoint/account and keyring-only
login support are not established by these fixtures. Windows IO remains unchanged.

Validation (2026-09-15) at `7c89212ff9743c524be8b4cd7fff35b67f68a228`:
Linux x64 and native ARM64 passed synthetic Codex IO fixtures, existing live
CPU/RAM/Network assertions and unchanged Probe CLI contracts in
[run 34986654557](https://github.com/medking82/hardware-pulse/actions/runs/34986654557).
Local fixtures and full Windows native validation also passed. No actual login
or external quota endpoint was accessed during validation.

## macOS Codex file-login adapter

`MacCodexQuota` now provides the same file-login capability as Linux. Both
platform entry points guard their default constructor by OS, then delegate to
`Adapters/Common/Modern/FileCodexQuota`. That owner contains the single bounded
file reader, JSON graph conversion, fixed-endpoint HTTP client and disposal
implementation. Paths follow CODEX_HOME or the user's .codex directory; existing
Linux public construction and Read/Dispose behavior are retained.

Modern IO source lives below Common/Modern and is included by the SDK project;
the Framework Windows build explicitly compiles only Common's top-level flow
source. Consequently Windows keeps its existing credential/HTTP implementation,
without adding System.Text.Json or HttpClient to the installed Framework path.
The existing Linux IO fixtures are now one shared source compiled into both
platform suites and executed through each real platform adapter entry point.

This supports read-only auth.json credentials on macOS, not Keychain-only login,
token refresh or login UI. Neither CLI automatically opts into account access.
Host composition, real endpoint/account validation, desktop UI and signed
distribution remain required steps toward complete cross-platform releases.

Validation at `6dcdc0bf41ae956bb35e8ce83c105aceb0029251` (2026-09-15):
macOS Intel/Apple Silicon file-login IO fixtures and existing live/CLI checks
passed in [run 34987581234](https://github.com/medking82/hardware-pulse/actions/runs/34987581234).
Linux x64/ARM64 regression passed in
[run 34987581257](https://github.com/medking82/hardware-pulse/actions/runs/34987581257),
six-platform Core/Common checks in
[run 34987581261](https://github.com/medking82/hardware-pulse/actions/runs/34987581261),
and Framework Windows adapters in
[run 34987581239](https://github.com/medking82/hardware-pulse/actions/runs/34987581239).
Local full Windows validation passed. No real quota account was accessed.

## Windows native capability probe

`scripts/Test-WindowsCapabilities.ps1` builds a Framework probe against the actual
Core/Windows adapter assemblies and verifies native process architecture, bounded
PE metadata parsing and a live RAM read. CI runs it on Windows x64 and ARM64;
the ARM64 launcher explicitly requests a native ARM64 process.

Run `build/adapters/WindowsCapabilityProbe.exe --package build/app` to inspect an
existing package. Its JSON reports OS/process architecture, RAM availability,
network interface count, known package PE machine types and PawnIO installation
indicators. It does not load package binaries, install a driver or elevate.
No adapter identifiers, personal paths or credential contents are emitted.
I386 metadata can represent managed AnyCPU; file presence and COFF machine types
alone do not establish execution or hardware support.

The current Windows build still explicitly targets x64. Local package inspection
found AMD64 App, LibreHardwareMonitor and PresentMon binaries; Core and Windows
adapter assemblies report I386 metadata. CPU temperature, fans, game FPS and
desktop integration are explicitly `not-tested` by this probe. A native ARM64
App build, dependency selection and real hardware/game verification remain
necessary before offering an ARM64 installer. This developer probe does not
change the installed Windows application or its sampling workload.

Validation at `4daf35a8bab5bbf4918c80b9f41389abd4a12cc1` (2026-09-15):
Windows x64 and native ARM64 adapter/probe checks passed in
[run 34989034498](https://github.com/medking82/hardware-pulse/actions/runs/34989034498).
Both reported a successful RAM read and three network interfaces; ARM64 reported
both OS and process architecture as Arm64. Neither runner had PawnIO installed,
so this provides no driver, temperature or fan support evidence. Local full
Windows validation, including Modern Core, also passed.

## Shared Desktop UI preview

`src/Hosts/Desktop` is a separate .NET 10 / Avalonia 12.1.2 host, with NuGet
lock files. It composes existing Linux and macOS adapters through ReadingSession;
neither Core nor adapters acquire an Avalonia dependency. Windows continues to
ship the existing WPF host. The new host requires explicit `--demo` on Windows.

Run `dotnet run --project src/Hosts/Desktop/Pulse.Desktop.csproj -c Release` on
Linux/macOS for CPU load, memory and a selected network interface. `--demo`
uses labeled sample data on every OS. `--smoke-test` closes after three samples
and fails when native CPU or memory readings are unavailable. This is a source
preview, not a platform installer or feature-parity release.

One serial worker owns the sessions; a one-second PeriodicTimer schedules polls,
IO runs off the UI thread, pause skips polling, and closing cancels the worker.
Updates reuse existing controls. Network interface selection creates a fresh
baseline; missing data clears the displayed value. macOS RAM is labeled an
estimate, and memory uses GiB. Codex quota access is explicitly opt-in (see below).

The native resizable window uses the platform default font, system theme, solid
background, existing vector assets and responsive one/two-column cards. Headless
Skia tests render the actual controls and exercise width changes, unavailable
values, keyboard pause and worker shutdown. CI separately starts real Windows
demo windows and Linux/macOS live windows; headless results do not prove native
window behavior. Desktop layer, tray, persistence, other quota providers, temperatures, fans,
FPS, blur and public platform distribution remain unimplemented in this host.

Validation at `52a5b411f69b854e02cfdaac67b1cacb4d36028c` (2026-09-15):
[Desktop preview run 34990240159](https://github.com/medking82/hardware-pulse/actions/runs/34990240159)
passed on all six Windows/Linux/macOS x64/ARM64 runners. Each ran the real-control
render/layout/keyboard/shutdown checks. Linux X11 (under Xvfb) and macOS native
windows completed three samples with live CPU/RAM available; Windows native
windows used explicitly labeled demo values. This does not verify Wayland,
game overlays, long-running performance or a distributable App package.
Local full Windows regression and locked dependency restore also passed.

## Desktop Codex quota composition

The shared UI now contains an opt-in Codex card. It is off at startup and creates
no credential reader or HTTP client until enabled. The host composes
LinuxCodexQuota or MacCodexQuota for each read, disposing the adapter afterward;
demo mode always uses labeled synthetic quota, including on Windows.
Existing file-login, fixed-endpoint, bounded-response and cancellation policies
remain owned by the platform adapter. Login UI, token refresh and keychain-only
credentials are not implemented by Pulse.

CodexQuotaPanel owns controls and a dispatcher timer only while enabled.
Core QuotaSession owns its five-minute refresh cadence, one pending request,
cancellation and result generation. Manual refresh uses the same session;
hardware pause is separate. Disable clears the visible readings and cancels the
request; closing the window disposes the session. Canceled results cannot replace
a newly enabled generation. No quota preference is persisted yet.

The card displays AllWindows, falling back to Windows only when the full list is
empty. Unknown remaining values render as an em dash without a progress bar.
Failures clear previous bars; login-required state directs users back to Codex.
Rows wrap, and reset times use the local timezone. Controls are rebuilt only when
the session publishes a new reading, not on every timer tick.

Headless tests run an actual dispatcher loop (RunJobs alone does not advance
dispatcher timers), using synthetic readers to verify keyboard opt-in, additional
quota pools, unknown values, retry, disable/re-enable stale-result rejection and
request cancellation. Tests and native smoke mode do not read real credentials
or contact the quota endpoint. Actual account verification remains outstanding.

Validation at `6ab4bb7377ce0562ac80d79e9c4df10ac8d1249c` (2026-09-15):
[run 34991684994](https://github.com/medking82/hardware-pulse/actions/runs/34991684994)
passed all six Windows/Linux/macOS x64/ARM64 jobs. Each executed the synthetic
Codex UI lifecycle tests and existing native window smoke checks. Local full
Windows validation, including Modern Core, also passed. No live account or
credential file was accessed during these checks.

## Self-contained Desktop development packages

`scripts/package_desktop.py --rid <RID>` publishes linux-x64, linux-arm64,
osx-x64 or osx-arm64 with .NET 10.0.12 and locked Avalonia dependencies. It creates
a tar.gz and SHA-256 sidecar under dist/desktop-preview. Linux uses a directly
executable folder; macOS uses Pulse Preview.app/Contents/MacOS with Info.plist.
These are development CI artifacts, not installer releases or notarized apps.

Each archive carries source commit, dirty-state marker, RID, a full file digest
manifest, dependency lock and available upstream licenses/notices. Normal builds
reject tracked source changes; --allow-dirty is for local validation only.
The verifier extracts into a separate temporary directory, checks inventory,
hashes, executable permissions, native AppHost architecture, included runtime
configuration and native runtime/graphics libraries. Native CI starts the
extracted executable for three live CPU/RAM samples with DOTNET_ROOT pointing
at a nonexistent directory. It does not invoke dotnet to launch the package.

Windows can cross-publish and inspect Linux archive structure, but cannot prove
native Linux execution. Unix permissions are encoded explicitly into tar so
Windows-produced archives retain the AppHost executable bit. Negative fixtures
reject altered files, inventory changes, framework-dependent runtime config,
missing executable permissions and incorrect archive digests.

CI retains successful native packages for seven days. Self-contained packaging
removes the installed .NET requirement, not native OS graphics/font dependencies.
Gatekeeper/quarantine behavior, Developer ID signing/notarization, native Skia
and HarfBuzz transitive notice audit, installation/update UX and broader device
testing remain release work. Packages do not register startup or install files.

Validation at `1f771479f86f6268cd7c2eb5a6287dd7ee1325e5` (2026-09-16 local):
[run 34993429993](https://github.com/medking82/hardware-pulse/actions/runs/34993429993)
passed all six jobs. The four Linux/macOS runners each launched the extracted
self-contained AppHost and obtained live CPU/RAM; Windows kept its demo/native
UI regression. All four uploaded artifacts were downloaded again and passed
local inventory/hash/runtime checks, with the same source commit and dirty=false.

| RID | Archive bytes | SHA-256 |
| --- | ---: | --- |
| linux-x64 | 44302848 | `2fef0887b4ecc8303a106f2e1bdbffd4ec69c1de6d4ba33417bc4b2f6e803948` |
| linux-arm64 | 41894920 | `655aeeb07a2bccc4bb12f6dc20d38687c9dadad459daadf89b5124e3953c007e` |
| osx-x64 | 46231867 | `890722404b10402591e4213533883da0c73832c5abffc12be3dcc5ffec07566b` |
| osx-arm64 | 44034795 | `7ef7ae59c5cb3ab14e6d37cc4ba8675832a17ba174f499b4343872e445ae58c3` |

These are compressed download sizes, not memory usage. Local full Windows
validation, UI contracts and package negative tests also passed.
