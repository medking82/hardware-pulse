# Independent quota readings

Scope: opt-in Codex, Claude and Antigravity remaining quota and reset times in
Monitor and Desktop View. No token history, spending, chat contents or account management.

Owner: the unprivileged Shell owns a separate asynchronous QuotaSession. Hardware
Collector, ReadingSession, FPS and startup tasks retain their existing contracts.
Provider adapters return only sanitized quota windows and finite-state errors. No
credentials, account emails or HTTP response bodies enter snapshots, settings or logs.

Authentication: read existing current-user Codex/Claude login on each refresh;
never write/rotate credentials or launch a login/CLI. Expired login requires the
owning application to renew it. Antigravity requires its local language server;
verify current-user process ownership and PID-owned listening ports before sending
its CSRF token to loopback. No Token Monitor process or PowerShell/Node runtime.
Only fixed provider HTTPS endpoints; disable redirects. Loopback TLS exceptions
are request scoped. No global certificate bypass. Providers are independently opt-in.

Refresh: background, at most one refresh per provider; five-minute cadence with
bounded requests, no per-frame API calls. Disable/dispose cancels requests and
rejects late results. Clear old windows on failures/account transitions; no fabricated
100% or reset-to-full inference. Show observation age and expired readings explicitly.

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
