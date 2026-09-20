# Claude quota recovery investigation

Status-line integration: design only; not included in Windows 0.6.32.
User direction: pursue both independent refresh and an optional status-line source.

## Verified integration option

The official [status line contract](https://code.claude.com/docs/en/statusline)
documents `rate_limits.five_hour` and `rate_limits.seven_day`, with
`used_percentage` and Unix-second `resets_at`. The documented example requires
Claude Code 2.1.251 or later. Windows CLI 2.1.278 is installed on the test machine.
Fields become available after an API response; windows can be absent and disappear
after reset. Absence must never mean zero usage or full remaining quota.

The installed `claude auth --help` lists login/logout/status, not refresh. The
[authentication documentation](https://code.claude.com/docs/en/authentication)
does not promise that status refreshes credentials; a setup token is documented
for model requests. Neither is a proven headless quota-renewal replacement.

## Proposed bounded implementation

Use a native opt-in status-line receiver as a supplementary observation source.
Accept bounded JSON on stdin, retain only validated quota windows and timestamps,
and discard transcript paths, workspace data, session identifiers and all other
fields. No credential reads, HTTP requests or model turns in this receiver.
Do not retain raw input in diagnostic logs. Write a small atomic local observation.

An observation must identify its source and age. Repeated status-line rendering
must not make old quota appear newly observed. Missing/reset windows and inactive
CLI sessions require explicit stale/unavailable behavior. A local observation
cannot silently replace the account selected by an explicit token override.

Admission findings: documented permission-mode changes and optional timer events
can rerun the command without a new quota response. The quota fields have no
per-observation timestamp or account identity. Reception time alone cannot justify
`Live`, and two sessions must not silently overwrite each other's selected source.
Do not use this integration as an automatic HTTP-authentication fallback.

Preserve any existing user status-line command; do not overwrite or execute it
as part of discovery. Configuration integration needs an explicit enable action,
clear ownership, and restoration of prior configuration when disabled.

Acceptance: synthetic zero/full/missing/expired/invalid windows, strict input and
output bounds, privacy whitelist, atomic publication, concurrent sessions, source
selection and stale behavior. Then verify one real CLI-produced observation without
generating an extra model turn solely to populate quota.

## Remaining requirements

This is not a complete solution for quota updates while every Claude client is
closed, nor proof of automatic renewal across credential expiry. Keep those
requirements open. Existing HTTP reading remains available; do not rotate Claude
Code's refresh token or substitute another account to mask an authentication error.

## Independent refresh: rate-limit recovery

The existing transports discarded HTTP Retry-After and the scheduler retried 429
after a fixed two minutes. A synthetic ten-minute deadline reproduced this early
retry. The bounded repair carries an optional retry timestamp through safe quota
failure metadata and honors it for automatic and manual refresh. Missing/invalid
headers retain the existing two-minute backoff. Credential selection, endpoints,
UI and ordinary five-minute successful refresh are unchanged. This improves
rate-limit behavior; it does not renew expired credentials.
