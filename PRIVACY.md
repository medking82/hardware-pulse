# Privacy policy

Hardware Pulse reads hardware sensors and stores settings and current snapshots locally. It does not upload sensor readings, game captures, hardware models, custom names or usage history.

The following update-check and FPS features are in the unreleased 0.4.0 source; published 0.3.1 does not include them.

When automatic update checks are enabled, or you choose Check for Updates, Pulse contacts the GitHub API for this repository's latest public release. GitHub receives the connection's IP address and a HardwarePulse update-check User-Agent. No machine inventory or sensor data is sent. Automatic checks are opt-in and can be disabled in Settings. Opening GitHub or a release download uses your browser and is subject to [GitHub's privacy policy](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

PresentMon is bundled for optional local FPS capture. It reads Windows graphics events for the selected process. LibreHardwareMonitor and PawnIO provide local hardware readings. Pulse does not use their data for remote analytics. There are no advertising or analytics SDKs in Pulse.

Settings live in `%LocalAppData%\HardwarePulse`; current collector snapshots live in `%ProgramData%\HardwarePulse\<UserSID>\runtime`. Uninstall retains settings and shared PawnIO. Delete retained settings only if you no longer need them.

Maintainer: [Marck Wong](https://github.com/medking82). Questions can be raised through repository Issues; do not include private snapshots or credentials in public issues.
