# Widget and installer boundary

Extract the existing Hardware Pulse implementation into its own repository without
moving or deleting PC maintenance history. Add original SVG icons, consistent Segoe UI type,
Title Case labels, and drag reordering with keyboard button fallback. Keep equal RAM/NVMe columns.

Installed code and libraries live under Program Files. Mutable state lives in LocalAppData.
The existing current-user, interactive, highest-available collector model remains unchanged.
No service account, hardware control or security-policy changes. User-authorized code signing keeps a non-exportable private key in CurrentUser/My; no key is exported or trusted as a root.
The installer handles the official PawnIO prerequisite and current-user login startup; uninstall
removes only this application's startup task, retaining settings and shared PawnIO.

Verify parsing, vector assets, dependency hashes, compiler output, live UI drag/cancel/persistence,
and sensor regression. Fresh-machine installation is a separate validation limit if no VM exists.
Keep the old running app and task until the new package is ready; do not overwrite unrelated tasks.

Version 0.2.0 adds a separate Settings page, live geometry/preference/Unicode-name persistence,
automatic hardware metadata, RAM/VRAM usage, both reported GPU fan channels, top/bottom magnetic
alignment and a limited-permission GUI login task. Public repository/release publication is
explicitly authorized, with author Marck Wong, GitHub links and a noreply commit identity.

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
