# Code signing policy

## Current status

Hardware Pulse submitted its SignPath Foundation application on 2026-09-13. On 2026-09-16, the maintainer reported that the application was rejected. The supplied response cites insufficient external public trust and visibility signals, such as community adoption, independent references and sustained engagement; it explicitly does not judge project quality or potential. SignPath welcomes a new application after broader recognition. Foundation signing is unavailable; do not treat the application as pending or assume future approval. Current releases use a self-signed certificate with subject `CN=Marck Wong`; this is not public CA trust. No root certificate or antivirus exclusion is installed.

## Project roles

- Author / committer: [Marck Wong (medking82)](https://github.com/medking82)
- Reviewer of external contributions: Marck Wong
- Signing approver: Marck Wong

Original Hardware Pulse code is MIT licensed. Third-party components retain their own notices and licenses. Signing should cover only Pulse-owned executables and the installer; upstream binaries must not be re-signed as Pulse.

## Conditional Foundation workflow (inactive)

If a future application is approved: build from the tagged public source in CI, verify dependencies against their locked hashes, submit owned artifacts for signing, require explicit maintainer approval, verify returned signatures and timestamps, then package/publish the exact verified artifacts with checksums. No Foundation account/project configuration, credentials or approval are currently assumed.

If accepted, this page will be updated to acknowledge free code signing provided by SignPath.io, with certificate by SignPath Foundation. Until then this is a proposed workflow, not a claim of sponsorship or trust.

See the [privacy policy](PRIVACY.md) and [SignPath eligibility and signing conditions](https://signpath.org/terms).
