# Hardware Pulse

Windows-native WPF widget. `src` owns application code, `assets` owns original vector artwork,
`scripts/Build.ps1` builds the WinExe and installer, `src/Test-Sensors.ps1` verifies sensor parsing.
Use `scripts/Validate.ps1` before commit. Keep machine snapshots, settings, downloaded binaries,
and personal paths out of Git. Preserve the existing read-only hardware boundary.
No repository SOP release launcher is configured; normal Git synchronization is used.
