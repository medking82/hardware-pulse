# Changelog

English first; 简体中文 follows each version. Dates are release dates. Unreleased entries describe source changes, not an available download. Author: [Marck Wong](https://github.com/medking82).

## 0.5.2 — 2026-09-14

- Reuse fixed sensor-discovery Regex instances and frozen UI brushes to reduce repeated managed allocations without changing the two-second collection interval or sensor selection rules.
- Skip card rendering while the window is hidden or minimized. Continue collecting session peaks and tracking stale readings; refresh immediately when the window is restored.
- Add a repeatable allocation workload and timed UI replay, plus regressions for hidden-window peaks, stale state and immediate restoration. Allocation reductions do not imply the same reduction in resident RAM.
- 复用 sensor-discovery Regex 和 UI brushes，减少 managed allocations；隐藏或 minimized 时暂停 card rendering，继续记录 Session Max 和 stale state，恢复窗口时立即刷新。保留原有采样频率及 sensor selection。

## 0.5.1 — 2026-09-14

- Fix startup registration on machines with missing scheduled tasks. Windows COM interop can report a missing task as FileNotFoundException; setup now treats that specific missing-task result as absent and creates the owned tasks.
- Rerun this installer over a failed 0.5.0 installation to repair startup registration while preserving preferences. Access-denied and unrelated errors still surface; permissions and hardware collection are unchanged.
- Add a real Task Scheduler regression for missing tasks and an isolated elevated integration test for first registration, partial-install repair, disabled-startup preservation and cleanup.
- 修复 missing task 被 .NET 转为 FileNotFoundException 后中断 startup registration 的 bug；直接覆盖安装即可修复失败的 0.5.0 installation，保留 preferences。
- 新增真实 Task Scheduler regression 和隔离 integration test；不改变权限或 hardware collection，installer 仍为 self-signed。

## 0.5.0 — 2026-09-14

- Replace the installed PowerShell UI, collector and startup helpers with C#/WPF. No System.Management.Automation dependency or runtime scripts are shipped; developer build/test tools may still use PowerShell.
- Preserve settings, window geometry, card order/visibility, language, read-only sensor discovery, FPS overlay and verified in-app update flow.
- Upgrade in place with an explicit obsolete-file cleanup list. Preserve Windows components, shared PawnIO and user preferences; no clean install is required. Treat Task Scheduler's omitted default XML values correctly and keep UI/collector privilege separation.
- Compare the baseline and native implementation using synthetic UI replay and sequential real hardware collection. Memory use was lower on the tested host; see [method and results](docs/PERFORMANCE-0.5.0.md), including the limits of single-run measurements.
- Installed runtime 改为 C#/WPF，不再依赖 PowerShell；保留 settings、桌面位置、card 选择、language、sensor discovery、overlay 和 updater。
- Installer 支持原位 upgrade，清理明确列出的旧 app 文件，保留 Windows components、共享 PawnIO 和用户设置。修复 Task Scheduler 省略 default XML values 时的 compatibility。
- 对比测试显示本机 memory use 降低；benchmark 不是所有电脑或 battery life 的保证。SignPath 仍待审核，installer 仍为 self-signed。

## 0.4.9 — 2026-09-14

- Make Compact readings independent of window height; Details retains full rows while adapting spacing, with a visible selected state.
- Keep card widths stable when scrolling appears. Use an integrated narrow monitor scrollbar and a rounded keyboard-focus outline.
- Translate pump/system fan channels and explain that a zero-RPM channel does not describe all fans or pumps. No hardware reading or driver behavior changed.
- Stop the overlay timer when unused, avoiding its previous 500 ms wakeups. PowerShell runtime remains in use; no measured memory reduction is claimed.
- Compact 使用紧凑 readings；Details 保留完整数据并自适应 spacing，增加 selected 状态。固定 scrolling 前后的 card 宽度，采用细 scrollbar 与圆角 keyboard focus。
- 补齐水泵与系统风扇 label 翻译，说明单通道 0 RPM 的含义；未修改 driver 或硬件采集逻辑。关闭未使用的 overlay timer，仍依赖 PowerShell runtime。

## 0.4.8 — 2026-09-14

- Add a saved 10–16 px text-size slider in Appearance (12 by default). Sizes use WPF logical pixels and follow Windows display scaling; older large-text preferences migrate automatically.
- Keep native text rendering while resizing card typography proportionally. Tighten card spacing, reflow narrow headers and compact reading groups, and truncate long row labels with full-text tooltips instead of wrapping them into tall cards.
- Verify font persistence across a new process, temperature/header bounds at 240/310 DIP, and existing screen-size and language regressions. Very large text or Details may still require scrolling.
- Appearance 新增 10–16 px 字号设置，默认 12，自动保存并兼容旧 Large Text 设置；使用 WPF logical pixels，跟随 Windows display scaling。
- Card 随字号调整 typography 与 spacing，窄窗口下 header 和 compact readings 自动换行，长 label 省略并通过 tooltip 显示全文，避免标题重叠与多余高度。
- 已验证字号保存、重启恢复、240/310 DIP header 边界与原有 layout/language regression；大字号或 Details 仍可能需要 scrolling。

## 0.4.7 — 2026-09-14

- Discover Intel D3D 3D load and shared GPU memory by sensor semantics, without machine-specific identifiers. Shared memory is labeled separately from dedicated VRAM, including in the overlay.
- Adapt monitor cards to detected sensor capabilities: hide absent fan/temperature fields and unused disk columns, retain saved card preferences, and restore fields when sensors return. Null readings remain unavailable rather than becoming zero.
- Show the number of valid mapped readings without the fixed 17-field denominator.
- 通用识别 Intel D3D 3D 负载及 GPU 共享内存，不绑定电脑型号；共享内存与独立 VRAM 明确区分。
- Monitor 根据传感器能力隐藏缺失的风扇、温度和硬盘占位，保留 card 设置，传感器恢复后自动显示。无数据不再误认为零读数。
- 移除固定 17 项分母。此版本没有新增厂商专用风扇 controller 支持。

## 0.4.6 — 2026-09-14

- Fix script-policy startup failures on Windows clients: use process-scoped RemoteSigned for the embedded host and collector. No persistent policy changes; Group Policy remains authoritative and unsigned Internet-marked scripts remain blocked.
- Keep text at native layout size in narrow windows, use Display text formatting, and tighten card spacing instead of scaling the whole interface.
- 修复 Windows 脚本策略导致的 app/startup helper/collector 启动失败：仅当前 process 使用 RemoteSigned，不修改持久系统设置，保留 Group Policy 与 Internet 脚本签名检查。
- 窄窗口不再整体缩字；使用 Display 字体排版和紧凑 card spacing。

## 0.4.5 — 2026-09-14

- Honor a fresh installer shutdown request even when an old startup STOP marker could not be deleted. This fixes a hidden widget retaining files during upgrade.
- 修复启动时无法删除旧 STOP 文件后忽略新退出请求的问题，避免 widget 隐藏到 tray 后继续占用安装文件。

## 0.4.4 — 2026-09-14

- Fix edge snapping trapping slow drags: derive movement from total cursor displacement since drag start instead of Windows' rebased moving rectangle. Pull away normally without Alt; retain the 24-DIP attraction range.
- 修复缓慢拖离 edge 时反复吸回的问题：根据 drag 起点累计 cursor 位移，正常拖动即可释放，无需 Alt；保留 24 DIP 吸附范围。
## 0.4.3 — 2026-09-14

- Group Settings into collapsible sections with wrapping switch labels; center the gear and replace its dotted focus decoration.
- Keep glass translucent when inactive. Opacity now includes card and gradient layers; zero opacity disables blur.
- Lock window movement, resize and card order. Locked Monitor reduces background opacity without fading readings; unlock restores the saved appearance. Tray adds Settings, Always on Top and Lock actions.
- Strengthen left/right and other edge magnets to 24 DPI-scaled logical pixels, apply during drag, and align to visible adjacent-window frames. Alt bypass remains available.
- Add Auto (System) app language and English/Simplified/Traditional Chinese installer detection, with localized setup messages.
- Download updates inside Pulse, with optional automatic downloads, progress, verified size/SHA-256 and Install and Restart. No unattended installation and no security-setting changes.
- Settings 改为折叠分组与 switch；修正 gear 居中、focus 和窄窗口 label 换行。
- 修复 inactive 透明度，card/gradient 跟随 opacity；锁定 Monitor 更透明，禁止移动、resize 和排序，tray 保留解锁入口。
- 吸附范围提高至 24 DIP，拖动时对齐 screen/window edge，保留 Alt 自由移动。
- App 增加 Auto (System)，installer 支持三种 language；新增 app 内下载、校验和点击安装更新。
## 0.4.2 — 2026-09-13

- Translate generated system/storage temperature descriptions, intake/exhaust labels, memory slots/configuration and RAM/VRAM usage labels in Simplified and Traditional Chinese. Preserve model identifiers and user-defined names.
- Use clearer Chinese wording for VRAM temperature, with a tooltip distinguishing memory-chip temperature from GPU core temperature.
- 补齐动态硬件说明、进出风、内存槽位及用量标签的简繁体翻译；“显存结温”改为“显存温度”，tooltip 说明其含义，型号与自定义名称保持原样。

## 0.4.1 — 2026-09-13

Promoted to stable / Latest with maintainer authorization after local regression and in-place upgrade passed. The installer binary is unchanged.

- Auto density now measures the actual card viewport in logical WPF units instead of hiding hardware names below a fixed window height.
- It preserves full information first, then reduces padding, then uses compact metric rows. Device descriptions are hidden only when those layouts still do not fit. Hidden cards are excluded from measurement.
- Details keeps full information available. Switching density through Details uses a short opacity transition that respects Windows reduced-motion settings.
- Regression covers resizing down and back up, plus the existing logical display-size matrix.
- 自动 density 根据实际 viewport 测量：优先完整信息，依次压缩留白、调整 metrics 排列，最后才隐藏设备说明。窗口放大后自动恢复；Details 切换增加轻量过渡并尊重 reduced motion。

## 0.4.0 — 2026-09-13 (Pre-release)

### Added

- Adaptive compact cards keep core readings together on laptop-sized work areas; Details restores full device descriptions. Settings can hide/show each card without discarding its order.
- In-app rounded scrollbar with transparent track, hover/drag feedback and a wider interaction area, replacing default arrow buttons.
- Optional click-through game overlay for a selected foreground window, six anchors, compact/detailed layout, selectable hardware readings and local PresentMon FPS capture.
- FPS: current one-second average, rolling-60-second AVG/MIN/1% Low. Application-present metrics do not count generated frames; 1% Low requires at least 100 samples. Missing/denied telemetry is not reported as zero.
- Close-to-tray, restore and explicit Exit. Exit stops the widget, overlay, owned PresentMon capture and requests collector shutdown.
- Start with Windows checkbox controls logon triggers for both verified current-user tasks, with UAC and state readback. On-demand collector startup remains available when logon startup is off.
- Background color, adaptive foreground and 0–100% background opacity. Native glass blur remains Windows-managed.
- Optional automatic GitHub version check and manual update checks, with a download-page action. Automatic checks are opt-in; no unattended installation.
- MIT License, privacy and proposed code-signing policies. SignPath application was submitted on 2026-09-13; no Foundation certificate has been granted.
- Public demonstration image uses fictional hardware/readings.

### Fixed

- Turning off logon startup no longer disables on-demand collector startup. Upgrade preserves the logon preference; inaccessible STOP files no longer abort widget initialization.
- Light backgrounds use dark button labels and readable status colors.
- Installer requests cooperative collector shutdown directly in Inno Setup and lets Windows Restart Manager close the previous UI before replacing files. The separate PulseUpgrade.exe helper has been removed. An active collector blocks replacement instead of being ignored.
- Tray lifetime uses a dispatcher loop instead of a modal dialog, so hiding the window does not end the application.

### Validation / limits

- WPF regression covers tray restore, FPS math/filtering/staleness, six anchors, language/settings, opacity zero, drag and snap. The rebuilt installer completed an in-place upgrade with Bitdefender protection unchanged (exit 0, no reboot). The installed widget reports live sensors. This is one-machine evidence, not an antivirus certification. Real game overlay/click-through, elevated FPS and clean-machine install remain unverified.
- Exclusive fullscreen is not supported by this ordinary topmost overlay. FPS may need admin or Performance Log Users access; Pulse does not alter group membership.
- Bright/dark foreground is based on the selected color; readability over arbitrary desktop content is not guaranteed at zero opacity. Glass blur strength has no slider.

### 简体中文

- 新增 game overlay 六位置、compact/detailed 布局、可选 metrics、PresentMon FPS；AVG/MIN/1% Low 使用最近 60 秒，Current 使用最近 1 秒。
- 新增 close-to-tray、明确 Exit、开机启动 checkbox、background color、0–100% opacity 和可选 update checks。
- Installer 增加覆盖前停止旧版流程；原创代码采用 MIT，SignPath 尚未获批；展示图使用虚构数据。
- WPF regression 已通过；移除独立 upgrade helper 后，installer 在本机成功覆盖安装（exit 0，无需重启），未修改 Bitdefender protection，已验证实时读数。真实游戏、elevated FPS 和 clean-machine 场景待验证。当前 update action 打开 download page，不会自动安装。

## [0.3.1] — 2026-09-13

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

[0.3.1]: https://github.com/medking82/hardware-pulse/releases/tag/v0.3.1
[0.3.0]: https://github.com/medking82/hardware-pulse/releases/tag/v0.3.0
[0.2.0]: https://github.com/medking82/hardware-pulse/releases/tag/v0.2.0
