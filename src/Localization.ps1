$script:translations=@{}
# English source text is the fallback and the stable catalog key.
@'
Live|实时|即時
Auto (System)|自动（系统）|自動（系統）
Details|明细|明細
Cards|卡片|卡片
No cards shown. Choose cards in Settings.|未显示卡片，请在设置中选择。|未顯示卡片，請在設定中選擇。
Show full hardware details|显示完整硬件明细|顯示完整硬體明細
Lock Position and Size|锁定位置与大小|鎖定位置與大小
General|常规|一般
Download Updates Automatically|自动下载更新|自動下載更新
Install and Restart|安装并重启|安裝並重新啟動
Downloading update|正在下载更新|正在下載更新
Update ready to install|更新已下载，等待安装|更新已下載，等待安裝
Update download failed; try again|更新下载失败，请重试|更新下載失敗，請重試
Installation canceled or failed; try again|安装取消或失败，请重试|安裝取消或失敗，請重試
Installer started|安装程序已启动|安裝程式已啟動
Load|负载|負載
Fan|风扇|風扇
Vcore|核心电压|核心電壓
RAM|内存|記憶體
VRAM|显存|顯存
System Temperature|系统温度|系統溫度
Composite Temperature|综合温度|綜合溫度
Bottom Intake|底部进风|底部進風
Top Exhaust|顶部排风|頂部排風
Slots|槽位|插槽
configured|已配置|已設定
Memory-chip internal temperature (junction sensor when available); separate from GPU core temperature.|显存芯片内部温度（支持时读取结温传感器），不同于 GPU 核心温度。|顯存晶片內部溫度（支援時讀取接面溫度感測器），不同於 GPU 核心溫度。
Voltage|电压|電壓
VRAM Temp|显存温度|顯存溫度
Background Color|背景颜色|背景顏色
Text contrast adapts to the selected color. Glass blur is controlled by Windows; Solid Background disables it.|文字对比随颜色调整。玻璃模糊由 Windows 控制；纯色背景可关闭它。|文字對比隨顏色調整。玻璃模糊由 Windows 控制；純色背景可關閉它。
Start with Windows|开机自动启动|開機自動啟動
Available after installation|安装后可用|安裝後可用
Collector start failed; reinstall or check permissions|Collector 启动失败；请重新安装或检查权限|Collector 啟動失敗；請重新安裝或檢查權限
Checks GitHub for new releases. No hardware data is sent.|向 GitHub 检查新版本，不发送硬件数据。|向 GitHub 檢查新版本，不傳送硬體資料。
Startup change failed|启动设置修改失败|啟動設定修改失敗
Startup change canceled or failed|启动设置修改已取消或失败|啟動設定修改已取消或失敗
Check for Updates Automatically|自动检查更新|自動檢查更新
Check for Updates|检查更新|檢查更新
Download Update|下载更新|下載更新
Checking for updates…|正在检查更新…|正在檢查更新…
Update available|有可用更新|有可用更新
You are up to date|已是最新版本|已是最新版本
Update check failed; try again|更新检查失败，请重试|更新檢查失敗，請重試
Game Overlay|游戏状态栏|遊戲狀態列
Windowed / borderless games. FPS capture requires administrator. Stats use application presents, not generated frames.|适用于窗口化 / 无边框游戏。FPS 采集需要管理员权限，统计应用提交帧，不包含生成帧。|適用於視窗化 / 無邊框遊戲。FPS 擷取需要管理員權限，統計應用提交幀，不包含生成幀。
Refresh Games|刷新游戏列表|重新整理遊戲清單
Show Overlay|显示状态栏|顯示狀態列
Detailed Layout|详细布局|詳細配置
Top Left|左上角|左上角
Top|顶部|頂部
Top Right|右上角|右上角
Bottom Left|左下角|左下角
Bottom|底部|底部
Bottom Right|右下角|右下角
AVG / MIN / 1% Low: rolling 60 seconds. MIN is the slowest frame; 1% Low averages the slowest 1%.|AVG / MIN / 1% Low 统计最近 60 秒。MIN 为最慢单帧；1% Low 根据最慢 1% 帧的平均耗时计算。|AVG / MIN / 1% Low 統計最近 60 秒。MIN 為最慢單幀；1% Low 根據最慢 1% 幀的平均耗時計算。
Reset FPS|重置 FPS|重設 FPS
FPS capture stopped|FPS 采集已停止|FPS 擷取已停止
FPS capture failed|FPS 采集失败|FPS 擷取失敗
FPS capture needs administrator|FPS 采集需要管理员权限|FPS 擷取需要管理員權限
Waiting for frames|等待帧数据|等待幀資料
Rolling 60 s|最近 60 秒|最近 60 秒
Show Pulse|显示 Pulse|顯示 Pulse
Exit|退出|結束
Session Max|本次峰值|本次峰值
Settings|设置|設定
Appearance|外观|外觀
Background Opacity|背景不透明度|背景不透明度
Solid Background|纯色背景|純色背景
Larger Text|放大文字|放大文字
Window|窗口|視窗
Always on Top|始终置顶|永遠置頂
Position and size are remembered automatically.|自动记住窗口位置与大小。|自動記住視窗位置與大小。
Hardware Names|硬件名称|硬體名稱
Leave a name blank to use device information. Hover over a field to see its automatic name.|留空以使用设备信息。将指针停在输入框上，可查看自动名称。|留空以使用裝置資訊。將游標停在輸入框上，可查看自動名稱。
SPD numbers identify sensor addresses, not physical slots. Installed slots are reported separately; assign a slot name only after confirming its sensor.|SPD 编号代表传感器地址，并非实体插槽。已安装插槽单独显示；确认对应关系后再命名。|SPD 編號代表感測器位址，並非實體插槽。已安裝插槽單獨顯示；確認對應關係後再命名。
Changes save automatically.|更改自动保存。|變更自動儲存。
About Pulse|关于 Pulse|關於 Pulse
Version 0.4.5 · Marck Wong|版本 0.4.5 · Marck Wong|版本 0.4.5 · Marck Wong
Sensors by LibreHardwareMonitor. Shared driver by PawnIO.|传感器：LibreHardwareMonitor。共享驱动：PawnIO。|感測器：LibreHardwareMonitor。共用驅動程式：PawnIO。
Language|语言 / Language|語言 / Language
Minimize|最小化|最小化
Close widget|关闭组件|關閉小工具
Back to Monitor|返回监控|返回監控
Memory|内存|記憶體
Airflow|散热|散熱
Processor|处理器|處理器
Graphics|显卡|顯示卡
Motherboard|主板|主機板
Utilization|使用率|使用率
Vcore · Motherboard|核心电压 · 主板|核心電壓 · 主機板
VRAM Junction|显存温度|顯存溫度
Core Voltage|核心电压|核心電壓
Fan Speed|风扇转速|風扇轉速
CPU Fan|CPU 风扇|CPU 風扇
System Fan 1|系统风扇 1|系統風扇 1
System Fan 2|系统风扇 2|系統風扇 2
Module 1|内存条 1|記憶體模組 1
Module 2|内存条 2|記憶體模組 2
Drive 1|硬盘 1|磁碟 1
Drive 2|硬盘 2|磁碟 2
NVMe · Composite Temperature|NVMe · 综合温度|NVMe · 綜合溫度
CPU Name|CPU 名称|CPU 名稱
GPU Name|GPU 名称|GPU 名稱
Memory Details|内存信息|記憶體資訊
Module 1 Label|内存条 1 名称|記憶體模組 1 名稱
Module 2 Label|内存条 2 名称|記憶體模組 2 名稱
NVMe Details|NVMe 信息|NVMe 資訊
Drive 1 Name|硬盘 1 名称|磁碟 1 名稱
Drive 2 Name|硬盘 2 名称|磁碟 2 名稱
Case / Motherboard|机箱 / 主板|機殼 / 主機板
CPU Fan Name|CPU 风扇名称|CPU 風扇名稱
Automatic: |自动：|自動：
Drag To Reorder (Esc To Cancel)|拖动排序（Esc 取消）|拖曳排序（Esc 取消）
Move Up|上移|上移
Move Down|下移|下移
sensors|传感器|感測器
Waiting for collector|等待采集器|等待擷取程式
Session peaks|本次峰值|本次峰值
STALE|数据过期|資料過期
OFFLINE|离线|離線
Connecting to sensors…|正在连接传感器…|正在連接感測器…
Current used / usable capacity (GB, binary units). Usage stays live in Session Max.|当前用量 / 可用容量（GB，二进制单位）。本次峰值模式仍显示实时用量。|目前用量 / 可用容量（GB，二進位單位）。本次峰值模式仍顯示即時用量。
GPU Fan 1 / GPU Fan 2 telemetry channels. These do not count physical fans.|GPU 风扇 1 / 2 转速通道，不代表实体风扇数量。|GPU 風扇 1 / 2 轉速通道，不代表實體風扇數量。
System glass background is unavailable on this Windows version.|此 Windows 版本不支持系统玻璃背景。|此 Windows 版本不支援系統玻璃背景。
'@ -split "`r?`n" | ForEach-Object {
    $parts=$_ -split '\|',3
    if($parts.Count -eq 3){$script:translations[$parts[0]]=@($parts[1],$parts[2])}
}
function Resolve-PulseLanguage([string]$language) {
    if($language -eq 'auto'){return Get-PulseSystemLanguage ([Globalization.CultureInfo]::CurrentUICulture.Name)}
    if($language -in @('en','zh-CN','zh-TW')){return $language}
    return 'en'
}
function Get-PulseSystemLanguage([string]$culture) {
    if($culture -match '^zh-(TW|HK|MO|Hant)'){return 'zh-TW'}
    if($culture -match '^zh(?:-|$)'){return 'zh-CN'}
    return 'en'
}
function Get-PulseText([string]$text) {
    if($script:language -eq 'en' -or -not $script:translations.ContainsKey($text)){return $text}
    $index=if($script:language -eq 'zh-TW'){1}else{0}
    return $script:translations[$text][$index]
}
function Get-PulseDeviceText([string]$text) {
    # Translate known generated descriptors, never arbitrary model substrings.
    $result=Get-PulseText $text
    foreach($phrase in @('System Temperature','Composite Temperature','Bottom Intake','Top Exhaust','Slots','configured')){
        $pattern='(?<![\p{L}\p{N}])'+[regex]::Escape($phrase)+'(?![\p{L}\p{N}])'
        $result=[regex]::Replace($result,$pattern,(Get-PulseText $phrase))
    }
    return $result
}
# Capture original UI strings once. Device names and user input are deliberately excluded.
function Register-PulseText($node) {
    if($node -isnot [Windows.DependencyObject]){return}
    if($script:labels.Values -contains $node -or $node -is [Windows.Controls.TextBox]){return}
    foreach($property in @('Text','Content','Header','ToolTip')){
        if($node.PSObject.Properties[$property] -and $node.$property -is [string] -and $script:translations.ContainsKey($node.$property)){
            $script:localizedControls.Add(@($node,$property,[string]$node.$property))
        }
    }
    foreach($child in [Windows.LogicalTreeHelper]::GetChildren($node)){Register-PulseText $child}
}
function Update-PulseLanguage {
    if($script:trayShow){$script:trayShow.Text=Get-PulseText 'Show Pulse';$script:trayExit.Text=Get-PulseText 'Exit';$script:traySettings.Text=Get-PulseText 'Settings';$script:trayPin.Text=Get-PulseText 'Always on Top';$script:trayLock.Text=Get-PulseText 'Lock Position and Size'}
    foreach($entry in $script:localizedControls){$entry[0].($entry[1])=Get-PulseText $entry[2]}
    foreach($name in @('Settings','Back','Minimize','Close','OpacitySlider','LanguagePicker')){
        $node=$window.FindName($name)
        $key=switch($name){'Back'{'Back to Monitor'} 'Close'{'Close widget'} 'OpacitySlider'{'Background Opacity'} 'LanguagePicker'{'Language'} default{$name}}
        [Windows.Automation.AutomationProperties]::SetName($node,(Get-PulseText $key))
    }
    foreach($key in $script:nameEditors.Keys){
        [Windows.Automation.AutomationProperties]::SetName($script:nameEditors[$key],(Get-PulseText $script:nameEditorTitles[$key]))
    }
    foreach($card in $cards.Children){
        $first=$card.Child.Children[0]
        $heading=if($first -is [Windows.Controls.Grid]){$first.Children[0]}else{$first}
        [Windows.Automation.AutomationProperties]::SetName($heading.Children[0],((Get-PulseText 'Drag To Reorder (Esc To Cancel)')+' · '+(Get-PulseText $card.Tag)))
        foreach($item in $script:orderButtons[[string]$card.Tag]){
            $key=if($item.Tag[1] -lt 0){'Move Up'}else{'Move Down'}
            $item.Header=Get-PulseText $key;$item.ToolTip=$item.Header+' · '+(Get-PulseText $card.Tag)
            [Windows.Automation.AutomationProperties]::SetName($item,$item.ToolTip)
        }
    }
    Update-DeviceNames
    Set-Material
    if(Get-Command Apply-Theme -ErrorAction SilentlyContinue){Apply-Theme}
    Update-Panel
}
