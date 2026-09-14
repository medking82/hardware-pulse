# Hardware Pulse

Windows-native C#/WPF widget. `src/Native` owns the runtime; shared C# components and Panel.xaml
remain in `src`, and `assets` owns original vector artwork. Legacy top-level PowerShell source
is retained only for differential tests and is not installed. `scripts/Build.ps1` builds the
WinExe and installer; `scripts/Test-NativeSensors.ps1` checks native/legacy parsing parity.
Use `scripts/Validate.ps1` before commit. Keep machine snapshots, settings, downloaded binaries,
and personal paths out of Git. Preserve the existing read-only hardware boundary.
No repository SOP release launcher is configured; normal Git synchronization is used.
