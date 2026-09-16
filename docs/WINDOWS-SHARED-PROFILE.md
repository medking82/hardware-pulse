# Windows stable profile and compatible preference import

## Frozen contract

Only a Windows process identified as the matching protected installed UI/worker
pair and carrying a stable three-part informational version may select the
stable shared profile. The current preview version remains preview. Stable uses
LocalAppData/HardwarePulse/shared-settings.json and instance scope Shared.Stable;
preview/development retains ApplicationData/HardwarePulse.Preview/settings.json
and Shared.Preview. Diagnostics continue without personal persistence. Use the
same admission predicate for the existing stable updater and stable window title.

An existing shared stable file is authoritative, including unknown fields. Never
fall back from a malformed existing stable file to WPF or preview defaults that
can overwrite it. Only when the stable file is absent may the store read the
same user's LocalAppData/HardwarePulse/widget-settings.json. Read a regular,
bounded JSON object and project an explicit allowlist; never copy arbitrary fields,
credentials or account data. No preview preferences are implicitly promoted.

Import explicitly saved common language, dimensions (subject to shared bounds), startup view,
Desktop appearance, shortcut, per-provider opt-ins, FPS selection and update
preferences. Preserve the original WPF file byte-for-byte. WPF screen positions
are DIP-based while the shared saved positions are physical pixels; leave the
new position unset rather than guessing scaling. Card groups and per-reading
layout IDs differ, so leave the new layouts at defaults and show a localized
notice asking the user to review layout and placement. Unsupported WPF-only
preferences remain available in the original file; this is not full UI parity.

Load performs no file writes. The first save after import atomically creates the
new file without replacement. If another file appeared meanwhile, preserve it,
report the save conflict, and require reload; never overwrite that winner with
the imported preimage. Subsequent loads/saves use the established shared store.
Invalid/oversized/unreadable input blocks persistence for that session using the
existing error surface. Migration never changes installed binaries, scheduled
tasks, credentials, preview files or the WPF file.

Allowed changes: shared profile/channel selection, a bounded WPF projection,
store integration, migration notice/localization, tests and this record. Reuse
PreviewSettingsStore normalization and atomic-save behavior. Rollback is a source
revert; the untouched WPF/preview files remain readable by their original hosts.
Do not test against the user's actual profiles or launch an installed stable UI.

Acceptance: pure channel/path cases; fixture import and source-byte preservation;
existing-file precedence and unknown fields; invalid/oversized/reparse refusal;
concurrent-create preservation; optional features not newly enabled; normalized
appearance/shortcut/FPS values; unchanged preview behavior; headless/native UI
and repository validation. The exact passing staged diff requires one Native Review.

## Validation evidence

Windows x64 build passed without warnings. `ProfileMigrationTests` covers BOM
JSON, explicit opt-ins, bounded appearance, source-byte preservation, stable
precedence, unknown stable fields, malformed and oversized input, directory
targets, concurrent creation/reload, distinct paths and preview separation.
It also exercises ancestor reparse refusal using a temporary directory junction
without elevation, and the actual Monitor import notice in English and both
Chinese languages. Fixtures never use personal profiles.

The headless Desktop suite and full Windows native session passed (local logs
`vendor/test-profile-headless.log` and `vendor/test-profile-native.log`). The added
junction case then passed the focused `--profile-migration` run. Repository
`scripts/Validate.ps1` passed under PowerShell 7 (`vendor/validate-profile-pwsh.log`).
The initial PowerShell 5.1 invocation could not run the existing hidden runner;
no validation assertions were changed. Actual protected installed stable admission
and upgrade acceptance remain release-package checks, not claims from these fixtures.

## Portable fixture paths

Desktop CI run 35129749374 passed Windows x64 but macOS stopped at the
read-only BOM import assertion. macOS temporary directories can traverse the
`/var` symlink, which the production legacy reader deliberately refuses.
The regular-input fixture now resolves temporary directory ancestors before
constructing its source path. A separate linked-input case still requires
refusal, then verifies that the resolved physical path imports successfully.
No production migration or reparse-point policy changed. The focused Windows
test passed (`vendor/test-profile-physical-fixture.log`); macOS confirmation
requires the subsequent CI run. Repository validation passed under PowerShell 7
(`vendor/validate-profile-physical-fixture.log`). This follow-up changes only
test setup and evidence; it does not reopen the reviewed migration implementation.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"schema_or_data_migration","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"required","kind":"risk-classification-assessment","reasons":{"formal_review":["high_risk_requires_review"],"risk":["schema_or_data_migration"]},"risk":"high","schema_version":2} -->
