# Shared adaptive layout

P03 remains incomplete. This slice connects the existing `Core.ColumnLayout`
policy to basic Monitor cards and floating CPU/memory/network/hardware groups.
It does not establish visibility/order, quota/FPS layout or locked-height parity.

`AdaptiveReadingsPanel` owns Avalonia child measurement and arrangement. Core
continues to own the 1–3 column calculation, minimum width and Auto hysteresis.
Settings store requested columns separately for cards and Desktop. Narrowing
may reduce the effective count without overwriting the requested preference.
Desktop minimum cell width follows font size, as in WPF. Long labels use spare
row width while values wrap within a bounded width. Empty hardware groups do
not reserve gaps. Live updates retain the existing reading controls.

Scope: shared presentation, separate preview settings, localization and tests.
No Core policy changes, new samplers, collector or installed-profile changes.
Rollback is this presentation diff; persisted unknown fields remain preserved.

Validation: `AdaptiveReadingsTests` covers breakpoints in both directions,
explicit/narrow columns, hidden items, long labels, live control identity and
settings roundtrip. Inspect 360/960/1400px Desktop and Monitor render artifacts;
run the full DesktopTests headless suite, Windows native session and
`scripts/Validate.ps1 -ModernCore`. Final installed-build and resource acceptance
remain separate release gates.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
