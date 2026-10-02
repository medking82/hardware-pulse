# Claude quota refresh recovery (Windows 0.6.46)

[简体中文](CLAUDE-QUOTA-REFRESH.zh-CN.md)

## Observed failure

The installed 0.6.45 scheduler retained a 30-minute local authentication cooldown after HTTP 401, even after Claude Code updated its credentials. Manual Refresh silently skipped Claude while refreshing other providers. A separate bounded request with the current credential succeeded. That proves the current login worked, not the cause of the earlier rejection or indefinite login renewal.

## Repair boundary

- Settings → AI Quota → Refresh Quota shows progress, completion or a next-allowed-request timestamp per enabled provider. A card's ↻ button refreshes only that provider. Card status also exposes progress and deferred requests.
- Only an enabled, non-local Claude source whose last completed remote status is Login required can shorten authentication backoff. The earliest next attempt is five minutes after that failed observation. Explicit refresh and known source-revision changes use this same floor. Pending requests are not queued again.
- Known rejected-source metadata is checked at most every 30 seconds through the existing read-only metadata predicate. No new credential source, token-content read, watcher, process launch or credential write is introduced. Without a known rejected revision (including after restart), automatic recovery retains the normal saved deadline; manual refresh remains available.
- Active HTTP 429, Retry-After, access and transport failures cannot be shortened this way. Rate-limit counters are not erased by an authentication recheck; only success clears them. Persistent schedules continue protecting provider toggles, source switches and restart.
- Before IO, the persisted reservation is explicitly marked Refresh pending with its reservation timestamp, so a restarted in-flight request cannot be mistaken for a completed authentication rejection. The schedule schema remains unchanged and retains only allowlisted scheduling metadata.
- Authentication remains owned by Claude Code. A metadata change is not proof of a valid login; the recheck can fail again and must remain bounded. This does not support unattended token renewal or a model-request-only setup token as a quota credential.

## Validation and rollback

Core fixtures cover restored 401 state, five-minute floor, repeated clicks, crashed in-flight reservations, preserved 429 debt, non-authentication cooldowns and normal cadence after success. Isolated WPF fixtures cover the real card/button handlers, Settings feedback, progress, completion, deferred requests and injected metadata predicates. They do not access real credentials or make provider requests.

Run scripts/Validate.ps1 -ModernCore and build/sign the matching Windows installer before publication. Real credential expiry/renewal, long-duration provider availability and fresh-machine installation remain separate acceptance tasks.

Rollback installs the prior stable Windows package. Existing settings and schema-1 schedules remain compatible; there is no credential migration to undo.
