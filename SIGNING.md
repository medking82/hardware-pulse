# Code signing policy

## Current status

Hardware Pulse is preparing an application to SignPath Foundation. Approval and Foundation signing have **not** been obtained. Current releases use a self-signed certificate with subject `CN=Marck Wong`; this is not public CA trust. No root certificate or antivirus exclusion is installed.

## Project roles

- Author / committer: [Marck Wong (medking82)](https://github.com/medking82)
- Reviewer of external contributions: Marck Wong
- Signing approver: Marck Wong

Original Hardware Pulse code is MIT licensed. Third-party components retain their own notices and licenses. Signing should cover only Pulse-owned executables and the installer; upstream binaries must not be re-signed as Pulse.

## Intended Foundation workflow

Build from the tagged public source in CI, verify dependencies against their locked hashes, submit owned artifacts for signing, require explicit maintainer approval, verify returned signatures and timestamps, then package/publish the exact verified artifacts with checksums. Foundation account/project configuration and MFA confirmation are pending. No Foundation credentials or approval are assumed by this repository.

If accepted, this page will be updated to acknowledge free code signing provided by SignPath.io, with certificate by SignPath Foundation. Until then this is a proposed workflow, not a claim of sponsorship or trust.

See the [privacy policy](PRIVACY.md) and [SignPath eligibility and signing conditions](https://signpath.org/terms).
