# Multi-platform stable release readiness

The delivery target is a public stable release with usable, verified packages,
not merely pushed adapters or a renamed experimental release. Existing Windows
WPF v0.6.24 remains stable; v0.7.0-preview.1 is still experimental.

## Scope decision

The user has requested the final stable release. The outstanding product choice
is whether the first cross-platform stable release may declare platform-specific
capabilities, or must match the existing Windows application completely. That
choice has been requested; no response has been recorded yet. Do not silently
interpret a successful launch as full feature parity.

## Acceptance evidence

| Area | Current evidence | Work before stable delivery |
| --- | --- | --- |
| Architecture / CPU / RAM / selected network | Native x64/ARM64 CI on Windows, Linux, macOS | Repeat on the exact release commit; document supported OS and counter limitations |
| Linux temperature / fans | Read-only hwmon fixtures and native graceful-absence tests; shared UI integration implemented with responsive headless tests | Verify native integrated UI and real exposed channels; do not infer physical coverage from an empty CI host |
| Other hardware / FPS / Desktop mode | Existing Windows WPF implementation only | Scope-dependent platform implementation and verification, or explicit unsupported capabilities |
| Settings / tray / lifecycle | Shared isolated settings, headless interactions and native launch checks | Validate intended desktop launch, restore, quit and persistence flows in packaged apps |
| Quota | Opt-in Codex file-login adapter, synthetic file/HTTP and UI tests | Verify intended supported provider scope without exposing credentials |
| Distribution | Six self-contained development archives, inventory, runtime and SHA-256 checks | Final package names/version, user launch/install instructions and platform installation behavior |
| Updates | Stable Windows updater uses latest stable GitHub release | Ensure publishing shared-host assets cannot break installed WPF update selection; choose explicit release channels/asset rules |
| macOS / Windows signing | No publisher signature; macOS not notarized | State the distribution policy and validate the download/launch experience; never disable OS security to pass a check |
| Performance | Short native process measurements and X11 allocation regression | Measure representative integrated workload; report scope and remaining physical-device gaps |
| Publication | Preview release assets independently verified | Freeze final source, run applicable checks, upload matching verified artifacts, verify remote metadata/digests and download paths |

Unsupported hardware must stay unavailable; absence cannot be represented as a
fabricated zero. Existing user settings and the Windows stable installation must
remain untouched by preview development. No new stable release has been made
by this readiness document.

Linux sensor UI integration at `ada4f306c36ef70e32f5975d79be74e7167c44a7`
passed local Desktop render/interaction tests and `Validate.ps1 -ModernCore`.
[Native run 35046006965](https://github.com/medking82/hardware-pulse/actions/runs/35046006965)
passed all six packaged application jobs. This closes integrated native startup
validation for this change, not the remaining release scope decision or physical
sensor coverage.
