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
