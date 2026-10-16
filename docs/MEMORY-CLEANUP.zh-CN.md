# Resources 已退役

[English](MEMORY-CLEANUP.md)

Windows Pulse 从 0.6.52 起退役 Resources 与 process cleanup，移除 process list、
grouping、RAM/GPU 关闭建议、normal-close actions 及其 adapter。Pulse 不再提供
RAM/VRAM cleanup，也没有 zombie-process detection。

这次实验没有可靠完成原定 job：正常关闭窗口的 request 不能保证 App 退出或释放
RAM/VRAM，process usage 也无法证明 App 已无用途。UI readability 又反复出现问题，
因此退役此功能，不继续扩展为 forced termination 或针对特定 App 的 system tray control。

CPU/GPU temperatures、RAM/VRAM usage、fans、network、Desktop、FPS 和 quota
继续作为只读 monitoring features。保留已有 settings 与 installer identity；本次退役
不会关闭任何 process，也不会修改 system settings。

旧实现和 release notes 保留在 Git history 与
[v0.6.51](https://github.com/medking82/hardware-pulse/releases/tag/v0.6.51)。
