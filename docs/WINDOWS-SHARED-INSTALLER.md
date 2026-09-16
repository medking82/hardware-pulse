# Shared Windows installer readiness

## Shared variant contract

Add an explicit `SharedDesktop` compiler variant that reads only
`build/windows-shared/app`, requires an explicit package version, and invokes
`worker/HardwarePulse.Collector.exe` for startup registration and removal.
Keep the AppId, protected installation directory, UI launch path, OS gate,
driver prerequisite, cooperative shutdown and completed-install guard. Reject
combining shared with Win7. Existing WPF/Win7 builds remain the defaults.

This change owns only installer source selection and fixed management paths,
plus opt-in compile checks in `Test-InstallerVariants.ps1 -SharedDesktop`.
Compile using `/O-`; do not execute this installer, modify installed files/tasks,
promote preview, bypass the missing package verifier, or claim upgrade rollback.
The existing local shared payload is development evidence, not a final artifact.
Acceptance: all three variants compile; expanded shared source uses dedicated
worker management while both launch entries still use the UI; missing version
and shared/Win7 combinations fail. Existing validation must still pass.
Rollback is reverting this source change; no installed state is changed.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->

The default shipping installer configuration is still the WPF 0.6.27 variant. A compiled
collector shutdown guard is an incremental prerequisite, not a shared 0.7.0
installer or proof of upgrade/rollback acceptance.

The shared variant now compiles separately with `SharedDesktop` and
`SharedVersion`. Local evidence is `vendor/test-shared-installer-variants.log`
and `vendor/installer-variants-09f86b0ed2b340d4b34ba99902973104/`.
Expanded scripts verify all three payload/management selections, unchanged UI
launch entries, and rejection of missing-version and shared/Win7 combinations.
All production variants were compiled with `/O-`: no shared installer artifact
was emitted or executed. This does not validate the existing development payload
or supply the missing package verifier.
Repository `scripts/Validate.ps1` also passed under PowerShell 7; local evidence
is `vendor/validate-shared-installer-variant.log`.

## Collector shutdown gate

`PrepareToInstall` previously enumerated only `HardwarePulse.exe`, so a dedicated
shared worker could be missed before files were replaced. The gate now enumerates
both executable names and matches exact paths relative to the installation:
`HardwarePulse.exe --collector` and
`worker/HardwarePulse.Collector.exe --collector`. An ordinary UI or management
process does not count. A same-named executable in another directory is ignored.
The gate also runs when the worker payload remains but the old UI file is absent.

The existing cooperative STOP request, setup-account SID selection, ten-second
wait, inability-to-inspect failure and refusal while another signed-in collector
remains are unchanged. No additional process is terminated and no task ownership
or privilege rule is changed. Rollback is the source diff; tests do not touch the
installed application or tasks.

`installer/CollectorIdentity.iss` supplies the predicate used by the production
gate and a no-install Pascal Script harness. `Test-InstallerVariants.ps1` compiles
both existing production variants and this harness, executes the predicate
checks at lowest privilege, and requires the success marker plus exit code 1.
The harness deliberately returns False from InitializeSetup, before installation.
The [Inno event contract](https://jrsoftware.org/ishelp/topic_scriptevents.htm)
and [exit codes](https://jrsoftware.org/ishelp/topic_setupexitcodes.htm) define
this outcome. It contains no application payload, registration or uninstall steps.

Fixtures cover both generations, path case, ordinary UI, management arguments,
foreign paths and sibling worker directories. Expanded production scripts must
contain the shared-worker query and the production predicate call. This does not
simulate actual WMI access, multiple signed-in sessions or a running installed
worker; those remain installed acceptance.

Local evidence: `vendor/test-installer-collector-gate.log`, the referenced
`vendor/installer-variants-*/identity-run.log`, and
`vendor/validate-installer-collector-gate.log`. The identity log records the pass
marker followed by `InitializeSetup returned False; aborting.`.

## Post-install outcome and relaunch

An isolated Inno probe exposed a prerequisite/startup failure-reporting defect:
an exception raised at `ssPostInstall` can be displayed/logged while setup still
exits zero, retaining replaced files. The old update `[Run]` entry also lacked
`postinstall`, so it could launch the application before that startup work.
The official [installation order](https://jrsoftware.org/ishelp/topic_installorder.htm)
documents the late no-rollback boundary; the
[event contract](https://jrsoftware.org/ishelp/topic_scriptevents.htm) provides
`GetCustomSetupExitCode` for overriding an otherwise successful result.

`InstallOutcome.iss` now holds an initially false completion state. The installer
marks it complete only after prerequisite checks and startup setup return
success. Both launch entries run at `postinstall` and require that state. An
otherwise successful setup exits 10 when it is still false; normal completion
remains zero and native preflight/error exit codes retain priority. The completed
wizard page describes incomplete setup instead of claiming success. This does
not restore replaced binaries, tasks or prerequisites and must not be presented
as full upgrade rollback.

`Test-InstallerRollback.ps1` builds a lowest-privilege, x64, text-only installer
in a unique `vendor/installer-rollback-*` directory. It requires a matching fixture
token, refuses a different install destination, preserves a keeper file, registers
no application/uninstaller/tasks, and uses only a harmless `whoami.exe` launch
entry to observe execution ordering. Baseline and `-GuardOutcome` runs cover:

| Injected phase | Old exit / launch | Guarded exit / launch | Files after setup |
| --- | --- | --- | --- |
| PrepareToInstall | 7 / no | 7 / no | old payload preserved |
| After first file callback | 0 / yes | 0 / yes | new payload retained |
| ssPostInstall | 0 / yes | 10 / no | new payload retained |
| Success control | 0 / yes | 0 / yes | new payload installed |

The file-callback case is an observation of Inno's exception handling, not a
failure caught by this post-install completion guard. Shipping setup has no such
callback. The guard specifically covers its prerequisite/startup work. Expanded
modern/Win7 scripts must contain both guarded late launch entries and the custom
exit/completion contract. The harmless probes exercise the actual shared include;
they do not install either production variant or change the user's application.

Allowed scope is outcome reporting, launch ordering, compiled fixtures and this
record. File replacement, task XML ownership/migration, driver installation rules,
user profiles and version/channel selection remain unchanged. Rollback of this
source change is a Git revert. Final file/task rollback composition still needs
an explicit transaction design and isolated installed acceptance.

Evidence: `vendor/test-installer-outcome-baseline-final.log`,
`vendor/test-installer-outcome-guarded-final2.log`, and the per-case setup logs
they reference. The successful guarded case records post-install completion
before the actual run entry; the failing case has no run entry and exits 10.
Both compiled shipping variants passed `Test-InstallerVariants.ps1`
(`vendor/test-installer-outcome-variants.log`). Full `Validate.ps1 -ModernCore`
passed (`vendor/validate-installer-outcome.log`). The actual interactive failure
page and production installation have not been exercised by these silent fixtures.

## Remaining installer contract

The shared variant must select the verified shared UI/worker payload, invoke the
worker for startup installation/removal, retain the shared PawnIO prerequisite,
preserve existing settings and startup preferences, and handle migration failure
without leaving old tasks pointing at an incompatible replacement executable.
The worker's task-XML rollback alone does not prove full installer file rollback.
Upgrade, interrupted/failing registration, uninstall and rollback need explicit
end-to-end acceptance before a stable alias is published.

The development payload builder still depends on the quarantined
`Test-WindowsShared.ps1`. It has not been restored, recreated or bypassed. Current
compiled WPF variants and the no-install harness do not satisfy that shared
payload-verification gate. Keep original user work and installed profiles intact.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
