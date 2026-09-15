Add-Type -Path @("$PSScriptRoot\Core\FrameHistory.cs","$PSScriptRoot\FrameCapture.cs")
Add-Type -Path "$PSScriptRoot\GameOverlay.cs" -ReferencedAssemblies @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')
$script:frameCapture=[FrameCapture]::new()
$script:gameOverlay=[GameOverlay]::new()
$script:overlayTarget=$null
$script:overlayState=@{enabled=$false;position='top-left';detail=$false;fps=$true;cpu=$true;gpu=$true;memory=$true;fans=$false;storage=$false}
if($saved.overlay){foreach($key in @($script:overlayState.Keys)){if($null -ne $saved.overlay.$key){$script:overlayState[$key]=$saved.overlay.$key}}}
# Never auto-start an ETW session on login; target selection is explicit each session.
$script:overlayState.enabled=$false
function Get-OverlaySettings { return $script:overlayState }
function Stop-Overlay { if($overlayTimer){$overlayTimer.Stop()};$script:frameCapture.Dispose();$script:gameOverlay.Hide();$script:overlayTarget=$null }
function Refresh-GameList {
    $picker=$window.FindName('GamePicker');$picker.Items.Clear()
    foreach($process in Get-Process | Where-Object {$_.MainWindowHandle -ne 0 -and $_.Id -ne $PID} | Sort-Object ProcessName){
        $item=[Windows.Controls.ComboBoxItem]::new();$item.Content=$process.ProcessName+' · '+$process.Id;$item.Tag=$process.Id;$null=$picker.Items.Add($item)
    }
}
function Start-Overlay {
    Stop-Overlay
    $choice=$window.FindName('GamePicker').SelectedItem
    if(-not $choice -or -not $window.FindName('OverlayEnabled').IsChecked){return}
    try{$script:overlayTarget=Get-Process -Id ([int]$choice.Tag) -ErrorAction Stop}catch{return}
    if($window.FindName('OverlayFps').IsChecked){$script:frameCapture.Start((Join-Path $PSScriptRoot 'tools\PresentMon.exe'),$script:overlayTarget.Id)}
    $overlayTimer.Start()
}
function Overlay-Value($values,$key,$unit) {
    if($values.ContainsKey($key)){return ('{0:0.#}{1}' -f $values[$key],$unit)}
    return '—'
}
function Update-Overlay {
    if(-not $script:overlayTarget -or -not $window.FindName('OverlayEnabled').IsChecked){$script:gameOverlay.Hide();return}
    try{$script:overlayTarget.Refresh();if($script:overlayTarget.HasExited){Stop-Overlay;return}}catch{Stop-Overlay;return}
    $data=Get-PulseSnapshot "$script:runtime\snapshot.json";$v=$data.values;$parts=[Collections.Generic.List[string]]::new()
    if($script:overlayState.fps){
        $frames=$script:frameCapture.Read()
        if($frames.Ready){
            $parts.Add(('FPS {0:0}  AVG {1:0}  MIN {2:0}' -f $frames.Current,$frames.Average,$frames.Minimum))
            $low=if([double]::IsNaN($frames.Low)){'—'}else{'{0:0}' -f $frames.Low}
            $parts.Add('1% LOW '+$low)
        }else{$parts.Add('FPS — · '+(Get-PulseText $frames.Status))}
        $window.FindName('OverlayStatus').Text=Get-PulseText $frames.Status
    }
    if($script:overlayState.cpu){$parts.Add('CPU '+(Overlay-Value $v 'cpu' '°C')+' · '+(Overlay-Value $v 'cpuLoad' '%'))}
    if($script:overlayState.gpu){$parts.Add('GPU '+(Overlay-Value $v 'gpu' '°C')+' · '+(Overlay-Value $v 'gpuLoad' '%'))}
    if($script:overlayState.memory){foreach($kind in @('ram','vram')){
        $usage=if($data.state -eq 'LIVE'){$data.usage[$kind]}else{$null}
        $parts.Add($(if($usage){'{0} {1:0.0}/{2:0.0} GB' -f $(if($usage.label){Get-PulseText $usage.label}else{$kind.ToUpper()}),$usage.used,$usage.total}else{$kind.ToUpper()+' —'}))
    }}
    if($script:overlayState.fans){$parts.Add('FAN CPU '+(Overlay-Value $v 'cpuFan' ' RPM')+' · GPU '+(Overlay-Value $v 'gpuFan' ' RPM'))}
    if($script:overlayState.storage){$parts.Add('NVMe '+(Overlay-Value $v 'diskC' '°C')+' / '+(Overlay-Value $v 'diskD' '°C'))}
    if($script:overlayState.detail){
        $parts.Insert(0,$script:overlayTarget.ProcessName+' · '+(Get-PulseText 'Rolling 60 s'))
        if($script:overlayState.cpu){$parts.Add('Vcore '+(Overlay-Value $v 'vcore' ' V'))}
        if($script:overlayState.gpu){$parts.Add('VRAM '+(Overlay-Value $v 'vram' '°C')+' · '+(Overlay-Value $v 'gpuVolt' ' V'))}
    }
    if($parts.Count -eq 0){$parts.Add('Pulse')}
    $separator=if($script:overlayState.detail){[Environment]::NewLine}else{'   |   '}
    $script:gameOverlay.Display($script:overlayTarget.MainWindowHandle,($parts -join $separator),[string]$script:overlayState.position)
}
foreach($pair in @(@('OverlayEnabled','enabled'),@('OverlayDetailed','detail'),@('OverlayFps','fps'),@('OverlayCpu','cpu'),@('OverlayGpu','gpu'),@('OverlayMemory','memory'),@('OverlayFans','fans'),@('OverlayStorage','storage'))){
    $control=$window.FindName($pair[0]);$control.Tag=$pair[1];$control.IsChecked=[bool]$script:overlayState[$pair[1]]
    $control.Add_Click({param($sender,$args)
        $script:overlayState[[string]$sender.Tag]=[bool]$sender.IsChecked
        if($sender.Tag -in @('enabled','fps')){Start-Overlay}
        Save-WidgetSettings;Update-Overlay
    })
}
$position=$window.FindName('OverlayPosition')
foreach($item in $position.Items){if($item.Tag -eq $script:overlayState.position){$position.SelectedItem=$item}}
if(-not $position.SelectedItem){$position.SelectedIndex=0}
$position.Add_SelectionChanged({$script:overlayState.position=[string]$window.FindName('OverlayPosition').SelectedItem.Tag;Save-WidgetSettings;Update-Overlay})
$window.FindName('RefreshGames').Add_Click({Refresh-GameList})
$window.FindName('GamePicker').Add_SelectionChanged({Start-Overlay})
$window.FindName('ResetFps').Add_Click({Start-Overlay})
$overlayTimer=[Windows.Threading.DispatcherTimer]::new();$overlayTimer.Interval=[TimeSpan]::FromMilliseconds(500);$overlayTimer.Add_Tick({Update-Overlay})
Refresh-GameList
