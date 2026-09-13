# 0.4.0 release readiness

## Acceptance criteria

- X hides to tray and continues collecting; explicit Exit stops Pulse-owned capture and requests collector shutdown.
- Upgrade stops the previous installed application before files are replaced, with bounded failure and without stopping unrelated applications.
- Settings, card order and geometry survive restart; startup controls affect only verified Pulse tasks.
- FPS metrics filter the selected PID and swapchain, reject invalid samples and stop showing stale readings. Six overlay anchors stay inside the selected window.
- Appearance supports zero background opacity; update checks are opt-in and disclose their GitHub request. Current update action opens the download page, not an automatic installer.
- Dependency versions/checksums are fixed. Linux/macOS remain planned, and SignPath approval is not claimed.

## Deterministic evidence (2026-09-13)

`scripts/Validate.ps1` passed: syntax, UTF-8 BOM, XML/SVG, version checks, sensor parsing/discovery, snap geometry, language/settings persistence, real WPF tray hide/restore/Exit dispatcher lifetime, card drag, FPS math/filtering/staleness and six anchors.

An earlier build was blocked when Bitdefender quarantined `build/PulseUpgrade.exe` as `Gen:Variant.MSILHeracles.265293`. The standalone helper has now been removed; Inno Setup directly writes the cooperative stop request and waits for the installed collector, then delegates UI shutdown to Windows Restart Manager. No exclusion, restoration or protection change was made.

The rebuilt installer completed the real in-place upgrade from the running previous version on 2026-09-13 (exit 0, no reboot). Local log `dist/upgrade-0.4.0.log` records cooperative shutdown, collector exit and successful file installation. The installed 0.4.0 UI reported 17/17 live sensors. Closing X hid its window while collection continued. A cooperative stop then exited the widget and collector; subsequent task launch restored the application. This does not establish compatibility with every antivirus engine or clean machine. The certificate is still self-signed, not SignPath-approved.

## Remaining verification

- Reboot behavior remains untested. Real installed startup-off/on commands returned 0, disabled only logon triggers and kept on-demand tasks enabled. The original enabled logon preference was restored.
- Live game overlay click-through/DPI/foreground handling and elevated PresentMon capture. The ordinary-user probe returned access denied; synthetic FPS tests are not live-game evidence.
- Public download checksum verification after publication; installer build and local upgrade now pass.
- First dual review: Claude completed; Antigravity failed with `native_output_truncated`. Packet `641e221b3531081a2ae5893baa5a38fffbec92a495e66934413bbc9b6af0eec5` is retained locally. This is not a completed dual review.
- Claude's startup P1 was accepted and repaired by separating logon triggers from on-demand task execution. Added tests verify that startup-off keeps tasks enabled. Light-theme contrast and stale changelog claims were corrected. The privacy policy already described update requests; an in-app localized disclosure was added too.
- Subsequent UI changes add compact overview, full-details access, custom scrollbar and persistent card visibility. These changes are not covered by a completed dual review.

## Updated UI checks

WPF tests exercise work-area inputs for 1080p at 100/125/150% scaling, 1440p at 125/150%, and 4K at 150%. At computed initial sizes, the compact overview has no vertical or horizontal scroll overflow with test readings. This is a logical-layout test on one Windows machine, not physical validation on every laptop/DPI combination. Card hiding preserves order, all-hidden state retains Settings, and visibility restores in a new process.

The local candidate is built and installed. On 2026-09-13 the maintainer explicitly authorized publication as a Pre-release with a waiver of a new model review, accepting the repaired Claude findings and deterministic/local installation evidence. The previous partial dual review remains unchanged and is not represented as completed. No replacement review was fabricated or launched. SignPath application submission is confirmed, but certificate approval and CI signing integration remain pending.

Installer SHA-256: `47990faeea544d42c95ebb786649da8b4c41c4147bcd53fe8da797f0bd3383c0`. The release uses the exact installer tested locally, not a rebuilt executable.
