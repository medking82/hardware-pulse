Hardware Pulse 0.7.0 — Windows x64

请保留完整安装目录并运行 HardwarePulse.exe。UI 已包含 .NET runtime；
worker 需要 Windows .NET Framework 4.8。

worker 及其 Framework libraries 保存在 worker/，不要与 UI libraries 混用。
tools/ 和 licenses/ 包含所需工具及第三方声明。installer 负责 PawnIO 和
startup registration。FPS 需要匹配的已安装 collector；仅复制文件夹不属于受支持的安装方式。

installer 会保留现有 Hardware Pulse preferences。当前 signing 状态见 SIGNING.md；
installer 尚未取得 public certificate trust。
