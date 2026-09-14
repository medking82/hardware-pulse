# 0.5.0 native runtime migration

Status: native implementation, deterministic checks, independent review and local 0.4.9-to-0.5.0 installed upgrade complete. See ../VALIDATION.md for review adjudication, actual installation evidence and remaining test gaps. Baseline: b0abadc04a32cf1dd689c11600e987141fe78097 (0.4.9). See PERFORMANCE-0.5.0.md for measured comparisons. Release notes identify the published version; this document records the migration design.

## Context and boundary

WidgetHost.cs currently embeds System.Management.Automation for Glass.ps1 and startup helpers, and spawns powershell.exe for Collector.ps1. Glass loads Panel.xaml and owns settings, tray, cards and display. DeviceProfile.ps1/Sensors.ps1 own semantic sensor selection and validation. Install/Set/Remove-Startup.ps1 own Task Scheduler registration. Installer invokes those host modes. Existing CardDrag, WindowSnap, UpdateCheck, FrameCapture and GameOverlay are C# reuse points.

State: LocalAppData/HardwarePulse/widget-settings.json (geometry, language, appearance, cards, names, overlay); ProgramData/HardwarePulse/<SID>/runtime/snapshot.json (schema 1/2 telemetry), cooperative STOP. Two user-owned scheduled tasks separate limited widget from elevated read-only collector. External dependencies remain pinned LibreHardwareMonitor, PawnIO and optional PresentMon. GitHub updater validates the repository, URL, size and SHA-256.

## Guardrails

- Installed application contains no PS1 execution path or System.Management.Automation reference; developer build/test scripts may remain PowerShell.
- Preserve settings keys and unknown settings, semantic sensor rules, stale/null handling, language auto-selection, tray exit, lock/snap/reorder, overlay, updater verification and installer language.
- No sensor control/writes, global execution-policy change, AV exclusion, root-certificate installation, driver update or unrelated user-file edits.
- Keep UI limited. Validate task path, arguments, principal SID and trigger before modifying registration; preserve startup preference and on-demand launches.
- Native implementation in src/Native plus existing C# components, Panel.xaml/resources, build/installer/tests/docs. Keep baseline source during differential validation; package only native runtime after parity passes.
- Test with isolated state and output. Do not overwrite the installed baseline or release 0.5.0 before native parity, build and independent review complete. Settings format stays rollback-compatible; 0.4.9 release/installer remains the rollback boundary.

## Acceptance and comparison

1. Compile C# directly against .NET Framework 4.8/WPF and pinned library; verify compiled assembly references and packaged files contain no PowerShell runtime dependency.
2. Differential synthetic schema-1/2 snapshots: Intel/AMD/NVIDIA, shared memory, duplicate/ambiguous/null/stale readings, labels, capabilities and peaks.
3. WPF interaction tests: settings persistence/legacy migration, 10/12/16 DIP, narrow windows, Details, language, card visibility/reorder, tray lifecycle, lock/snap, update validation and overlay cleanup.
4. Read-only baseline/native comparison with equal inputs, 2-second polling, same window size and feature flags. Report warm-up, sample duration, process tree, CPU normalized by logical processors, Working Set, Private Bytes, startup readiness, and limitations. Use replay separately from real collector to distinguish UI from sensor costs. A screenshot or one Task Manager sample is not a benchmark.
5. Build installer, inspect startup ownership/rollback and test upgrade boundaries; require independent Native Review for frozen high-risk diff. Publish stable 0.5.0 only with truthful tested/untested boundaries and English-first changelog.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"changed","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"material","verification":"deterministic"},"formal_review":"required","kind":"risk-classification-assessment","reasons":{"formal_review":["high_risk_requires_review"],"risk":["privilege_boundary_change"]},"risk":"high","schema_version":2} -->

## In-place upgrade cleanup

Keep the existing AppId and install directory. PrepareToInstall requests cooperative STOP before replacement. InstallDelete lists only known obsolete top-level application scripts, shipped C# source and PulseUpgrade.exe; no wildcards or recursive directory cleanup. Preserve LocalAppData settings (including unknown fields), runtime diagnostics, shared PawnIO and Windows PowerShell. Replace the same two owned scheduled tasks with native executable actions and retain disabled logon preference. Reinstall 0.4.9 restores its package if rollback is required; do not promise transactional rollback for installer file deletions. Verify the old process tree has exited, obsolete files are absent, settings survive, and native UI/collector launch at the intended privilege levels.
