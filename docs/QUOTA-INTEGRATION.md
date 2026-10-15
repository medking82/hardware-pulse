# Independent quota readings

## Native Windows development: typed balance providers

This increment targets the currently installed native WPF runtime, not an installer
release or the separate shared-host preview. `QuotaCatalog` owns provider IDs,
SVG names, accents, metric kind and authentication kind. `QuotaSession` consumes
those IDs and remains the sole five-minute scheduling/cancellation owner. Percent
windows and decimal `QuotaBalance` rows are separate contracts: balance rows keep
currency and total/granted/topped-up amounts and never acquire a percentage bar.
Native settings generate opt-ins from the catalog; API-key providers share a masked
credential editor and user-bound DPAPI store. Native Monitor and Desktop consume
the same sanitized reading. New observations fade in over 160 ms only when Windows
client-area animations are enabled; existing reorder animation is retained.

To add a provider such as Grok or Qwen, declare its metadata, implement a fixed-origin
credential/transport adapter and response decoder, supply a themeable 24×24 SVG with
provenance, and add parser/transport/lifecycle fixtures. The catalog is not a
universal API protocol or automatic permission to send keys to arbitrary URLs.
Do not add provider-specific cards or store secrets in ordinary settings. The
shared-host preview explicitly refuses the new source instead of falling through
to Codex; it does not yet expose the DPAPI editor.

DeepSeek uses only GET `https://api.deepseek.com/user/balance`. Its key remains in
the native UI user's encrypted local store. Requests are bounded, cancellable,
redirect-free and opt-in. Saving/removing a key invalidates in-flight results;
provider failures clear old balances. No Chrome debugging or browser-session
extraction is added. Key saved means saved, not that the provider validated it.
Concurrent saves are not serialized; a rare file-replace race surfaces the standard
"Could not save encrypted key" status and keeps the previous key intact.

Claude 403 is access denied, not proof of an expired login. File expiry is explicit;
if Claude Code rotates its token during a rejected request, Pulse re-reads once
and retries only when the credential changed. It never refreshes or writes the
owning app's login. This does not repair a genuinely expired session. Current PC
Claude live quota succeeded during investigation; that is not proof that all
intermittent authentication failures are resolved.

The B850M known SYS1/SYS3 mapping now retains a channel when one value is null;
no previous RPM is substituted. Unknown-board discovery is unchanged. Missing
values have explanatory tooltips. The live PC sample captured no missing sensor;
the intermittent user symptom as a whole remains unproven. Synthetic null-channel
regression reproduced the capability bug before its fix.

DSH source investigation: its platform account reader queries
`/api/v0/users/get_user_summary` with an `x-dsh-auth-token`, projecting
`normal_wallets` and `bonus_wallets` with CNY/USD balances. This is a separate
authentication route, not a percentage subscription. No DSH token is copied into
Pulse, and no GA401 summary service is deployed by this increment. Whether the
user's API key and DSH login address the same account/wallet has not been verified.

Focused Windows acceptance is `Test-Core.ps1`, `Test-WindowsAdapters.ps1`,
`Test-NativeSensors.ps1`, and `Test-QuotaFeature.ps1` after the native build.
The latter exercises real WPF controls with synthetic credentials and captures
narrow bilingual Settings and mixed metric cards. Live DeepSeek API acceptance
requires a key supplied directly to the app; synthetic balances are not a claim
of successful account access. The full native UI suite currently also fails
`Desktop auto columns ignore resize` on the unchanged base under the same SSH
desktop, so it is not reported as passing.

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
