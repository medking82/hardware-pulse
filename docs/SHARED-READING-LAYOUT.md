# Shared reading visibility and order

Monitor card groups and individual Desktop metrics have separate layout
preferences and editors in Settings → Layout. Checkboxes control visibility;
up/down buttons support pointer and keyboard activation with accessible names.
Hiding a view does not enable or disable its sampling source. Existing quota
and FPS switches remain the acquisition controls.

`ReadingLayout` owns order/hidden preferences. `PreviewSettingsStore` owns their
separate preview-profile persistence, preserving unknown root and nested fields.
Unknown/disconnected IDs retain preferences; new metrics default to visible.
`DesktopRows` maps supplied hardware, quota and FPS snapshots into stable IDs,
labels and values. Labels are localized but IDs are not. Provider window keys
include provider, original label and duplicate occurrence, keeping same-label
windows from different providers distinct.

`MonitorWindow` owns card presentation and both layout editors; the floating
window owns retained row controls. Changes apply to the existing windows.
Live updates reuse controls; topology changes add/remove only affected IDs.
The flat Desktop panel permits ordering across hardware, network, quota and FPS
and uses the existing adaptive columns. Quota status/remaining and unavailable
values preserve their prior behavior.

Guardrails: no collector, Core acquisition, credential, privilege, WPF, startup
or installed settings changes. Scope is shared host presentation/settings and
tests. Rollback is this presentation diff; do not delete existing settings.
The prior separate adaptive groups were removed because they prevented
cross-group order; they were not duplicated into another sampling path.

Validation: `ReadingLayoutTests` runs headless and in the native Windows suite.
It covers independent visibility/order, updates while hidden, retained controls,
device disappearance/reappearance, language changes, 360px editor bounds,
restart restoration, unknown preferences and unchanged optional-source opt-in.
Existing hardware/quota/FPS, adaptive layout, localization, tray and native input
regressions remain required, along with `scripts/Validate.ps1 -ModernCore`.
Local evidence: `vendor/test-layout-full.log`, `vendor/test-layout-native.log`,
`vendor/validate-shared-layout.log`, and `vendor/layout-ui/` screenshots.
These are incremental checks, not final installed or resource acceptance.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
