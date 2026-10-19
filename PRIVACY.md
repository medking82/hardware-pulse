# Privacy policy

Hardware Pulse reads hardware sensors and stores settings and current snapshots locally. It does not upload sensor readings, game captures, hardware models, custom names or usage history.

Desktop Mode's optional Auto Contrast samples six nearby screen pixels at most once every two seconds while the desktop is foreground and the readout is locked. These color samples are used locally to choose light or dark text; they are not stored or transmitted.

The separate, opt-in Local adaptive text contrast captures the screen rectangle covered by the Desktop panel, including visible content from other apps. It processes those pixels in memory to color text black or white; it does not save or transmit images. Sampling is scheduled every 100 ms with at most one capture in flight, and pauses while the panel is hidden. Windows capture exclusion prevents Pulse from sampling itself; this also means screenshots, recordings and screen sharing may omit the panel while the feature is enabled. Disabling it restores capture visibility. Screenshot mode freezes the current text colors and restores capture visibility for 15 seconds, then automatically resumes local sampling and exclusion. Pulse does not save a screenshot itself. Choosing a custom Text Color disables both contrast modes.

Network readings use local adapter byte counters through LibreHardwareMonitor. Pulse does not inspect packet contents or run a speed test; adapter names and rates stay in the local snapshot.

AI Quota is independently opt-in for Codex, Antigravity, Claude, DeepSeek and Jev. Codex and Claude
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

DeepSeek uses an API key you enter in its masked Settings field. Pulse encrypts this
key with Windows current-user DPAPI and stores it only under
`%LocalAppData%\HardwarePulse\credentials\DeepSeek.dpapi`; it is not copied into
settings, snapshots, diagnostics or logs. The key is sent only to the fixed HTTPS
endpoint `https://api.deepseek.com/user/balance`, with redirects disabled. DeepSeek
receives your IP address and User-Agent and returns wallet balances; Pulse performs
no model inference or purchase. Saving a key does not enable monitoring. Replacing
or removing it clears old account readings; local removal does not revoke the key
at the provider. An API key may permit paid model calls outside Pulse. DPAPI protects
the stored file, not a compromised Windows user account or a running process.

Jev uses a TypeSafe Cookie value entered into its masked Settings field, not a model
API key. The Cookie and browser User-Agent are protected together with current-user
Windows DPAPI in `%LocalAppData%\HardwarePulse\credentials\Jev.dpapi`. Saving does
not enable monitoring. Pulse launches an isolated installed Chrome or Edge (112+)
with private inherited debugging pipes, not a listening debugging port or your
normal browser profile. The generated User-Agent uses public installed-version
metadata; no browser credential database is read. One fixed read-only balance
action goes to `https://console.typesafe.ai/usage`; redirects and other page
requests are blocked. The credential is sent through process memory and private
pipes, never command-line arguments or plaintext files. The temporary browser
profile is removed after the owned browser exits. TypeSafe receives your IP,
Cookie and browser headers. No inference, recharge or account changes are performed.
Removing the local session does not revoke it at TypeSafe; sign out there to
invalidate it. A Cookie may grant broader account access outside Pulse. DPAPI and
private pipes do not protect a compromised Windows account or process.

Quota recovery diagnostics stay in the local settings directory as
`quota-diagnostics.jsonl` and one rotated copy, each limited to 32 KiB. These files
contain only provider/source labels, safe status categories, HTTP status codes,
attempt timestamps, duration and retry deadlines. They contain no credentials,
account identifiers, command lines, response bodies or quota values and are not
uploaded or included in Export Diagnostics. A temporary Claude display cache uses
an in-memory, salted source-revision key derived only from file/store metadata;
neither the key nor cached values are written to these logs. Repeated failures use
backoff; the normal successful polling interval remains five minutes.

When automatic update checks are enabled, or you choose Check for Updates, Pulse contacts the GitHub API for this repository's latest public release. GitHub receives the connection's IP address and a HardwarePulse update-check User-Agent. No machine inventory or sensor data is sent. Automatic checks and automatic downloads are opt-in and can be disabled in Settings. Downloads contact GitHub and its release-asset hosts and store the installer in `%LocalAppData%\HardwarePulse\updates`. Pulse checks the expected size and SHA-256 before offering installation; installation requires your action and Windows elevation. GitHub connections are subject to [GitHub's privacy policy](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).

PresentMon is bundled for optional local FPS capture. It reads Windows graphics events for the selected process. LibreHardwareMonitor and PawnIO provide local hardware readings. Pulse does not use their data for remote analytics. There are no advertising or analytics SDKs in Pulse.

Settings live in `%LocalAppData%\HardwarePulse`; current collector snapshots live in `%ProgramData%\HardwarePulse\<UserSID>\runtime`. Uninstall retains settings and shared PawnIO. Delete retained settings only if you no longer need them.

Maintainer: [Marck Wong](https://github.com/medking82). Questions can be raised through repository Issues; do not include private snapshots or credentials in public issues.

Export Diagnostics in Settings → General saves a JSON report only to a location
you select. It includes App/OS versions, manufacturer/model, motherboard name,
non-network hardware sensor names/IDs/readings and fan mapping results. Missing or
invalid collector data is recorded as a status. It does not export account
credentials, quota data, settings, raw logs, user/computer names, user SIDs, network
adapter identifiers, screenshots or device serial-number fields. Nothing is
uploaded automatically. Review the report before choosing to share it; hardware
models and readings describe your device.
