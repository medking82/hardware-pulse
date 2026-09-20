# Hardware Pulse

[English](README.md) · 简体中文

![Hardware Pulse 界面演示](docs/showcase/pulse-hero.png)

展示图使用真实 WPF 界面、随软件提供的 SVG 图标及虚构演示数据。

[更新记录](CHANGELOG.zh-CN.md) · [操作示意图](docs/QUICKSTART.zh-CN.md)

**[下载 Windows 稳定版 0.6.33](https://github.com/medking82/hardware-pulse/releases/download/v0.6.33/HardwarePulse-Setup.exe)**

另有面向 Windows、Linux、macOS 的 x64／ARM64 [实验性跨平台预发布版 0.7.0-preview.1](https://github.com/medking82/hardware-pulse/releases/tag/v0.7.0-preview.1)，功能尚未与 Windows 稳定版对等，不替代现有安装程序。

### 更新与桌面位置

- 常规支持可选的后台更新检查与下载，校验 GitHub 仓库、文件大小和 SHA-256 后，点击 **安装并重启** 安装；失败或取消可以重试。
- **锁定位置与大小** 禁用移动、调整大小和卡片排序。锁定监控页面的背景不透明度降为原设置的四分之一并关闭模糊；设置保持可读。从托盘或设置解锁后恢复；纯色／高对比度设置优先。这还不是桌面层嵌入。
- 应用程序默认 **自动（跟随系统）**，安装程序也根据 Windows UI 语言预选英语、简体或繁体中文。

当前版本为 **0.6.33**，手动 Refresh 也遵守 server Retry-After 和最低 rate-limit backoff，并修复 credential 更新期间的 login-file sharing。保留现有 WPF UI 和默认 quota source。新增可选的 experimental Claude Code status-line source；需要手动 setup，过期 snapshot 会隐藏，不提供 credential renewal。[Setup 与 rollback](docs/CLAUDE-STATUSLINE.zh-CN.md) · [Integration 与 validation 限制](docs/CLAUDE-QUOTA-RECOVERY.md)。

适用于 **Windows 10 22H2 / Windows 11 x64** 的轻量桌面硬件组件，集中显示 CPU、GPU、内存、NVMe 和风扇读数，以及实时 RAM/VRAM 用量。

采用 WPF 毛玻璃背景、原创 SVG 图标和 Segoe UI 字体排版，支持调整背景不透明度、随窗口宽度缩放，以及保存卡片顺序。运行时不需要 HWiNFO、浏览器、Codex 或云服务；需要 Windows 自带的 .NET Framework 4.8；安装后的应用程序不再依赖 PowerShell 运行环境。

### 使用方式

- **设置 → 语言** 可即时切换英语、简体中文和繁體中文，自动保存选择；默认自动（跟随系统），保留手动选择。设备型号、自定义名称和单位保持原样。

- 拖动卡片标题的六点拖动手柄即可排序，松手保存；Esc 取消当前拖动。
- 右键卡片可选择 **上移／下移**；聚焦拖动手柄后也可按 Shift+F10 打开菜单。
- 拖动 Pulse 标题移动窗口。拖动时及松手后，距离 24 DIP（随 DPI 缩放）内的屏幕边缘或相邻窗口边缘会吸附，包括顶部／底部对齐。按住 Alt 可跳过；窗口不会跟随另一个应用程序移动。
- **实时** 显示当前读数；**本次峰值** 记录本次会话的峰值。
- 点击齿轮按钮打开 **设置**，调整不透明度、纯色背景、放大文字、始终置顶和硬件名称。自定义名称留空时使用自动读取的设备信息。
- 毛玻璃在非活动状态时保持透明；不支持的桌面合成使用纯色回退背景。
- DIMM 和两块 NVMe 采用等宽列。RAM/VRAM 用量在本次峰值中仍实时更新；GB 使用二进制单位，分母为操作系统/驱动报告的可用容量，可能小于实际安装的容量。
- **GPU 风扇转速** 显示的是 RPM 遥测通道，不代表实体风扇的数量。

### 硬件发现与范围

根据同一设备内的传感器名称／类型自动匹配一个 CPU、一个 GPU、最多两条支持温度读取的 DIMM，以及两块 NVMe。多 GPU 环境优先选择 NVIDIA，其次为 AMD 独立显卡，再次为集成显卡。未知或有歧义的读数显示 `—`；电压不会使用 VID 代替。

DIMM 品牌、型号和已安装的槽位来自 SMBIOS。**SPD #1/#3 是传感器地址，不等于 A2/B2**：不能仅凭相同型号，将两条内存的温度传感器对应到实体槽位。已安装的槽位会单独列出；确认传感器对应关系后，再自定义槽位名称。NVMe 盘符只在磁盘型号能唯一匹配时显示；风扇接口名称不能说明风扇实际安装在机箱哪个位置。

目前已在 9700X / RTX 5080 / B850M Mortar 机器上进行实机验证，并保留其已验证的 SYS1/SYS3 映射和原有自定义名称。其他 CPU/GPU 有自动化模拟数据覆盖，但尚未在其他实体机器上验证。

所有传感器访问均为只读；应用程序不调整风扇曲线或 Curve Optimizer。

### 原位升级

可直接安装 0.6.33 覆盖现有版本，无需全新安装。安装程序会停止旧采集器、替换应用程序及其两个启动任务，并清理明确列出的旧脚本/源代码文件。偏好设置和桌面位置与大小保留在 LocalAppData；Windows PowerShell 和共享 PawnIO 保持安装。升级中断或失败时可能需要重新运行安装程序；文件清理不提供事务性回滚。

### 安装程序与自动启动

安装程序会预先检查 .NET Framework 4.8。PawnIO 库或驱动注册缺失时，会自动运行内置的官方安装程序，并在完成后再次检查；失败时不会继续注册自动启动。Windows 自带的组件若缺失或损坏，需要先修复 Windows；安装程序不会自动修改 Windows 功能或安全设置。这些检查验证安装状态，不保证驱动能在所有安全策略下加载。

发行版中的 `HardwarePulse-Setup.exe`（版本 0.6.33）包含应用程序、固定版本的 LibreHardwareMonitor 库、许可声明/源代码归档，以及官方 PawnIO 2.2.0 前置依赖安装包，无需在运行时下载依赖项。目标 Windows 版本自带 .NET Framework 4.8 和 Windows PowerShell 5.1；安装程序会检查 .NET 要求。

请使用当前 Windows 管理员账户安装，并确认 UAC。代码安装到 Program Files；安装程序会注册当前用户的交互式采集器任务，以及普通权限的组件任务。组件在登录后延迟 10 秒启动。卸载会保留共享 PawnIO 和用户设置。此版本不支持使用另一个管理员账户，为标准用户代为安装。

设置保存在 `%LocalAppData%\HardwarePulse`，共享传感器快照保存在 `%ProgramData%\HardwarePulse\<UserSID>\runtime`。后者用于避免软件包独立的 AppData 视图隐藏采集器更新。窗口位置、大小、置顶、不透明度、纯色背景、放大文字和卡片顺序会保留；位置、大小与设置在操作停止 750 ms 后自动保存，不依赖重启前正常关闭窗口。采集器故障会显示为 **过期／离线**。

### 签名

发行版应用程序和安装程序使用 **自签名 Authenticode 证书，CN=Marck Wong**。这不是经公共证书颁发机构验证的发布者身份：Windows/SmartScreen 仍可能拦截或提示，杀毒软件检测也独立于签名。

安装过程不会添加根证书／受信任发布者证书或安全排除项。不可导出的私钥留在作者本机的 Windows 证书存储区，不随软件分发。发行版附带校验值和可供检查的公钥证书；当前自签名构建没有时间戳。参见 [Microsoft 签名选项](https://learn.microsoft.com/windows/apps/package-and-deploy/code-signing-options)。

### 构建、编码与许可证

在 Windows 11 上使用 PowerShell 7 进行构建；PowerShell 7 仅为构建工具，不是运行环境要求。

```powershell
./scripts/Validate.ps1
./scripts/Build.ps1 -Installer
# 可选：使用 CurrentUser\My 中自己的签名证书。
./scripts/Build.ps1 -Installer -SigningCertificateThumbprint <thumbprint>
```

依赖项通过 `dependencies.lock.json` 中的 SHA-256 固定。构建会准备指定的 Inno Setup 编译器，并在不显示控制台窗口的情况下运行子进程。构建产物、下载的依赖项、个人设置和传感器日志不进入 Git。SVG/ICO 在 `assets` 中；运行时使用 WPF 几何图形渲染 SVG 路径。

文本统一为 **UTF-8**。为兼容 Windows PowerShell 5.1，`.ps1` 使用 UTF-8 带 BOM；JSON 显式使用 UTF-8 读写，由 `.editorconfig` 和验证检查相关规则。

第三方源代码位置和声明位于 `licenses`；LibreHardwareMonitor 的完整上游源代码归档随包提供且未修改，依赖项保留各自上游许可证。原创 Hardware Pulse 代码采用 [MIT 许可证](LICENSE)。

### 验证范围

已检查传感器解析回归测试、脚本/XML 验证、原生/安装程序编译、真实 WPF 设置导航、自动保存／恢复、发现模拟数据、用量单位、吸附位置与大小，以及当前机器上的安装。最小窗口为 240 × 340 逻辑像素；高度不足时使用滚动。

Windows 10 兼容性基于 API 基线和回退方案，**尚未进行 Windows 10 实机验证**。全新环境安装、多显示器 DPI 变化、卸载/重新安装，以及新组件任务的下一次完整重启仍未验证。详见 [验证说明](VALIDATION.md)。
