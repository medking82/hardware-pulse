# Memory usage and app cleanup

[简体中文](MEMORY-CLEANUP.zh-CN.md)

## Behavior and boundaries

The main Resources tab opens a shared usage page. It takes an on-demand snapshot
of processes in the current Windows user's interactive session, with RAM working
set and optional reported dedicated GPU memory. Rows represent processes, not
aggregated application totals. Refresh takes another snapshot;
there is no background sampling timer, remote AI request or persisted process list.
RAM/GPU sorting reuses the snapshot. Clicking the active tab retains the current sort.
Click App, RAM or GPU column headers to change the numeric/alphabetic order;
click again to reverse it. Unavailable GPU readings stay last in both directions.
Resources shares the main App window's color, background opacity and Windows glass.
Adjust appearance in Settings → App Appearance; Back returns to Resources.
There are no separate opacity controls on the usage page. Text is never faded
with the background; Solid/high contrast and missing
glass support retain the existing opaque fallback.

Review suggestions identify at most three high-usage normal-close candidates
(RAM at least 512 MiB, or reported dedicated GPU memory at least 128 MiB).
Foreground-at-snapshot and recognized Windows shell/input/session components are
excluded. These are candidates to review if unused, not evidence of unused apps.
Reported per-process usage is shown in the tooltip. Close suggestions opens one
confirmation naming the current candidates, then sends their normal close requests
only if confirmed. Manual checked-app closing remains separate. No automatic close
or selection happens on reading or sorting the snapshot.
Leaving and reopening the page while a read is pending reuses that same task,
rather than spawning parallel scans. Local guidance uses valid physical RAM
headroom, not a guess that a large app is unused. Missing, non-finite or inconsistent
memory readings never produce a "cleanup unnecessary" recommendation.
CPU, RAM and GPU monitoring in the elevated Collector remain read-only.

The user checks one or more apps and confirms a single named batch of normal
close requests. Cancellation dispatches nothing; requests are processed one at a
time without automatic retry. Leaving Resources or hiding this window stops undispatched requests.
Each result reports requested/unavailable counts, not reclaimed memory. Before each dispatch,
the Windows adapter revalidates its process creation time, user, session and main
window. Pulse and the Windows shell are excluded. No main window, access denial,
exit or identity change makes the action unavailable. No process is force-killed,
no UAC elevation is requested, and no service, driver, cache or working set is
purged. App save prompts and refusal remain authoritative. A successful request
is not an exit or a promised number of freed bytes; refresh shows subsequent usage.

RAM working set is resident memory, not private commit or reclaimable memory.
GPU per-process memory is an estimate; shared resources can be counted in multiple
processes, and Microsoft documents inaccurate dedicated counters on some systems.
Unsupported or invalid GPU observations stay unavailable, never zero. Adapter-level
VRAM totals retain their existing sensor source. Closing an app is the generic way
to ask its owner to release RAM and GPU resources; there is no generic cross-process
VRAM trimming API. This feature does not promise improved FPS or identify unused apps.

## Ownership and checks

`WindowsAppResources` owns Windows process/PDH observations and guarded close
dispatch. The WPF `AppResources` partial owns the page, selection, confirmation,
one in-flight operation and local advice. No shared Core contract or Collector IPC
is added. Modern hosts and other operating systems are outside this first boundary.

Identity inspection requests only Windows query access for the process token.
Checks: isolated child windows verify close acceptance, refusal, stale identity,
and disabled/modal main windows;
pure fixtures verify GPU instance parsing, bounds and candidate eligibility/ranking.
The authored WPF check uses synthetic read, confirmation and close delegates; it
never closes a user app. It covers tab entry, Settings/Back navigation, checkbox eligibility,
numeric/alphabetic sorting without another read, invalid RAM, candidate and manual
batch confirmation/cancellation, duplicate clicks, stale results, background opacity,
localization, narrow layout, light/dark rendering and in-flight page teardown.
`scripts/Validate.ps1` remains the pre-commit check. Cross-process actions require
one independent frozen-diff review after deterministic checks. Classification is
high risk when changing the normal close dispatch cross-process action boundary.
The 0.6.49 navigation and layout change preserves that adapter boundary and is
classified as routine, with deterministic UI and affected regression checks.

## Primary sources

- [Windows working set](https://learn.microsoft.com/en-us/windows/win32/memory/working-set)
- [Normal close request](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.closemainwindow)
- [GPU counter limitations](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/gpu-process-memory-counters-report-wrong-value)
- [Shared GPU resource accounting](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/)
- [DXGI device-owned trim](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_3/nf-dxgi1_3-idxgidevice3-trim)
