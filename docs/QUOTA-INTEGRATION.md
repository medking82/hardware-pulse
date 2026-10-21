# Independent quota readings

## Native Jev Balance

Jev is a USD wallet, not a percentage quota. Settings → AI Quota → Jev Session
contains a masked Cookie editor, encrypted save/remove, PC Chrome DevTools steps
and an explicit Open TypeSafe button (Chrome preferred, Edge fallback). Browser
metadata is generated from the installed version; the optional override is folded.
Saving never enables monitoring; enabling without a saved session opens guidance
without consuming a network attempt. Session replacement/removal invalidates old
observations and in-flight completions without forgiving existing cooldown debt.

The adapter launches the installed Chromium browser (112+) with private anonymous
inherited pipes and an isolated current-user profile/context. It attaches only to
its own process, not an existing browser. A local empty document establishes the
fixed origin. The process starts suspended with an explicit inherited-handle list,
is assigned to a private kill-on-close Job Object, then resumes; child creation
cannot race assignment, and shutdown/cancellation contains the whole owned tree.
Only the two CDP client pipe handles are inherited from Pulse. That document uses the
fixed HTTPS origin. Request interception admits that document and exactly one
balance POST, injecting the Cookie only into that POST; foreign requests and
redirects are blocked. No Electron, Node or browser bundle is shipped. A 25-second
owned process deadline bounds startup, protocol, fetch and cleanup. Extra-info
events correlate opaque manual redirects/429 with the originating request ID.

The deployed Next.js action ID is pinned to the known read-only balance function.
It is not a public API contract: an upstream action change fails closed and may
require a Pulse update. No automatic action discovery or account-writing fallback
is attempted. Only one unambiguous successful Flight result with finite integer
`data.balanceCents` is accepted. Zero and negative USD balances are genuine values.
HTTP 403 alone does not imply expired login; known login redirects, challenges,
rate limits and changed actions remain distinguishable.

Core decoding/presentation is portable. The separate shared Desktop preview has
no Cookie editor and continues rejecting Jev before credential reads; it never
falls through to a different provider. Native card and Desktop share the catalog,
wallet visibility/order key and theme tint. DeepSeek's currency filter cannot hide
or alter Jev's USD. The original Pulse Jev monogram is not an official TypeSafe mark.
Synthetic protocol/DPAPI/WPF tests are separate from authenticated consumer
acceptance; a normal HTTP 307 invalid-session control is not balance acceptance.

## Shared Antigravity (P02)

The shared host composes readers in `DesktopQuotaReaders`; quota panels receive
a reader and do not select credential implementations. Windows builds reuse the
existing `AntigravityQuota` and `QuotaProviders` source, including current-user
process ownership, PID-owned ports, fixed endpoints, redirect rejection and
bounded transport. The modern JSON bridge uses the same bounded `QuotaJson`
graph as the other modern quota clients. No second Antigravity collector or
floating-window reader is introduced.

`QuotaSession` remains the refresh/cancellation/late-result owner. Antigravity has
its own opt-in in the separate preview profile. Monitor publishes the same
reading to floating Desktop, including reopen and opt-out. Missing local server
is explicit; no values are fabricated. macOS/Linux acquisition is unavailable:
Windows WMI, SID and TCP ownership discovery has no verified adapter there.
The presentation and decoder remain portable; this does not claim portable
Antigravity source discovery.

Synthetic tests cover modern JSON bounds, fractional and zero quota, canceled
reads, independent persistence, missing source, floating propagation/reopen and
provider-specific clearing. Core late-result/no-overlap tests cover each provider.
Tests never read authentication stores. Live account compatibility is separate
from synthetic acceptance.

Local P02 verification: `Validate.ps1 -ModernCore`, Desktop headless regression,
native Windows session checks and extracted self-contained win-x64 package smoke
passed. Rendered Antigravity floating rows were inspected. Local validation
packages retain the preview version and dirty-source marker; they are not stable
release assets. No installed application or authentication store was modified.

## Existing Windows contract

Scope: opt-in Codex, Claude and Antigravity remaining quota and reset times in
Monitor and Desktop View. No token history, spending, chat contents or account management.

Owner: the unprivileged Shell owns a separate asynchronous QuotaSession. Hardware
Collector, ReadingSession, FPS and startup tasks retain their existing contracts.
Provider adapters return only sanitized quota windows and finite-state errors. No
credentials, account emails or HTTP response bodies enter snapshots, settings or logs.

Authentication: read existing current-user Codex/Claude login on each refresh;
never write/rotate credentials or launch a login flow. Native Claude CLI Query
and Auto may invoke the installed Claude Code quota-only `/usage` command with
customizations/tools disabled, stdin closed and a bounded deadline. Existing
Login and CLI Snapshot remain separate manual choices. Expired or revoked login
may require the owning application to renew it. Antigravity requires its local language server;
verify current-user process ownership and PID-owned listening ports before sending
its CSRF token to loopback. No Token Monitor process or PowerShell/Node runtime.
Only fixed provider HTTPS endpoints; disable redirects. Loopback TLS exceptions
are request scoped. No global certificate bypass. Providers are independently opt-in.

Refresh: background, at most one refresh per provider; five-minute cadence with
bounded requests, no per-frame API calls. Disable/dispose cancels requests and
rejects late results. Clear old windows on failures/account transitions; no fabricated
100% or reset-to-full inference. Show observation age and expired readings explicitly.

Native Monitor card and Settings Refresh actions force an immediate attempt for
each selected, enabled provider, bypassing Pulse's cadence, saved reservation and
retry deadline. Card Refresh stays enabled while idle and disables during a request,
including a timed-out worker that has not stopped. Repeated clicks during a request
do not overlap or queue another attempt. Forced requests retain failure counters;
their outcome sets the next automatic retry, including any new server Retry-After.
An explicit force also rechecks API authentication after Claude Auto has stopped.

