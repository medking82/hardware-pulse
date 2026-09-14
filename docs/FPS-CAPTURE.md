# FPS capture boundary

The 0.5.4 UI runs without elevation but starts PresentMon directly. A local
PresentMon 2.5.1 probe exited with `failed to start trace session: access denied`.
The UI also overwrote `overlay.enabled` at startup and lost its PID-only choice
when the selected process exited. These are separate, reproduced defects.

## Repair

- The existing elevated Collector owns PresentMon. Its fixed bundled tool must
  reside under Program Files with no reparse-point path components. No supplied
  paths, command lines, remote hosts or executable downloads enter this boundary.
- A local named pipe is scoped to the current SID and Windows session. Its DACL
  admits that SID; both endpoints check the peer process's executable, SID and
  session. The UI stays non-elevated. No group membership or system policy changes.
- Requests are exactly 20 bytes: PID, UTC process-start ticks and reset generation.
  The Collector validates target ownership/session/birth and holds a process handle
  through capture teardown to prevent PID recycling. Responses are exactly 44 bytes.
- Disconnect, invalid target, disabled FPS, disabled Overlay or expired three-second
  request lease stops capture. Pipe I/O is bounded and off the UI thread. Capture
  startup failure retries at most once per ten seconds on a live connection.
- UI responses from an old target or reset generation are discarded. Missing fresh
  frames are displayed as waiting, never as a stale live number.
- Auto means **foreground app**, not a universal game detector. Desktop/shell and
  Pulse are excluded. Visiting Pulse or the desktop retains the last target history;
  the overlay is hidden while its target is not foreground. Manual selections are
  stored by process name and resolved again after exit/restart; Refresh retains them.
- The toggle remains opt-in and persists. FPS-disabled hardware overlays do not
  connect to the FPS service. Closing the widget closes its connection.

## Validation

`scripts/NativeFpsTests.cs` covers binary framing, fragmented requests, idle timeout,
peer executable identity, invalid PID/birth, workspace executable rejection,
desktop exclusions and stale readings. Real WPF tests cover persisted enabled state
and selection retention when the selected process is absent. Existing CSV tests
cover process filtering, swapchains and rolling aggregate definitions.

These deterministic checks do not prove elevated ETW capture or every game's API.
Before release, verify the installed ordinary UI / elevated Collector path with a
controlled graphics target, including target restart and stopping capture. Exclusive
fullscreen overlays and generated-frame counts are not supported. Public release
requires the high-risk independent review and the repository validation command.

For 0.5.5, an isolated Program Files probe compiled from the transport/capture
sources verified an ordinary client against an elevated server with the bundled
PresentMon and a WPF/D3D9 target. It read approximately 107–110 FPS, reconnected
after target restart, and stopped PresentMon when disconnected while the target
remained alive. Server teardown left no probe capture process. This is controlled
ETW validation, not a claim of compatibility with every game.

The first live probe exposed PresentMon's lowercase `msBetweenPresents` header;
column matching is now ordinal case-insensitive. The real header has a deterministic
regression. Independent review completed; its translation finding was corrected.

## Overlay appearance

Settings provides a background color picker and a 0–100% background opacity slider.
The preview uses sample data and the same background brush as the overlay. Text
remains opaque. Color and opacity persist independently of the main widget theme;
Reset restores #111923 and 80%. WPF checks cover transparent/opaque endpoints,
saved appearance and reset behavior.
