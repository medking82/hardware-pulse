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

## Next platform work: boundary inventory (2026-09-15)

The current Desktop topmost iteration is Windows-only. Platform work follows it;
these are implementation prerequisites, not supported download targets.

| Owner today | Reuse / required boundary |
| --- | --- |
| `ReadingSession.cs`, `SensorProfile.cs`, `NetworkRate.cs` | Reuse reading state, capability handling and formatting; isolate snapshot IO/JSON first. |
| `Models.cs`, `Settings.cs`, `QuotaData.cs` | Keep data contracts and parser behavior; `System.Web.Script.Serialization` is a .NET Framework dependency to replace behind tested serialization semantics. |
| `Collector.cs` / `PulsePaths` | Separate Windows SID paths, WMI, LibreHardwareMonitor and driver ownership from portable models. |
| `QuotaProviders.cs`, `AntigravityQuota.cs` | Windows Credential Manager, Windows process identity and TCP ownership discovery need platform implementations. Quota parsing alone does not make account discovery portable. |
| `Shell`, `DesktopLayer`, startup, tray and FPS | WPF / Win32 surfaces remain Windows-owned. Prototype a cross-platform UI separately before choosing migration scope. |

Sequence: establish shared reading/serialization tests, then build a Windows ARM64
prototype and validate dependencies on ARM hardware. Linux/macOS start with CPU
load, memory and network; account quota requires working per-platform discovery.
Temperature, fans, FPS and desktop embedding need explicit capability validation.
Do not replace the working Windows installer with an unvalidated prototype.

Environment observation: this Windows host has `dotnet.exe`, but `dotnet --list-sdks`
returned no SDKs on 2026-09-15. ARM64, Linux and macOS runtime checks have not run.
Install/select a modern .NET SDK and establish target build/test environments before
claiming a portable build. No WSL checkout or new framework was introduced here.

## 简体中文维护说明

Dependency 使用固定 release version 和 SHA-256；用户机器不会持续 `git pull` 或自动替换 upstream dependency。更新在开发环境完成，经过 sensor、退出、installer upgrade 和相关 FPS regression 验证后，随 Hardware Pulse 新版本发布。保留 upstream license、必要 source 和 signature。

Linux / macOS 属于 planned support，需要各自的 backend 和 UI 适配；当前不宣称支持。自动 upstream-check / PR workflow 尚未实现。