Allowed surfaces: quota adapters/model/session, Shell wiring, XAML/settings,
Desktop quota rows, localization, attribution, focused regression tests and docs.
No changes to installed authentication stores, elevation or hardware acquisition.
Observed live Codex TLS handshake fails when pinned to TLS 1.2 but succeeds with
TLS 1.3 available. Quota and UpdateCheck therefore enable TLS 1.2 and TLS 1.3,
so updater refresh cannot re-pin a process-wide TLS 1.2-only policy. Windows
chooses an available protocol. System-default mode also failed in the live probe.
Rollback is removal of the opt-in feature; existing settings remain compatible.

Native Claude Auto is opt-in (`claudeAutoSource`); upgrades retain existing source
selections. The session starts with API and queues CLI only after an explicit API
HTTP 401. It never queries both in one refresh: wait for the shared deadline, then
read CLI. CLI success is final for that cycle; the next normal cycle returns to
API. A 429 from either stream retains the longer server deadline and never switches
routes. Transport, permission and unclassified errors are not login failures.
After API and CLI both reject authentication, automatic retries stop until the
user signs in through Claude Code and requests Refresh. Source
toggles do not clear that cooldown. Schedule schema 2 preserves recovery stage
and stop state; schema 1 loads with API as its initial stage and unchanged debt.
Only allowlisted source/status/error metadata enters diagnostics, never raw CLI
output or credential contents. The shared preview host retains its existing
manual API reader and does not silently opt into the native Auto feature.

Before `/usage`, the native CLI reader checks the bounded local `auth status --json`
report. An explicit `loggedIn: false` returns `Login required`, including when the
CLI exits with code 1; malformed or oversized auth reports return `Login unavailable`.
No account identity or raw report is retained. Claude Code 2.1.304 on this Windows
host returned only session cost/token statistics for redirected `/usage`, even with
exit code 0. Those statistics are not subscription quota. Use Existing Login when
its API check succeeds; a signed-in Claude app does not establish CLI authentication.

Acceptance: parser fixtures for all three providers including missing, zero,
malformed and additional windows; cancellation/disable/error lifecycle tests;
isolated WPF rendering/settings/Desktop tests; full scripts/Validate.ps1. Real
provider checks are separate from fixtures and must report unavailable logins.
High-risk frozen diff requires Native Review after deterministic checks.

## Shared Claude adapter in progress

The shared host increment reuses Core `QuotaDecoder` and `QuotaSession`; platform
login sources supply an access token to `ClaudeQuotaClient`. It sends only a GET
to the fixed Anthropic usage endpoint, never follows redirects, limits response
JSON to 1 MiB/depth 32 and sanitizes failures. `QuotaJson` is shared with Codex;
existing Codex transport fixtures remain part of regression validation.

`ClaudeFileLogin` reads the existing configured file without changes or token
renewal. `MacClaudeLogin` gives an explicit environment token precedence, then
queries one Claude Keychain service/account. Only item-not-found permits the
file fallback; denied/locked access does not silently change source. The custom
config-directory service hash is isolated from the default service and still
requires real-device verification. There is no Keychain enumeration.

Legacy Keychain UI suppression uses `SecKeychainGetUserInteractionAllowed` and
`SecKeychainSetUserInteractionAllowed` under one process-local lock, restores
the observed state in `finally`, and releases returned native data. The native
call itself is synchronous; cancellation is checked before and after it, not
claimed to interrupt Apple's API. No stored ACLs or OS security settings change.
The copied credential buffer is cleared after parsing; managed token strings
remain transient and are not persisted or logged.

Sources: [Claude credential storage](https://code.claude.com/docs/en/authentication),
[Apple legacy API declarations](https://github.com/apple-oss-distributions/Security/blob/main/OSX/libsecurity_keychain/lib/SecKeychain.h),
[Apple per-query authentication limitations](https://github.com/apple-oss-distributions/Security/blob/main/keychain/headers/SecItem.h),
[config-directory service naming evidence](https://github.com/alexey-pelykh/sessiometer/issues/100).

The shared Monitor and floating monitor project independent Codex and Claude
sessions. Settings groups their opt-ins under AI Quota; existing settings leave
Claude disabled. Disabling a provider clears only its own rows. No credential
access occurs while disabled, and no token is stored in settings.

Current verification uses synthetic credentials: precedence, exact lookup,
bounds, cancellation, cleanup, HTTP errors and Codex regression passed. Shared
UI regression covers independent opt-ins, complete windows, persistence and
floating rows. The narrow settings render was inspected, localization coverage
passed, and scripts/Validate.ps1 passed. A macOS SDK ABI check is prepared
without reading credentials. Native Windows session checks also passed. Actual
macOS credential compatibility and native CI remain unverified; fixtures are
not evidence of successful access to a real user's Keychain.

Native Review completed on the frozen increment (packet `f06e6bb0`, linked to
failed Gemini packet `deed9e1f`). The P2 missing-artwork hypothesis was rejected:
`assets/claude.svg` is already tracked in HEAD. The P3 restore-cleanup finding was
accepted: a failing interaction-state restore could replace cancellation and
abandon a managed credential copy. A failing synthetic cancellation/restore
fixture reproduced it; the correction preserves the original exception, clears
copies on exceptional exits and refuses credentials when restore fails. Adapter
and Desktop regression passed after this bounded correction. No second model
round was run; the correction does not change the credential trust boundary.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"sensitive","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"changed","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"material","verification":"deterministic"},"formal_review":"required","kind":"risk-classification-assessment","reasons":{"formal_review":["high_risk_requires_review"],"risk":["privilege_boundary_change"]},"risk":"high","schema_version":2} -->
