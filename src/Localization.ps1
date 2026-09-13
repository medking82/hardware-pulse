$script:translations=@{}
# English source text is the fallback and the stable catalog key.
@'
Live|实时|即時
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
Version 0.3.0 · Marck Wong|版本 0.3.0 · Marck Wong|版本 0.3.0 · Marck Wong
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
Vcore · Motherboard|Vcore · 主板|Vcore · 主機板
VRAM Junction|显存结温|顯存接面溫度
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
    if($language -in @('en','zh-CN','zh-TW')){return $language}
    return 'en'
}
function Get-PulseText([string]$text) {
    if($script:language -eq 'en' -or -not $script:translations.ContainsKey($text)){return $text}
    $index=if($script:language -eq 'zh-TW'){1}else{0}
    return $script:translations[$text][$index]
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
    Update-Panel
}
