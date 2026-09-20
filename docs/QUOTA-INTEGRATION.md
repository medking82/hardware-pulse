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
Antigravity first uses its local language server;
verify current-user process ownership and PID-owned listening ports before sending
its CSRF token to loopback. No Token Monitor process or PowerShell/Node runtime.
Only fixed provider HTTPS endpoints; disable redirects. Loopback TLS exceptions
are request scoped. No global certificate bypass. Providers are independently opt-in.

## Windows Antigravity CLI fallback (next iteration)

When no usable current-user Desktop language server is found, use an existing
`LocalAppData/agy/bin/agy.exe` with the fixed native `/usage` command. Do not
download a CLI, search PATH, launch a model prompt, create a login, borrow OAuth
client credentials, or persist tokens. The installed CLI owns authentication.
An identified Desktop session's HTTP/auth failure must not switch accounts by
falling back to the CLI. The CLI account can differ from the Desktop account:
Monitor status and Desktop quota labels identify the source as `CLI`.

The existing bounded child-process lifetime is reused by Codex and Antigravity:
15-second deadline, cancellation, hidden stdio, capped diagnostics, and owned-child
cleanup. Antigravity stdout is capped at 64 KiB, requires a successful exit and a
complete two-window TSV report for every returned pool, and rejects duplicate
windows, invalid percentages, missing timezone/reset and unknown report formats.
No raw CLI report or diagnostic output enters application logs or settings.
The five-minute QuotaSession cadence and explicit opt-in remain unchanged.

### Pipe cancellation hardening

The Windows child adapter reads stdout/stderr through a single-reader pipe stream.
It probes available bytes before reading, checks cancellation while waiting, and
retains StreamReader UTF-8 decoding. It does not rely solely on killing the direct
child to produce EOF: another writer may still hold a pipe handle. Cleanup cancels
and joins the diagnostic reader even when the child has already exited. Existing
output caps, fixed CLI arguments and the 15-second deadline remain in place.
The stream borrows its handle from Process; it never closes an unrelated handle,
changes credentials, starts a replacement refresh, or terminates other processes.

The local pre-fix diagnostic showed both parsers remaining blocked after token
cancellation until the in-process writer closed. `scripts/QuotaPipeTests.cs` now
checks cancellation with an open idle writer for both parsers and stderr, plus
fragmented UTF-8, EOF and worker completion. The existing RPC child fixtures cover
real owned-child exit, timeout cancellation and excessive diagnostics. These
checks do not establish that a real provider spawned a descendant, that antivirus
caused a quota failure, or that expired credentials can renew automatically.

Evidence: with no observed Antigravity/agy/language-server process before the
probe, the released Windows reader returned `Open Antigravity to read quota`;
the installed native CLI independently returned weekly and five-hour Gemini
quota. The revised Windows reader returned `Live`, `Source=CLI`, two windows,
and no matching process remained after completion. This proves the local
installed-CLI path, not behavior on machines without that CLI or login.

Allowed changes: Windows quota adapters, shared read-only Source metadata,
existing quota labels, and focused tests. Preserve WPF layout/appearance,
credential stores, release 0.6.28 and all hardware acquisition. Rollback removes
the CLI fallback and optional Source metadata; no credentials need restoration.
Required checks: parser fixtures, existing Codex child-lifetime fixtures, full
native validation, modern/shared consumer build/tests, and independent review.

Refresh: background, at most one refresh per provider; five-minute cadence with
bounded requests, no per-frame API calls. Disable/dispose cancels requests and
rejects late results. Clear old windows on failures/account transitions; no fabricated
100% or reset-to-full inference. Show observation age and expired readings explicitly.

HTTP status presentation distinguishes missing/rejected authentication (401,
`Login required`) from forbidden access (403, `Quota access denied`). A 403 does
not prove the login expired. Both retain the normal five-minute retry cadence;
429 keeps a two-minute minimum backoff, extended by a valid server
Retry-After deadline. Manual Refresh respects both the minimum and server deadline. Missing
or invalid headers retain the two-minute default; transient failures retry after 30 seconds.
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

## Update failure visibility (next iteration)

The installed 0.6.28 UI repeatedly reports update-check failure and generic Codex/Claude
quota failures. Read-only probes using the installed assembly succeed, including an
isolated WPF Shell. A locally built diagnostic WPF app with the same saved preferences
reports up-to-date, live Codex/Antigravity and Claude login-required. Returning to the
installed executable reproduces the original failures. This is evidence of a
process/package/environment difference, not proof of a firewall cause or a fixed updater.
The installed app and its owned collector were restored after comparison.

The bounded change exposes only a sanitized update failure category/status in Settings;
no exception messages, response bodies, request headers, credentials or URLs are shown.
A fake network failure regression proves the diagnostic is populated and cleared after
successful retry. Existing installer identity, digest, redirect and download checks stay
unchanged. This visibility improvement must not be advertised as resolving the still
unidentified installed-app network failure.

Resolution: the user restored Bitdefender firewall rules to defaults. The installed app then showed You are up to date and live Codex; Claude showed Login required. Collector was restored through its unchanged owned scheduled task and consecutive advancing snapshots plus live UI were verified. No security rules were changed by the agent.

## Recovery regression evidence (Windows 0.6.31)

QuotaSession coalesces manual Refresh clicks received during a pending request into
one follow-up read. Provider requests remain serialized, disabled generations cannot
publish late results, and a rate-limited completion retains its backoff. On UI resume,
a completed successful observation older than the normal five-minute interval is
refreshed immediately when its previous deadline has elapsed; a fresh observation
keeps the normal cadence.

CoreQuotaSessionTests includes 360 synthetic rounds spanning 36 hours of host clock,
810 reads across three providers, mixed success/network/auth/rate-limit responses,
provider toggles, exact request counts, no per-provider overlap, and clearing old
quota windows on failures. The same fixture passes on .NET Framework and .NET 10.
This is accelerated lifecycle coverage, not 36 hours of real provider availability
or evidence that an actual credential has crossed its expiry and renewed.

Claude credential lifetime remains owned by Claude Code. Its documented
[long-lived setup token](https://code.claude.com/docs/en/authentication#generate-a-long-lived-token)
is model-request-only; do not present it as a supported quota credential replacement.
The documented `claude auth status` command reports authentication state; its
[CLI contract](https://code.claude.com/docs/en/cli-reference) does not promise a
quota-read or credential-renewal operation. A persistent-login claim requires an
observed, supported renewal path, not repeated login prompts, inference calls to
force refresh, or copying/rotating an application's refresh token behind its back.
