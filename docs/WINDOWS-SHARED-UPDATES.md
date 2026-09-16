# Shared Windows updates

The shared Settings view includes Updates with manual check/download/install and
independent automatic check/download preferences. Both automatic preferences
default off. Enabling automatic download also enables automatic checks; it never
installs automatically. Settings use the existing preview profile for now.

## Ownership and admission

Modern Windows adapters compile the same `UpdateCheck` and `UpdateCoordinator`
sources used by WPF. Transport, redirects, byte bounds, SHA-256 verification,
cached-file verification and installer launch are not reimplemented in the UI.
The only runtime compatibility changes are bounded System.Text.Json field
deserialization and a file-scoped modern compiler warning suppression for the
existing HttpWebRequest implementation. Framework compilation retains its
original serializer and behavior.

Actual UI transport is enabled only for a non-preview build running as the
protected installed `HardwarePulse.exe` with its matching protected worker.
Demo, smoke, measurement, development, preview archives and non-Windows hosts
remain unavailable. The current assembly is still `0.7.0-preview.3`; wiring this
feature does not promote that build to stable. Fake clients are test injection
only and are never selected from user input or settings.

`UpdateCoordinator` remains authoritative for busy state, six-hour check cadence,
release selection, duplicate actions, download readiness and disposal. The shared
Monitor's existing polling cadence drives due checks, including hidden tray
mode and paused hardware monitoring. The UI's 500 ms timer observes progress only while an operation is busy.
Closing the Monitor cancels download and ignores late results.

Only a newer stable release with one exact `HardwarePulse-Setup.exe` asset is
eligible. Other platform archives are ignored. Required digest/size and HTTPS
URL/redirect policy remain in `UpdateCheck`; the current installer limit remains
100 MiB. The final shared installer must fit this verified contract or receive a
separately justified change before release. Installation is explicit and repeats
file length/hash verification while holding a read-only handle across launch.

Allowed scope is source reuse, modern JSON compatibility, update presentation,
preview preferences and tests. No release publication, changed trusted host,
changed asset name/size policy, new installer privilege or installed-profile
migration is included. Rollback removes shared wiring while preserving WPF and
settings. The risk classifier records an unchanged privilege boundary; this is
not release approval.

## Evidence and remaining gates

`DesktopUpdateTests` exercises the actual modern metadata parser with stable
mixed-platform assets, prerelease, duplicate assets, malformed metadata, missing
digest and oversized installer cases. UI fixtures verify disabled automatic
checks, serialized download, progress, explicit install, recoverable cancellation,
late-result suppression, preference persistence and three-language narrow layout.
The real modern verifier rejects an unverified launch and passes a known SHA-256
vector. No test installer is launched. Existing Framework coordinator/updater
tests remain in `scripts/Validate.ps1`.

An explicit read-only `--update-live-metadata` test used the actual modern
HttpWebRequest transport and received GitHub latest stable `v0.6.27` on
2026-09-16. It did not download or run an installer. Default test suites are
offline for update behavior.

Validation: DesktopTests Release build, `--update-controls`, full headless,
full `--native-session`, then `scripts/Validate.ps1 -ModernCore`, sequentially.
Local logs: `vendor/test-update-controls-full.log`,
`vendor/test-update-controls-native.log`, `vendor/validate-update-controls.log`.
Final shared installer download, cancellation during actual network transfer,
upgrade/restart/rollback, alias publication and exact-commit installed acceptance
remain release gates. Fake client success is not end-to-end update acceptance.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
