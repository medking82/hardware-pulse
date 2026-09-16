# Shared Windows installer readiness

The shipping installer source is still the WPF 0.6.27 variant. A compiled
collector shutdown guard is an incremental prerequisite, not a shared 0.7.0
installer or proof of upgrade/rollback acceptance.

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
