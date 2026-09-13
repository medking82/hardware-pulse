# Hardware Pulse 0.4.2

Completes Simplified and Traditional Chinese translations for generated hardware descriptions: system and storage temperature, bottom intake/top exhaust, memory slot/configuration metadata, and RAM/VRAM usage. Hardware identifiers and custom names are preserved; known legacy default descriptions now follow the selected language.

VRAM temperature uses clearer Chinese wording and a tooltip explaining memory-chip internal temperature separately from GPU core temperature. The sensor selection is unchanged.

Three-language descriptor checks and the full regression suite passed. The installer remains self-signed; SignPath approval is pending. Check for Updates discovers stable releases but installation remains manual. Game overlay retains its experimental status and existing limitations.

## 简体中文

补齐系统温度、硬盘综合温度、底部进风、顶部排风、内存配置与槽位、RAM/VRAM 用量标签的简繁体翻译，保留硬件型号和自定义名称。

“显存结温”改为更易懂的“显存温度”，tooltip 解释它与 GPU 核心温度的区别，实际 sensor 读取不变。三语言 checks 和完整 regression tests 通过。installer 仍为 self-signed；目前支持检查更新，安装仍需手动完成。
