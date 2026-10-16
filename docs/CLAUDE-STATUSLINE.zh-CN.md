# Claude Code status-line source（experimental）

[English](CLAUDE-STATUSLINE.md) · [返回 README](../README.zh-CN.md)

适用于 Windows Pulse 0.6.33 及以后版本。**默认推荐 Existing login。** 可选的
CLI snapshot source 从活跃的 Claude Code session 接收 quota observation；它不负责
credential renewal，也无法在 CLI 关闭后独立刷新。Synthetic receiver 和 shell tests
已通过，真实 Claude Code session 的 delivery acceptance 尚未完成。

## Setup

1. 在 Pulse 的 **Settings → AI Quota** 中启用 Claude，选择 experimental
   **CLI snapshot** source。点击 **Copy status-line command**：Claude Code 使用
   Git Bash 时选择 Git Bash；未安装 Git Bash 时选择 PowerShell。复制的 command
   会使用当前 Pulse executable 的实际路径。
2. 编辑前备份 Claude Code 的 user `settings.json`。Windows 默认路径为
   `%USERPROFILE%\.claude\settings.json`；设置了 `CLAUDE_CONFIG_DIR` 时使用对应
   configuration directory。保留其他 settings。如果已有 `statusLine`，先保留其
   完整值；下面的简单 setup 会替换原 status line，不会合并多个 command。
3. 将 `statusLine.type` 设为 `command`，将复制的 command 填入
   `statusLine.command`。下面是完整 JSON 示例，仅适合原本为空的 settings file。
   已有文件只合并 `statusLine` property，不要覆盖整份文件；示例路径需换成实际路径。

Git Bash 示例：

```json
{
  "statusLine": {
    "type": "command",
    "command": "'C:/Program Files/HardwarePulse/HardwarePulse.exe' --claude-statusline"
  }
}
```

PowerShell 示例：

```json
{
  "statusLine": {
    "type": "command",
    "command": "& 'C:/Program Files/HardwarePulse/HardwarePulse.exe' --claude-statusline"
  }
}
```

Clipboard 中是 **shell command，不是 JSON**。保留 command 自身的 quoting；放入
JSON string 时，把双引号转义为 `\"`、反斜杠转义为 `\\`。Executable 路径包含
单引号时，Git Bash command 尤其需要这一步。不要把 JSON 外层引号也放进 command
本身。Pulse 不会自动修改 Claude Code configuration。

4. 保留一个预期使用的 Claude Code session，等待下一次正常 API response 提供
   quota data；无需为了测试专门发起付费 model turn。Pulse 下一次 local poll
   （约 30 秒内）应显示 **CLI snapshot** 和可用的 quota windows。

## 如何判断状态

- 只显示实际提供的 5-hour／weekly windows；缺少数据不等于剩余 100%。仍受 Claude
  account／plan eligibility 和 CLI data availability 限制。
- 同一份 observation 在十分钟后或 window reset 时过期。重复执行相同的 status-line
  input 不会把旧 observation 变成新数据。
- 首次接受的 CLI session 会在本地绑定。更换 session 时，使用 Pulse 的 rebind
  control，然后等待目标 session 的下一份 observation。其他活跃 session 可能先发送，
  因此 rebind 前先关闭不需要的 session。Session binding 不代表 account identity 验证。
- 没有数据时检查 shell、executable path 和 JSON syntax。Project／managed Claude
  settings 可能覆盖 user setting；不要绕过 managed restrictions。Pulse 移动或重装到
  其他目录后，需要重新复制 command 并更新路径。

Receiver 在 `%LOCALAPPDATA%\HardwarePulse\claude-statusline` 保存有大小限制的 quota
observation 和 session fingerprint，不保留原始 status-line input、transcript path、
prompt 或 credentials，也不发送 network request。

## Rollback

在 Claude settings 中恢复原 `statusLine` 值；原本没有时，只移除 Pulse 的
`statusLine` property，保留其他 settings 和有效 JSON。随后在 Pulse 选择
**Existing login**。仅切换 Pulse source 不会移除 Claude 中配置的 command。
无需删除 Claude credential file 或退出登录。

Contract：[Claude Code 官方 status-line 文档](https://code.claude.com/docs/en/statusline)。
Implementation 与 test 限制：[quota recovery investigation](CLAUDE-QUOTA-RECOVERY.md)。
