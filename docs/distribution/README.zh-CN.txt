Pulse 桌面预览版

简体中文。英文说明：README.txt。

启动
请选择与操作系统及 CPU 架构（x64 或 ARM64）对应的软件包。
将整个压缩包解压到独立目录，不要混用版本或只复制可执行文件。
Windows：运行 Pulse.Desktop.exe；不会替换现有的 WPF 稳定版。
Linux：在图形桌面中运行 ./Pulse.Desktop。已验证 X11，尚未验证原生 Wayland 交互。
macOS：打开 Pulse Preview.app；移动应用时须保持内部 Contents 目录完整。
软件包自带 .NET 运行环境，但仍需要操作系统提供图形和字体库。

功能
每秒刷新 CPU、内存，以及所选网卡的下载和上传速率。
在 Settings > Network > Refresh interfaces 重新获取网卡列表，无需重启。
已保存的网卡暂时缺席时不会自动切换；重新连接后刷新，或手动选择其他网卡。
Linux 可显示内核公开的温度和风扇通道。无法读取的数据以破折号表示，不伪装成零。
Session Max 显示本次会话的 CPU、所选网卡和 Linux 传感器峰值；内存及额度仍显示当前值。
Codex 额度默认关闭，可手动启用；只读取已有文件登录，不修改凭据。
预览版使用独立设置，保存窗口大小、主题、网卡选择和 Codex 开关。
桌面支持时，托盘或菜单栏提供打开和退出操作；关闭窗口即退出应用。

限制
这是实验性预览版，尚未与 Windows 稳定版实现完整功能对等。
尚未接入 GPU 遥测、FPS、桌面悬浮与点击穿透、模糊和局部自适应对比度、全局快捷键、
开机启动、自动更新及其他额度提供方。
本共享应用目前仅通过 Linux hwmon 支持温度和风扇通道。
Windows CPU 统计要求单处理器组；macOS 内存值为已用内存估计。
不执行安装或自动更新；升级时先关闭应用，再替换解压目录。
Windows 预览版没有发布者签名；macOS 软件包未经 Developer ID 签名或公证。
持续集成中的启动检查不代表已验证下载隔离或 Gatekeeper 行为。请勿为启动而关闭系统安全功能。

校验与许可
解压前可将压缩包的 SHA-256 与附带的校验文件比较；内部清单记录源代码提交和各文件摘要。
Windows/Linux 的许可说明位于 licenses/；macOS 位于应用内的 Contents/Resources/licenses/。
macOS 应用内的 Contents/Resources/ 也保留这两份使用说明，移动应用后仍可查阅。
