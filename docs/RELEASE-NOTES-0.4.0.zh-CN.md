# Hardware Pulse 0.4.0 发行说明

[English](RELEASE-NOTES-0.4.0.md)

在发行页面的附件中下载 **HardwarePulse-Setup.exe**。目标平台为 Windows 10 22H2 / Windows 11 x64，安装包包含应用程序库、PresentMon 和 PawnIO 安装程序，并检查 Windows 自带的 .NET Framework 4.8 与 PowerShell 5.1，无需 HWiNFO。
本版移除了被 Bitdefender 隔离的独立升级辅助程序，改由 Inno Setup 直接准备升级。本机覆盖安装已成功，退出码 0，无需重启，也未修改 Bitdefender 防护设置。新增紧凑卡片、自定义滚动条、卡片显示开关、托盘、自动启动、0–100% 不透明度和实验性游戏悬浮层。
这是供测试反馈的 **预发布版**，并非所有机器都已验证。安装程序目前为自签名，SignPath 尚未批准，Windows 仍可能显示发布者／信誉提示。真实游戏、全新环境和重启场景还需测试；FPS 不统计生成帧，独占全屏不支持。可在 [GitHub Issues](https://github.com/medking82/hardware-pulse/issues) 提交复现步骤，分享截图前请自行隐藏个人信息。
