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
