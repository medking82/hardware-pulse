# Claude quota recovery investigation

Status-line integration: decoder, Windows receiver and explicit source selection
implemented. Configuration automation and real CLI acceptance remain pending.
Available as an opt-in experimental source in Windows 0.6.33; existing login remains the default.
User direction: pursue both independent refresh and an optional status-line source.

Decoder, receiver and source-selector admission (reclassify before automatic CLI configuration):
<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"ordinary","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"unchanged","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"not_required","kind":"risk-classification-assessment","reasons":{"formal_review":["routine_no_review"],"risk":["no_high_risk_signal"]},"risk":"routine","schema_version":2} -->

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
reset/rebind and source selection are exposed in AI Quota Settings.

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

## Source selection and scoped audit

Inspection baseline: `510a2b2`; the source-selector changes are the subsequent
working-tree delta. Scope: QuotaSession, QuotaView/Shell, receiver/decoder, quota
Settings and tests. `docs/RUNTIME-BOUNDARIES.md` and `docs/QUOTA-INTEGRATION.md`
control this boundary; no root CONTEXT/CONTEXT-MAP, WAYFINDER or FRAMEWORK.md was
found. No cross-platform UI redesign is inferred from shared Core ownership.

The job is dependable remaining-quota visibility without repeated manual login.
Product audit mode: prototype maturity and implementation architecture. Passing
synthetic tests does not prove real credential-expiry recovery or independent
refresh with every Claude client closed.

- Healthy boundary: QuotaSession owns cancellation, versioned result acceptance,
  cadence and Retry-After. Adapters own credentials/transport. Switching the
  explicit Claude source uses disable/enable invalidation; a previous worker's
  completion cannot repopulate the newly selected source. Existing login is the
  default, and snapshot mode never falls back to HTTP or changes credential state.
- Freshness knowledge must reach both views: receiver values expire independently,
  while Monitor/Desktop may hold a previously read result. Both now gate snapshot
  values by age and reset deadline; render signatures include window values and
  validity, so a partial-window change cannot remain hidden by an unchanged oldest
  timestamp. Synthetic WPF tests cover fresh, stale and reset-window presentation.
- Local and remote cadence differ: snapshot reads use the existing worker scheduler
  every 30 seconds, with no extra provider request; HTTP success keeps five minutes.
  A focused Core test asserts this deadline and existing lifecycle tests protect
  cancellation/non-overlap. There is no new timer or background service.
- Product gap: configuring a status line and rebinding a new session add work for
  users. The optional selector and copy-command action make the current boundary
  explicit but do not meet the independent-refresh requirement. Keep this as a
  supplementary source; next validate real CLI delivery and design reversible
  configuration that preserves an existing statusLine command. Do not claim a
  long-term stability improvement from a synthetic snapshot alone.

Clarified decisions: preserve WPF glass/layout and existing login by default;
snapshot is an explicit alternative, not an authentication fallback; rebind is
an explicit action that admits the next session. Unknown: real status-line invocation
and independent renewal across expiry. Non-goals: login automation, token rotation,
account switching, replacing an existing CLI command without preservation, and
any hardware/collector or shared-UI redesign.

Validation: `Validate.ps1`, `Test-CoreModern.ps1`, and narrow Settings screenshots
from `Test-SettingsQuota.ps1` cover the code boundary. Screenshot inspection confirms
the source controls and footer remain reachable at 340 DIP with no horizontal
overflow. Live configuration and real CLI acceptance remain separate gates.

## Windows command compatibility

The official status-line Windows contract selects Git Bash when installed and
PowerShell otherwise. A quoted EXE path followed by arguments works as a Bash
command but is a string expression, not an invocation, in PowerShell. The initial
copy-command implementation therefore failed on the PowerShell route. Double
quotes also allowed Bash expansion inside an executable path containing `$`.

`ClaudeStatusLineCommand.Create` now builds literal-path commands separately for
the two shells: Bash single-quote escaping, or PowerShell's call operator with
single-quote escaping. The copy action presents both documented shell routes
without changing the Settings layout. It does not guess from Pulse's environment
which shell an independently launched Claude session uses.

`ClaudeStatusLineTests` reproduces failure of the original command in actual
Windows PowerShell, then verifies the new commands in actual Windows PowerShell
and Git Bash, using a windowless receiver fixture, redirected JSON stdin and an
executable path containing spaces, `$` and an apostrophe. Only synthetic input and
isolated state are used. Git Bash reports an explicit skip if unavailable; both
shells were available and passed on this workstation. This proves shell invocation
and pipe delivery, not a real Claude-produced quota observation or token renewal.

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

