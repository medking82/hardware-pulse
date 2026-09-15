# Privacy policy

Hardware Pulse reads hardware sensors and stores settings and current snapshots locally. It does not upload sensor readings, game captures, hardware models, custom names or usage history.

Desktop Mode's optional Auto Contrast samples six nearby screen pixels at most once every two seconds while the desktop is foreground and the readout is locked. These color samples are used locally to choose light or dark text; they are not stored or transmitted.

The separate, opt-in Local adaptive text contrast captures the screen rectangle covered by the Desktop panel, including visible content from other apps. It processes those pixels in memory to color text black or white; it does not save or transmit images. Sampling is scheduled every 100 ms with at most one capture in flight, and pauses while the panel is hidden. Windows capture exclusion prevents Pulse from sampling itself; this also means screenshots, recordings and screen sharing may omit the panel while the feature is enabled. Disabling it restores capture visibility. Screenshot mode freezes the current text colors and restores capture visibility for 15 seconds, then automatically resumes local sampling and exclusion. Pulse does not save a screenshot itself. Choosing a custom Text Color disables both contrast modes.

Network readings use local adapter byte counters through LibreHardwareMonitor. Pulse does not inspect packet contents or run a speed test; adapter names and rates stay in the local snapshot.

AI Quota is independently opt-in for Codex, Antigravity and Claude. Codex and Claude
read the current user's existing login (Codex auth.json, Claude Code credential file
or Windows Credential Manager, or configured Claude OAuth environment token). Their
access token is sent only to the corresponding fixed HTTPS quota endpoint at
chatgpt.com or api.anthropic.com; the provider receives your IP and User-Agent.
Antigravity reads quota from a current-user local language server over loopback,
using its CSRF token. Antigravity must be running. Pulse does not write/renew login
credentials, copy them into settings, or include them in logs or hardware snapshots.
Quota readings stay in UI-process memory. Refresh occurs every five minutes while
enabled; disabling a provider cancels pending requests and removes its readings.
Expired credentials must be renewed in the owning application. No chat history,
token totals, billing history or purchased credit actions are read or performed.

When automatic update checks are enabled, or you choose Check for Updates, Pulse contacts the GitHub API for this repository's latest public release. GitHub receives the connection's IP address and a HardwarePulse update-check User-Agent. No machine inventory or sensor data is sent. Automatic checks and automatic downloads are opt-in and can be disabled in Settings. Downloads contact GitHub and its release-asset hosts and store the installer in `%LocalAppData%\HardwarePulse\updates`. Pulse checks the expected size and SHA-256 before offering installation; installation requires your action and Windows elevation. GitHub connections are subject to [GitHub's privacy policy](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

PresentMon is bundled for optional local FPS capture. It reads Windows graphics events for the selected process. LibreHardwareMonitor and PawnIO provide local hardware readings. Pulse does not use their data for remote analytics. There are no advertising or analytics SDKs in Pulse.

Settings live in `%LocalAppData%\HardwarePulse`; current collector snapshots live in `%ProgramData%\HardwarePulse\<UserSID>\runtime`. Uninstall retains settings and shared PawnIO. Delete retained settings only if you no longer need them.

Maintainer: [Marck Wong](https://github.com/medking82). Questions can be raised through repository Issues; do not include private snapshots or credentials in public issues.
