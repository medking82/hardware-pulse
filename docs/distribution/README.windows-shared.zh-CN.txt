Hardware Pulse — Windows x64

此 package 使用 shared App/Desktop components 和独立 Windows hardware collector。
需要 Windows 10 22H2 / Windows 11 x64 及 .NET Framework 4.8。
App 使用的现代 .NET runtime 已随附。installer 管理 collector 和 PawnIO prerequisite，
请勿通过复制个别文件覆盖 installation。

version 含 -rc 或 -preview 即为 prerelease，不代表 stable。
安装后的 stable build 导入可识别的 0.6.27 preferences，保留原 profile。
development/prerelease build 使用独立 preview profile。
quota 读取需要主动启用，并依赖 provider login 的可用性。

tray 可用时，关闭窗口会隐藏 App；通过 tray 重新打开或 Quit。
如果 setup 报告 installation 未完成，请保留 setup log，解决报告的 prerequisite/startup
错误后重新运行 setup。post-install failure 可能已更新文件，不代表旧 installation 已恢复。

data、signing 与 dependency 说明见 PRIVACY.md、SIGNING.md 和 licenses。
