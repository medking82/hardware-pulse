# Windows shared Desktop shortcut

The shared host retains the WPF shortcut policy: two or more of Ctrl, Alt and
Shift with A–Z, 0–9 or F1–F11. The default is Ctrl+Alt+F10. The Desktop settings
offer enable/disable and keyboard capture; Escape cancels and Tab leaves capture.
Registration conflicts preserve the previous combination and show feedback.

`WindowsDesktopShortcut` owns the Monitor HWND's registrations and Avalonia hook.
It registers a replacement before releasing the old combination, requests no
key repeat, and releases registrations on disposal. `DesktopShortcutSettings`
owns capture and status; `PreviewSettingsStore` preserves the separate profile.
`MonitorWindow.ToggleFloatingMonitor` shows/hides the same floating window and
locks it when shown. Monitor/tray reopening remains the unlock route. No extra
sampler, polling timer, collector, elevation or installed settings are introduced.
Demo, smoke, measurement and unsupported-platform sessions do not register a key.

Scope: these three host surfaces, the existing settings/localization files and
DesktopTests. Preserve all WPF source and collector boundaries. Rollback is the
shortcut-only source diff; unknown-field persistence preserves saved preferences.

Validation: `DesktopShortcutTests` exercises capture, conflict preservation,
enable/disable, persistence, same-window identity and disposal. Its native branch
uses two owned HWNDs to test real OS conflicts, transactional replacement,
WM_HOTKEY dispatch, clear and disposal. It never steals an external registration.
Run both the full DesktopTests headless and Windows `--native-session` suites,
inspect the 360px screenshot, and run `scripts/Validate.ps1 -ModernCore`.
These checks do not establish physical-key behavior in every game or final
installer acceptance.

<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->
