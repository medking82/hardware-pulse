# Claude quota recovery investigation

Status-line integration: decoder and Windows receiver implemented; opt-in source
selection and configuration integration remain pending. Not included in Windows 0.6.32.
User direction: pursue both independent refresh and an optional status-line source.

Decoder and local receiver admission (reclassify before configuration integration):
<!-- sop-risk-classification: {"facts":{"blast_radius":"isolated","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->

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
and discard transcript paths, workspace data, raw session identifiers and all other
fields. A SHA-256 session fingerprint is retained solely to reject other sessions;
it is not an account identifier, authentication credential or proof of caller identity.
No credential reads, HTTP requests or model turns in this receiver.
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

`src/Core/ClaudeStatusLineSnapshot.cs` now decodes only the two documented windows
from an already parsed input graph. It accepts finite numeric percentages and
integral Unix-second resets in the future, bounded by the window duration. It
retains no input graph or non-quota fields and returns detached window copies.
Each unchanged window keeps its first reception time independently, even when
another window changes. At ten minutes or its reset deadline, values are hidden.
Clock rollback also hides values. Available values are labeled `CLI snapshot`,
never `Live`; receipt is not proof of a new API observation.

Core tests cover zero/full, missing/invalid values, strict reset units and bounds,
repeat renders, partial window changes, stale deadlines and mutation isolation.
Core tests alone do not prove the receiver, source selection or real CLI behavior.

## Windows receiver implementation

`HardwarePulse.exe --claude-statusline` is a separate, windowless entry point.
`src/Native/ClaudeStatusLineReceiver.cs` accepts redirected UTF-8 stdin with a
65,536-character limit, JSON recursion limit 16, and a two-second process-side
wait. It returns fixed status text only. No input/parse exception details are
forwarded to `host-error.txt`; a still-open stdin does not keep the process alive.

The receiver persists only schema, session fingerprint, whitelisted windows and
their independent reception timestamps under the current user's Pulse state.
An exclusive file lock covers read/owner-check/replace. First reception binds the
session; different sessions and corrupt ownership state fail closed. The reader
never automatically falls back from HTTP or selects another account. Explicit
reset/rebind and source selection still need a user-facing integration.

Publication uses a same-directory temporary file and atomic replace. Interrupted
writes leave at most one reusable temporary file; readers only open the final
file, with a 4,096-character cap. Session fingerprints prevent accidental mixing,
not malicious modification by another process already running as the same user.

`scripts/ClaudeStatusLineTests.cs` exercises synthetic input, disk round trips,
repeat reception, clock rollback, oversized/deep JSON, corrupt files, simultaneous
first-session binding, writer contention and redirected WinExe child completion
(including an open idle stdin). It runs through `Test-Native.ps1`. It does not
exercise a real Claude session or prove that CLI settings invoke the command.
No user Claude settings, credentials, existing HTTP source, UI or collector state
were changed. Rollback removes the new entry point/decoder/receiver and tests;
there is no installed configuration migration to reverse.

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
