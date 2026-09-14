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

ReadingSession is synchronous and is called on the UI thread. It owns no timer,
window, scheduler or driver. Its state is per Shell instance, and the UI treats
Latest as read-only. Cards and overlay consume the same session. Closing to tray
continues polling; shutdown/STOP handling and the two-second timer remain owned
by Shell. These lifecycle semantics were preserved during extraction.

`scripts/Test-ReadingSession.ps1` compiles Models, SensorProfile, ReadingSession
and its tests without WPF, WinForms or the app EXE. It is part of Validate.ps1.

Remaining coupling: Shell.Services still combines updater presentation/state
transitions with overlay presentation. A later, separate refactor should extract
the updater coordinator while retaining UpdateCheck's asset validation and
installation boundary. This document does not describe that extraction as done.
