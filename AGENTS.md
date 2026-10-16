# Hardware Pulse

Windows-native C#/WPF widget. `src/Native` owns the runtime; shared C# components and Panel.xaml
remain in `src`, and `assets` owns original vector artwork. Legacy top-level PowerShell source
is retained only for differential tests and is not installed. `scripts/Build.ps1` builds the
WinExe and installer; `scripts/Test-NativeSensors.ps1` checks native/legacy parsing parity.
Use `scripts/Set-Version.ps1 -ExpectedVersion <current> -Version <next>` for native version bumps;
preview with `-WhatIf` to preserve BOM/newlines and reject drift before writing. Use `scripts/Validate.ps1` before commit. Keep machine snapshots, settings, downloaded binaries,
and personal paths out of Git. Preserve the existing read-only hardware boundary.
No repository SOP release launcher is configured; normal Git synchronization is used.

Public documentation keeps English and Simplified Chinese in separate files with language links
(for example README.md / README.zh-CN.md and CHANGELOG.md / CHANGELOG.zh-CN.md).
GitHub release descriptions use separate English and 简体中文 sections; never interleave translations.
Write natural Chinese prose while preserving product names, code identifiers, paths, units and hashes.

## UI acceptance

- Use Title Case for English UI headings, labels, choices and buttons, including every word
  (for example `Save Encrypted`, `Topped Up`, `5-Hour`). Preserve official brand spelling,
  acronyms, units, endpoints, user-defined names and persisted/protocol identifiers. Keep
  explanatory sentences distinct from captions; never rewrite source data for capitalization.
- Keep compact readings concise: the desktop balance title is `DeepSeek`, not
  `DeepSeek · Available Balance`; the card caption is `Balance`, with detail in the tooltip.
- DeepSeek currency display is an explicit persisted CNY (RMB), USD or All choice, shared
  by App and Desktop. Filter the original balances; never convert currencies, fabricate a
  missing amount, or replace a selected zero with another currency's nonzero balance.
- Run the real WPF quota/UI suite and inspect English and Simplified Chinese, light/dark
  settings and cards, and narrow desktop readings before declaring UI acceptance. A build
  alone or a main-window screenshot does not verify desktop-row layout. The suite is
  `scripts/Test-QuotaFeature.ps1 -AppPath <isolated-built-app>`; keep fixtures synthetic.
