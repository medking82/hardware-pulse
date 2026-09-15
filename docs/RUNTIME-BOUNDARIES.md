# Runtime ownership

The native runtime has these existing boundaries:

| Owner | Responsibility | Verification |
| --- | --- | --- |
| Collector | Read-only hardware sampling and snapshot publication | Sensor fixtures and installed live snapshots |
| SensorProfile | Map a snapshot to readings and reject stale/invalid input | Native/legacy differential tests |
| Pulse.Core / ReadingSession | Consume a supplied reading source and retain per-session peaks and known capabilities | Pure CoreTests plus Windows snapshot ReadingSessionTests |
| Pulse.Core / QuotaSession | Per-provider refresh schedule, pending work, cancellation and late-result rejection | Core-only quota lifecycle tests and native quota integration |
| Shell and its UI partials | WPF timer, controls, rendering, visibility and user interaction | NativeTests WPF integration |
| Startup / SchedulerStore | Validate ownership and operate the app's scheduled tasks | Startup tests and isolated scheduler integration |
| UpdateCheck | Validate/download installer assets and start installation | Updater verification tests |
| UpdateCoordinator | Version selection, check schedule, operation state, retries and disposed-result handling | Headless UpdateCoordinatorTests |
| MaterialPolicy | Derive effective opacity and backdrop flags from saved preferences and current display state | Headless material tests and WPF lock/settings roundtrip |
| Pulse.Core / ContrastAnalysis | Bounded luminance analysis, temporal hysteresis and local region color/edge confidence | Headless CoreTests; no WPF, Win32 or capture dependencies |
| LocalContrast | Windows capture exclusion, reusable GDI buffers and WPF mask creation | Actual capture, resize, dispose/resume and Screenshot mode integration |

ReadingSession is synchronous and is called on the UI thread. It owns no timer,
window, scheduler or driver. Its state is per Shell instance, and the UI treats
Latest as read-only. Cards and overlay consume the same session. Closing to tray
continues polling; shutdown/STOP handling and the two-second timer remain owned
by Shell. These lifecycle semantics were preserved during extraction.

`scripts/Test-ReadingSession.ps1` references the actual Pulse.Core DLL and compiles
the Windows snapshot DTOs, JSON helper and SensorProfile adapter into its headless
integration fixture. CoreTests separately exercise the same session with synthetic
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

Before adding a modern .NET target, validate its compatibility with the existing
Windows host. ARM64 requires separate validation of the collector/driver and FPS
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
Core reading types; raw snapshot serialization remains in Native. The differential
fixture is named Pulse.SensorFixture.dll to avoid colliding with the real Core DLL.
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
Framework compiler; modern .NET and ARM64/Linux/macOS runtime validation is still
outstanding. No new cross-platform support or memory reduction is claimed.

## Quota contracts and network formatting

The next extraction uses baseline `547e2c8`. `QuotaReading` and `QuotaWindow`
previously shared QuotaData.cs with System.Web response parsing. They now live in
Core; QuotaData and provider credentials/requests remain in Native. QuotaSession
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
Native. No new interface or forwarding layer is needed.

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
