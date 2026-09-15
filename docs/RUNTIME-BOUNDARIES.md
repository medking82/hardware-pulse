# Runtime ownership

The native runtime has these existing boundaries:

| Owner | Responsibility | Verification |
| --- | --- | --- |
| Collector | Read-only hardware sampling and snapshot publication | Sensor fixtures and installed live snapshots |
| SensorProfile | Map a snapshot to readings and reject stale/invalid input | Native/legacy differential tests |
| ReadingSession | Poll snapshots and retain per-session peaks and known capabilities | Headless ReadingSessionTests |
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

`scripts/Test-ReadingSession.ps1` compiles Models, SensorProfile, ReadingSession
and its tests without WPF, WinForms or the app EXE. It is part of Validate.ps1.

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

Further extraction should follow demonstrated consumers: semantic reading DTOs
and session state first, with snapshot JSON/LHM mapping retained in the Windows
adapter. Collector, FPS capture, tray, startup, window layering and update asset
selection remain platform responsibilities. Preserve the existing process and
privilege boundaries while adding platform implementations; unsupported sensors
must be reported as unavailable rather than fabricated as zero.

Before adding a modern .NET target, validate its compatibility with the existing
Windows host. ARM64 requires separate validation of the collector/driver and FPS
payloads; shared UI support alone does not establish telemetry parity. Linux and
macOS require native collectors and desktop integration. These ports are not yet
implemented. See [Local Contrast measurements](PERFORMANCE.md) for this phase.
