# Privacy policy

Hardware Pulse reads hardware sensors and stores settings and current snapshots locally. It does not upload sensor readings, game captures, hardware models, custom names or usage history.

Desktop Mode's optional Auto Contrast samples six nearby screen pixels at most once every two seconds while the desktop is foreground and the readout is locked. These color samples are used locally to choose light or dark text; they are not stored or transmitted. Disable Auto Contrast to use a custom text color without background sampling.

Network readings use local adapter byte counters through LibreHardwareMonitor. Pulse does not inspect packet contents or run a speed test; adapter names and rates stay in the local snapshot.

When automatic update checks are enabled, or you choose Check for Updates, Pulse contacts the GitHub API for this repository's latest public release. GitHub receives the connection's IP address and a HardwarePulse update-check User-Agent. No machine inventory or sensor data is sent. Automatic checks and automatic downloads are opt-in and can be disabled in Settings. Downloads contact GitHub and its release-asset hosts and store the installer in `%LocalAppData%\HardwarePulse\updates`. Pulse checks the expected size and SHA-256 before offering installation; installation requires your action and Windows elevation. GitHub connections are subject to [GitHub's privacy policy](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

PresentMon is bundled for optional local FPS capture. It reads Windows graphics events for the selected process. LibreHardwareMonitor and PawnIO provide local hardware readings. Pulse does not use their data for remote analytics. There are no advertising or analytics SDKs in Pulse.

Settings live in `%LocalAppData%\HardwarePulse`; current collector snapshots live in `%ProgramData%\HardwarePulse\<UserSID>\runtime`. Uninstall retains settings and shared PawnIO. Delete retained settings only if you no longer need them.

Maintainer: [Marck Wong](https://github.com/medking82). Questions can be raised through repository Issues; do not include private snapshots or credentials in public issues.