Follow-up reproduction found that a click after a completed 429 still reset the
scheduler deadline when Retry-After was missing, expired or shorter than two
minutes. Manual refresh now preserves the effective rate-limit deadline, including
the local two-minute floor. Synthetic tests first failed with no Retry-After, then
passed for missing, expired, short and long server deadlines; repeated clicks
produce exactly one retry at the effective deadline. Successful and authentication
failure refreshes retain their existing immediate manual recovery behavior. This
is scheduler-only work: no credential, endpoint, account or UI changes.

## Windows login-file sharing

A synthetic credential-shaped file held by a writer with ReadWrite/Delete sharing
reproduced an IOException in the Windows adapter's File.ReadAllText call. Its read
sharing denied the already-open write handle even though the writer allowed reads.
This can turn an overlapping file update into a transient quota failure. It is not
proof that every reported authentication failure had this cause.

The existing ReadLogin boundary now opens read-only with ReadWrite/Delete sharing,
enforces the one-MiB limit against the open stream and every copied chunk, and
preserves BOM decoding. Each refresh opens the selected path anew; an atomic file
replacement becomes visible on the next read. Missing files retain Login required,
oversized files retain Login unavailable, and malformed partial JSON is rejected.
The adapter neither writes nor renews credentials and does not add an account or
fallback source. ReadLogin serves both Codex and Claude on Windows.

QuotaLoginFileTests covers compatible writer handles, atomic replacement, UTF-8
and UTF-16 BOMs, oversized/missing files and partial JSON using isolated synthetic
data. No real credential values or files are used. The pre-fix sharing test failed;
the corrected adapter passed. This does not establish provider token-expiry recovery.

## Owner-rotated token recovery (Windows 0.6.34)

Local token-monitor source at commit `1e2c03d2a55b5eef97c7732341281415c2a3d7ea`
uses direct OAuth refresh and credential persistence on Windows; its macOS path
instead delegates a best-effort `/status` probe to Claude Code. Those are different
ownership contracts, not evidence that Pulse's read-only adapter already renews a
login. The official CLI reference documents auth login/logout/status, but no
standalone auth-refresh command. The authentication guide says an expired login
that cannot refresh requires `/login`. Do not promise permanent unattended renewal.

A separate race is reproducible: the selected credential owner can replace an
access token while a quota request using the previous token returns HTTP 401.
Previously Pulse reported Login required without checking for that replacement.
`ClaudeQuotaRequest` now rereads the same selected source after the first 401 and
retries once only when the token has changed and passes validation. An explicit
Windows environment token remains pinned for the read, with no file fallback.
No token endpoint, refresh-token read, credential write or CLI launch was added.
Unchanged tokens, 403, 429 and transport failures do not cause request retries;
the second rejection propagates. Cancellation is checked before credential access
and each request. The modern client retains one ten-second budget for the whole
operation; Windows retains its per-request bounds and host cancellation budget.

Both Windows and modern Claude transports use this shared request boundary.
The modern HTTP-handler fixture failed before the fix and passed afterwards.
Framework and .NET 10 tests cover changed/unchanged/invalid replacement tokens,
exact retry counts, non-authentication errors and cancellation. This handles an
owner update already in progress; it cannot manufacture a fresh token when the
owner is closed and the login has expired.

Sources: [CLI reference](https://code.claude.com/docs/en/cli-reference),
[authentication](https://code.claude.com/docs/en/authentication#renew-an-expiring-login),
[token-monitor source](https://github.com/medking82/token-monitor/blob/1e2c03d2a55b5eef97c7732341281415c2a3d7ea/src/shared/limitCollector.js).

### Pin the source for a recovery attempt

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"sensitive","destructive":"no","failure_cost":"low","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"changed","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"low","verification":"deterministic"},"formal_review":"required","kind":"risk-classification-assessment","reasons":{"formal_review":["high_risk_requires_review"],"risk":["privilege_boundary_change"]},"risk":"high","schema_version":2} -->

Windows previously rediscovered the source on every credential read. A synthetic
configuration-path change between the original read and retry reproduced source
drift. The reader now selects one file path or one Credential Manager target for
the entire request/retry pair. A selected file disappearing fails with Login
required instead of consulting another file or store. A subsequent independent
refresh may discover the current configuration as before. Explicit environment
tokens stay pinned and never fall back to a file.

The file regression uses only isolated synthetic data and a process-local
CLAUDE_CONFIG_DIR, restored in finally. It proves path pinning and disappearance
handling without accessing real Credential Manager entries. The native store
reader retains the existing bounded decoding and memory cleanup; its selected
target is captured for rereads. Live store mutation is not part of these tests.

This changes the credential-selection boundary and requires one independent review
after deterministic checks, before commit/release.
