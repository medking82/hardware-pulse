# Hardware Pulse 0.4.1

![Hardware Pulse 0.4.1 illustrative UI with fictional demo data](https://raw.githubusercontent.com/medking82/hardware-pulse/main/docs/showcase/pulse-demo.png)

*Illustrative UI with fictional hardware and readings.*

Auto density now measures the available card viewport instead of switching at a fixed window height. Full hardware information takes priority: reduce padding first, then use compact metrics, and only then hide descriptions if necessary. Enlarging the window restores detail automatically. Details transitions use a short fade that respects Windows reduced motion.

Validated: full regression suite, logical 1080p/1440p/4K layout inputs, shrink/grow detail restoration, and local upgrade from running 0.4.0 (exit 0, no reboot). Live readings remained available after launch. This is a routine UI correction, with no installer privilege or driver changes.

**Update limitation:** Check for Updates currently checks stable GitHub releases only. This version is now the stable Latest release and can be discovered by older clients. Download Update opens a release page; it does not download or install automatically. Use this release's EXE asset to update manually.

The installer remains self-signed; SignPath approval is pending. Game overlay remains experimental. See the 0.4.0 release notes for inherited platform and testing limits.

[简体中文](RELEASE-NOTES-0.4.1.zh-CN.md)
