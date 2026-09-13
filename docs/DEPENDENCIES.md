# Dependency maintenance and platform roadmap

Hardware Pulse pins upstream release versions and SHA-256 digests in `dependencies.lock.json`. Builds consume those releases; installed clients do not pull Git repositories or replace dependencies from upstream branches.

## Maintenance policy

1. Check upstream stable releases before each Pulse release. Prioritize security fixes and hardware compatibility regressions.
2. Update the exact version, official download URL and checksum together. Retain upstream licenses, source-distribution requirements and signatures.
3. Validate sensor identity/readings, resource usage, complete exit, installation and upgrade. Validate FPS parsing when PresentMon changes.
4. Ship the verified dependency set through a versioned Hardware Pulse installer. Keep previous release assets available for recovery.

LibreHardwareMonitor is the sensor library; PawnIO is the low-level Windows driver dependency. PresentMon provides Windows frame telemetry. Build tooling such as Inno Setup is maintained separately from runtime components. An automatic upstream-check/PR workflow is not yet implemented.

## Platform roadmap

Windows 10 22H2 / Windows 11 x64 is the current target. Linux and macOS support is planned, not implemented or validated. Each needs a platform-specific sensor/capture backend and a compatible UI implementation; Windows drivers and WPF cannot simply be repackaged for those systems. Shared metric definitions, settings semantics and visual design should be preserved where practical. Unsupported sensors must be shown as unavailable rather than fabricated.

## 简体中文

Dependency 使用固定 release version 和 SHA-256；用户机器不会持续 `git pull` 或自动替换 upstream dependency。更新在开发环境完成，经过 sensor、退出、installer upgrade 和相关 FPS regression 验证后，随 Hardware Pulse 新版本发布。保留 upstream license、必要 source 和 signature。

Linux / macOS 属于 planned support，需要各自的 backend 和 UI 适配；当前不宣称支持。自动 upstream-check / PR workflow 尚未实现。
