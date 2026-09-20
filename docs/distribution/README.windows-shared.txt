Hardware Pulse 0.7.0 — Windows x64

Keep the installed folder together and launch HardwarePulse.exe. The UI's .NET
runtime is included. The worker requires Windows .NET Framework 4.8.

The worker and its Framework libraries stay in worker/. Do not mix them with
the UI libraries. Tools and third-party licenses are included in tools/ and
licenses/. The installer handles PawnIO and startup registration. FPS requires
the matching installed collector; copying this folder alone is not a supported installation.

The installer preserves existing Hardware Pulse preferences. Current signing
status is in SIGNING.md; the installer does not have public certificate trust.
