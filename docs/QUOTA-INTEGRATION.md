# Independent quota readings

Scope: opt-in Codex, Claude and Antigravity remaining quota and reset times in
Monitor and Desktop View. No token history, spending, chat contents or account management.

Owner: the unprivileged Shell owns a separate asynchronous QuotaSession. Hardware
Collector, ReadingSession, FPS and startup tasks retain their existing contracts.
Provider adapters return only sanitized quota windows and finite-state errors. No
credentials, account emails or HTTP response bodies enter snapshots, settings or logs.

Authentication: read existing current-user Codex/Claude login on each refresh;
Pulse never writes/rotates credentials or launches a login. Claude still requires
the owning application to renew an expired token. Codex authentication failures
may recover through the installed official Windows CLI as described below.
Antigravity requires its local language server;
verify current-user process ownership and PID-owned listening ports before sending
its CSRF token to loopback. No Token Monitor process or PowerShell/Node runtime.
Only fixed provider HTTPS endpoints; disable redirects. Loopback TLS exceptions
are request scoped. No global certificate bypass. Providers are independently opt-in.

Refresh: background, at most one refresh per provider; five-minute cadence with
bounded requests, no per-frame API calls. Disable/dispose cancels requests and
rejects late results. Clear old windows on failures/account transitions; no fabricated
100% or reset-to-full inference. Show observation age and expired readings explicitly.

HTTP status presentation distinguishes missing/rejected authentication (401,
`Login required`) from forbidden access (403, `Quota access denied`). A 403 does
not prove the login expired. Both retain the normal five-minute retry cadence;
429 keeps the two-minute backoff and transient failures retry after 30 seconds.
This classification does not refresh tokens, change endpoints or grant access.

## Codex managed-client recovery (implementation in progress)

After `Login required` only, Windows may use the current user's installed
`Programs/OpenAI/Codex/bin/codex.exe` under LocalAppData. No PATH, shell shim,
download or additional bundled runtime is used. Ordinary successful HTTP reads,
network errors, forbidden access and rate limiting do not launch the CLI.
The CLI inherits the same CODEX_HOME as the existing reader; Pulse passes no
token or account identifier. The official client owns any managed-token renewal.

The process runs without a console or elevation, with fixed read-only/untrusted
CLI options and stdio transport. The only protocol messages are `initialize`,
`initialized` and `account/rateLimits/read`; no thread, turn, login, logout,
reset-credit or purchase method is permitted. Responses flow through the existing
QuotaDecoder. Raw stdout/stderr and account information are never logged or saved.
Output is bounded; a 15-second deadline and cancellation terminate only the
owned child process. A failed recovery preserves the original sanitized status.
The ordinary five-minute authentication retry cadence remains in QuotaSession.

Acceptance before delivery: protocol and malformed/oversize/error fixtures,
owned-process exit/cancellation fixtures, native WPF and modern adapter builds,
full validation, a sanitized live quota probe, and one independent review of the
frozen credential/process-boundary change. The local probe establishes working
quota retrieval, not that an expired-token scenario was reproduced. Reverting
this recovery path restores direct HTTP behavior; no credential rollback is done.

Reference: [official app-server protocol](https://learn.chatgpt.com/docs/app-server).

Allowed surfaces: quota adapters/model/session, Shell wiring, XAML/settings,
Desktop quota rows, localization, attribution, focused regression tests and docs.
No changes to installed authentication stores, elevation or hardware acquisition.
Observed live Codex TLS handshake fails when pinned to TLS 1.2 but succeeds with
TLS 1.3 available. Quota and UpdateCheck therefore enable TLS 1.2 and TLS 1.3,
so updater refresh cannot re-pin a process-wide TLS 1.2-only policy. Windows
chooses an available protocol. System-default mode also failed in the live probe.
Rollback is removal of the opt-in feature; existing settings remain compatible.

Acceptance: parser fixtures for all three providers including missing, zero,
malformed and additional windows; cancellation/disable/error lifecycle tests;
isolated WPF rendering/settings/Desktop tests; full scripts/Validate.ps1. Real
provider checks are separate from fixtures and must report unavailable logins.
High-risk frozen diff requires Native Review after deterministic checks.

<!-- sop-risk-classification: {"facts":{"blast_radius":"shared","change_kind":"implementation","data_boundary":"sensitive","destructive":"no","failure_cost":"material","irreversibility":"reversible","operational_controls":"not_applicable","privilege_boundary":"changed","project_policy":"default","rollback":"easy","scope_knowledge":"known","uncertainty":"material","verification":"deterministic"},"formal_review":"required","kind":"risk-classification-assessment","reasons":{"formal_review":["high_risk_requires_review"],"risk":["privilege_boundary_change"]},"risk":"high","schema_version":2} -->
