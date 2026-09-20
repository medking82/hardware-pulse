# Claude Code status-line source (experimental)

[简体中文](CLAUDE-STATUSLINE.zh-CN.md) · [Back to README](../README.md)

Available in Windows Pulse 0.6.33 or later. **Existing login remains the recommended
default.** This optional source reads observations from an active Claude Code
session; it does not renew credentials or refresh quota after the CLI closes.
Synthetic receiver and shell tests pass. Delivery from a real Claude Code session
has not yet completed acceptance testing.

## Set up

1. In Pulse, open **Settings → AI Quota**, enable Claude, and choose the experimental
   **CLI snapshot** source. Use **Copy status-line command** and choose Git Bash if
   Claude Code uses Git Bash, or PowerShell when Git Bash is not installed. The
   copied command uses your current Pulse executable path.
2. Back up your Claude Code user `settings.json` before editing. Its default Windows
   location is `%USERPROFILE%\.claude\settings.json`; if you use `CLAUDE_CONFIG_DIR`,
   use that configuration directory instead. Keep all unrelated settings. If a
   `statusLine` is already configured, preserve its entire value: the simple setup
   below replaces that status line and does not combine commands.
3. Set `statusLine.type` to `command` and `statusLine.command` to the copied command.
   These examples are complete JSON objects only for an otherwise empty settings
   file. In an existing file, merge the `statusLine` property rather than replacing
   the whole document. Replace the example executable path with your copied path.

Git Bash example:

```json
{
  "statusLine": {
    "type": "command",
    "command": "'C:/Program Files/HardwarePulse/HardwarePulse.exe' --claude-statusline"
  }
}
```

PowerShell example:

```json
{
  "statusLine": {
    "type": "command",
    "command": "& 'C:/Program Files/HardwarePulse/HardwarePulse.exe' --claude-statusline"
  }
}
```

The clipboard contains a **shell command, not JSON**. Preserve its quoting. When
placing it inside a JSON string, escape any double quote as `\"` and any backslash
as `\\`. This matters particularly for Git Bash commands containing apostrophes
in the executable path. Do not paste an additional pair of outer JSON quotes into
the command itself. Pulse does not edit Claude Code configuration for you.

4. Keep one intended Claude Code session active. Let its next normal API response
   supply quota data; there is no need to generate a paid turn just to test this
   integration. Pulse should display **CLI snapshot**, with available quota windows,
   on its next local poll (up to approximately 30 seconds).

## Interpret the result

- Only supplied 5-hour and weekly windows are shown. Missing data never means 100%
  remaining. Claude account/plan eligibility and CLI data availability still apply.
- An unchanged observation expires after ten minutes, or at its window reset.
  Re-running the same status-line input does not make an old observation fresh.
- The first accepted CLI session is bound locally. For a new session, use Pulse's
  rebind control and then allow the intended session to send its next observation.
  Other active sessions can send first; close unwanted sessions before rebinding.
  This binding is not proof of account identity.
- If nothing arrives, check the selected shell, executable path and JSON syntax.
  Project or managed Claude settings may override the user setting. Do not bypass
  managed restrictions. After moving or reinstalling Pulse to another directory,
  copy its command again and update the configured path.

The receiver stores bounded quota observations and a session fingerprint under
`%LOCALAPPDATA%\HardwarePulse\claude-statusline`. It does not retain the raw status-line
input, transcript path, prompt, or credentials. It makes no network requests.

## Roll back

In Claude settings, restore the previous `statusLine` value. If none existed,
remove only the Pulse `statusLine` property, retaining valid JSON and all other
settings. Then select **Existing login** in Pulse. Selecting Existing login alone
does not remove the configured Claude command. Do not delete your Claude credential
file or log out to undo this integration.

Contract: [Claude Code status-line documentation](https://code.claude.com/docs/en/statusline).
Implementation and test limits: [quota recovery investigation](CLAUDE-QUOTA-RECOVERY.md).
