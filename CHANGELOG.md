# Changelog

English first; 简体中文 follows each version. Dates are release dates. Unreleased entries describe source changes, not an available download. Author: [Marck Wong](https://github.com/medking82).

## [0.3.1] — Unreleased

### Changed

- Dragged cards lift subtly and follow the pointer; neighboring cards animate into the proposed order before release.
- Drop and cancellation settle with an interruptible 220 ms ease-out. Re-grabbing uses the current displayed position.
- Respect Windows client-area animation preference: reduced motion skips decorative scale and settling transitions. Keyboard menu reordering remains immediate.

### Fixed

- Canceling a preview restores the original order without saving. Successful drops save only once; stable card IDs and user labels remain unchanged.

### Validation / limits

- WPF gesture tests cover preview displacement, cancel, reduced motion, commit and interrupted settling, alongside existing regression tests.
- Pointer feel and edge autoscroll still require hands-on validation. This version does not yet fix the installer inability to close an already-running elevated collector automatically.

### 简体中文

- Card drag 增加轻微抬起、相邻 cards 让位，以及 220 ms ease-out 回落；可在 animation 中重新抓取。
- Esc 取消后恢复原顺序且不保存；drop 完成后仅保存一次。
- 遵循 Windows animation preference；keyboard menu 排序保持即时响应。
- WPF gesture regression 覆盖预览、取消、reduced motion、提交和中断回落；真实 pointer 手感和 edge autoscroll 待验证。
- 尚未修复 upgrade 时无法自动关闭 elevated collector 的问题。

## [0.3.0] — 2026-09-13

### Added

- English, Simplified Chinese and Traditional Chinese selection in Settings, applied immediately and persisted across restarts.
- Translated monitor labels/status, Settings, reorder menus, tooltips and accessibility names, with English fallback for unknown language codes.
- Version consistency checks for the native app, installer, build and About text.

### Changed

- Installer checks .NET Framework 4.8 and Windows PowerShell 5.1 before proceeding.
- Missing PawnIO library or driver registration triggers its bundled installer, followed by a presence check before startup registration.
- English-first bilingual README and Release Notes.

### Validation / limits

- WPF language switching, autosave/restore, custom-name preservation and 240-pixel render checks passed; sensor/settings/snap regressions and signed builds passed.
- Windows 10, clean-machine dependency installation and installed upgrade were not validated at release. An upgrade was subsequently reported blocked by a running collector; asking the collector to stop released the occupied files.
- Self-signed Authenticode is not public CA trust; Windows/SmartScreen and antivirus warnings can remain.

### 简体中文

- 新增 English、简体中文、繁體中文 UI，支持即时切换和 restart 后恢复；保留 hardware model、自定义名称及 card order。
- 增强 .NET / PowerShell prerequisite checks 和 PawnIO 安装后检查。
- README 与 Release Notes 改为 English 优先；增加 version consistency checks。
- Language、persistence、窄窗口和现有 regression checks 通过；Windows 10 / clean-machine / upgrade 未在 release 前验证。之后发现运行中的 collector 可阻塞 upgrade，停止 collector 可释放占用。

## [0.2.0] — 2026-09-13

### Added

- First public GitHub Release: Windows x64 EXE installer with pinned LibreHardwareMonitor libraries, official PawnIO installer, dependency hashes and third-party notices/source archives.
- Compact glass widget for CPU, GPU, memory, NVMe and fan readings; equal DIMM/NVMe columns and live RAM/VRAM usage bars.
- Original vector icons, adaptive typography, opacity controls and solid-background fallback.
- Separate Settings page, editable hardware labels and automatic device discovery.
- Saved position, size, appearance and card order, including debounced autosave and Unicode custom names.
- Card drag ordering, keyboard/context-menu alternatives, and screen/neighbor edge alignment with Alt bypass.
- Current-user collector and delayed widget login tasks; self-signed Marck Wong app/installer signatures and public checksums.

### Fixed

- Stale readings caused by differing AppData views: shared snapshots moved to per-user ProgramData runtime storage.
- Sticky window dragging: snap only when movement ends, allowing free drag-away.
- UTF-8 middle-dot corruption, incomplete Settings gear and ambiguous GPU fan labels; distinguish telemetry channels from physical fan count.
- Replace unreliable widget Startup shortcut with a dedicated login task.

### Validation / limits

- Actual Windows 11 AMD CPU/NVIDIA GPU sensor readings and installation validated; Intel CPU/AMD GPU discovery covered by fixtures only.
- Windows 10, clean-machine installation, multiple-monitor DPI, uninstall/reinstall and the replacement widget task's next reboot remained untested.
- One CPU/GPU and up to two reporting DIMMs/NVMe drives. Missing or ambiguous sensors display a dash; SPD addresses are not physical slot identities.
- Public repository visibility does not grant an open-source license to original application code. Dependencies retain their upstream licenses.

### 简体中文

- 首个 public Release：Windows EXE installer，包含固定 versions 的 dependencies、third-party notices/source archives 与 checksums。
- 提供 compact glass UI、CPU/GPU/Memory/NVMe/Fan readings、RAM/VRAM usage、独立 Settings、自定义名称和自动 discovery。
- 保存窗口、外观、card order；支持 drag 排序、keyboard menu 和窗口边缘吸附。
- 修复 stale snapshot、sticky drag、UTF-8 编码、gear icon 与 GPU fan label 问题；GUI startup 改用独立 task。
- 已验证 Windows 11 当前机器；其他品牌主要依靠 fixtures，其他 OS/clean-machine/reboot 场景仍有验证限制。

[0.3.1]: https://github.com/medking82/hardware-pulse/compare/v0.3.0...main
[0.3.0]: https://github.com/medking82/hardware-pulse/releases/tag/v0.3.0
[0.2.0]: https://github.com/medking82/hardware-pulse/releases/tag/v0.2.0
