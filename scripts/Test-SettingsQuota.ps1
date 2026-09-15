param([string]$AppPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,System.Windows.Forms
$app=[IO.Path]::GetFullPath($AppPath)
[void][Reflection.Assembly]::LoadFrom((Join-Path $app 'HardwarePulse.exe'))
$compiler=[CodeDom.Compiler.CompilerParameters]::new();[void]$compiler.ReferencedAssemblies.Add((Join-Path $app 'HardwarePulse.exe'));[void]$compiler.ReferencedAssemblies.Add((Join-Path $app 'Pulse.Core.dll'));[void]$compiler.ReferencedAssemblies.Add((Join-Path $app 'Pulse.Adapters.Windows.dll'));[void]$compiler.ReferencedAssemblies.Add('System.Web.Extensions.dll')
Add-Type -CompilerParameters $compiler -TypeDefinition @'
using System;using System.Threading;using HardwarePulse;
public static class SettingsQuotaFixture {
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
 [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr Style(IntPtr hwnd,int index);
 public static int Hit(IntPtr hwnd,int x,int y){return SendMessage(hwnd,0x84,IntPtr.Zero,new IntPtr((y<<16)|(x&65535))).ToInt32();}
 public static QuotaReading Read(string provider,CancellationToken cancel){
  string json=provider=="Codex"?"{\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":10},\"secondary\":{\"windowDurationMins\":10080,\"usedPercent\":40}},\"A very long extra model quota name\":{\"primary\":{\"windowDurationMins\":300,\"usedPercent\":0},\"secondary\":{\"windowDurationMins\":10080,\"usedPercent\":3}}}}":provider=="Antigravity"?"{\"groups\":[{\"displayName\":\"Gemini Models\",\"buckets\":[{\"window\":\"session\",\"remainingFraction\":0.9},{\"window\":\"weekly\",\"remainingFraction\":0.8}]},{\"displayName\":\"Claude and GPT Models\",\"buckets\":[{\"window\":\"session\",\"remainingFraction\":0.7},{\"window\":\"weekly\",\"remainingFraction\":0.6}]}]}":"{\"five_hour\":{\"utilization\":12},\"seven_day\":{\"utilization\":20},\"seven_day_sonnet\":{\"utilization\":30}}";
  return QuotaData.Decode(provider,QuotaData.Parse(json),DateTimeOffset.UtcNow);
 }
}
'@
$state=Join-Path (Split-Path $PSScriptRoot) ('vendor/settings-quota-'+[Guid]::NewGuid().ToString('N'))
$paths=[HardwarePulse.PulsePaths]::new($app,$state,(Join-Path $state 'runtime'));$shell=[HardwarePulse.Shell]::new($paths,$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Assert($condition,$message){if(-not $condition){throw $message}}
function InvokeShell($method){[void]$shell.GetType().GetMethod($method,$flags).Invoke($shell,@())}
function Pump {$shell.Window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background)}
function Settle {$timer=[Diagnostics.Stopwatch]::StartNew();while($timer.ElapsedMilliseconds -lt 250){Pump;Start-Sleep -Milliseconds 10}}
function Toggle($name,$enabled){$control=$shell.Window.FindName($name);$control.IsChecked=$enabled;$control.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Primitives.ButtonBase]::ClickEvent));Pump}
function Capture($name){Settle;$window=$shell.Window;$bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new([int]$window.ActualWidth,[int]$window.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($window);$png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$stream=[IO.File]::Create((Join-Path $state $name));try{$png.Save($stream)}finally{$stream.Dispose()}}
try {
 $shell.Show();Pump;$settings=$shell.GetType().GetField('settings',$flags).GetValue($shell)
 Assert ($shell.Window.FindName('DesktopAppIconColors').IsChecked) 'New default must follow App icon colors'
 $delegate=[Delegate]::CreateDelegate([Func[string,Threading.CancellationToken,HardwarePulse.QuotaReading]],[SettingsQuotaFixture].GetMethod('Read'))
 $session=[HardwarePulse.QuotaSession]::new($delegate);$field=$shell.GetType().GetField('quotas',$flags);$field.GetValue($shell).Dispose();$field.SetValue($shell,$session)
 foreach($provider in [HardwarePulse.QuotaSession]::Providers){Toggle ('Quota'+$provider) $true}
 $session.Tick([DateTimeOffset]::UtcNow);foreach($i in 1..100){$session.Tick([DateTimeOffset]::UtcNow);if(@($session.Readings|Where-Object Status -ne 'Live').Count -eq 0){break};Start-Sleep -Milliseconds 10}
 $codex=@($session.Readings|Where-Object Provider -eq 'Codex')[0];$ag=@($session.Readings|Where-Object Provider -eq 'Antigravity')[0]
 Assert ($codex.Windows.Count -eq 1 -and $codex.AllWindows.Count -eq 4) 'Codex complete limits lost or compact changed'
 Assert ($ag.Windows.Count -eq 2 -and $ag.AllWindows.Count -eq 4) 'Antigravity complete pools lost or compact changed'
 $shell.UpdatePanel();Pump;$panel=$shell.Window.FindName('QuotaCards');Assert ($panel.Children.Count -eq 3) 'Missing quota cards'
 $shell.Window.FindName('QuotaDisplay').SelectedIndex=1;$shell.UpdatePanel();Settle
 Assert ($panel.Children[0].Child.Children.Count -gt 6) 'Full quota mode did not render extra limits'
 $card=$panel.Children[0];$grip=$card.Child.Children[0].Children[0]
 Assert ($grip -is [Windows.Controls.Primitives.Thumb]) 'Quota card has no drag handle'
 $key=[Windows.Input.KeyEventArgs]::new([Windows.Input.Keyboard]::PrimaryDevice,[Windows.PresentationSource]::FromVisual($shell.Window),0,[Windows.Input.Key]::Right);$key.RoutedEvent=[Windows.Input.Keyboard]::PreviewKeyDownEvent;$grip.RaiseEvent($key);Pump
 Assert ($panel.Children[1].Tag -eq 'Codex') 'Quota keyboard reorder failed'
 $shell.GetType().GetField('quotaSignature',$flags).SetValue($shell,$null);InvokeShell RenderQuota
 Assert ($panel.Children[1].Tag -eq 'Codex') 'Quota refresh lost order'
 $shell.Window.Width=340;$shell.Window.Height=900;$shell.UpdatePanel();Capture 'quota-full-narrow.png'
 $shell.ShowSettings($true);$shell.Window.FindName('AppearanceSection').IsExpanded=$true;$shell.Window.FindName('DesktopAppearanceSection').IsExpanded=$true
 foreach($size in @(@(340,1),@(800,2),@(1200,3))){$shell.Window.Width=$size[0];$shell.Window.Height=900;InvokeShell ApplyMaterial;Settle;Assert ($shell.Window.FindName('SettingsSections').Columns -eq $size[1]) 'Settings did not adapt columns';Capture ('settings-'+$size[1]+'.png')}
 $page=$shell.Window.FindName('SettingsPage');Assert ($page.FontSize -ge 14) 'Settings text inherits unreadably small monitor font'
 Assert ($shell.Window.FindName('LanguagePicker').VerticalContentAlignment -eq 'Center') 'ComboBox selected text is not centered vertically'
 $page.ScrollToEnd();Settle;Assert ($shell.Window.FindName('Back').IsVisible) 'Back action scrolls away'
 foreach($item in $shell.Window.FindName('LanguagePicker').Items){if($item.Tag -eq 'zh-CN'){$shell.Window.FindName('LanguagePicker').SelectedItem=$item}};Capture 'settings-zh.png'
 Assert ($shell.Window.FindName('HardwareReadingColors').Foreground.Color.R -gt 100) 'Dark settings radio foreground remains black'
 $settings.Data['background']='#FFFFFF';InvokeShell ApplyMaterial;Capture 'settings-light.png';Assert ($shell.Window.FindName('HardwareReadingColors').Foreground.Color.R -lt 100) 'Light settings radio foreground remains white'
 $shell.GetType().GetMethod('SelectSettingsCategory',$flags).Invoke($shell,@('Desktop'));Pump
 foreach($name in @('DesktopSection','DesktopAppearanceSection')){Assert ($shell.Window.FindName($name).IsVisible) 'Desktop controls split across categories'}
 Assert (-not $shell.Window.FindName('AppearanceSection').IsVisible) 'App appearance leaked into Desktop category'
 foreach($size in @(340,1200)){$shell.Window.Width=$size;$shell.Window.Height=900;Capture ('desktop-settings-'+$size+'.png')}
 $shell.ShowSettings($false);Toggle 'DesktopEnabled' $true
 $desktop=$shell.GetType().GetField('desktop',$flags).GetValue($shell);Assert (-not $desktop.Locked) 'Desktop enable did not enter editor'
 $settings.Map('desktopVisible')['fps']=$true;Toggle 'OverlayEnabled' $false;InvokeShell StartOverlay;InvokeShell UpdateDesktop
 Assert ($shell.GetType().GetProperty('DesktopFpsActive',$flags).GetValue($shell)) 'Desktop FPS depends on overlay switch'
 $metrics=$shell.GetType().GetMethod('DesktopMetrics',$flags).Invoke($shell,@());Assert (@($metrics|Where-Object Key -eq 'fps').Count -eq 1) 'Desktop FPS reading missing'
 Assert (-not $shell.Window.FindName('OverlayEnabled').IsChecked) 'Desktop FPS enables separate overlay'
 $setFps=$shell.GetType().GetMethod('SetDesktopFps',$flags);$sample=[FrameMetrics]::new();$sample.Ready=$true;$sample.Current=144;$sample.Average=128;$sample.Minimum=60;$sample.Status='Live'
 $setFps.Invoke($shell,@($sample));InvokeShell UpdateDesktop
 $fpsRows=$desktop.GetType().GetField('rows',$flags).GetValue($desktop)
 Assert ($fpsRows['fps'].IconColor -eq '#9EDFD3') 'FPS SVG does not use Pulse mint'
 Toggle 'DesktopAppIconColors' $false;InvokeShell UpdateDesktop
 Assert ($fpsRows['fps'].IconColor -ne '#9EDFD3') 'FPS SVG ignores text color mode'
 Toggle 'DesktopAppIconColors' $true;InvokeShell UpdateDesktop
 Assert ($fpsRows['fps'].Value.Text.Replace([string][char]0x2007,'') -eq '144 NOW  128 AVG  60 MIN' -and $fpsRows['fps'].Value.TextWrapping -eq 'NoWrap') 'FPS statistics are missing or wrap'
 $slotWidth=$fpsRows['fps'].Value.ActualWidth
 $slotHeight=$fpsRows['fps'].Border.ActualHeight
 foreach($fps in @(99,9,100)){$sample.Current=$fps;$setFps.Invoke($shell,@($sample));InvokeShell UpdateDesktop;$desktop.UpdateLayout();Assert ([Math]::Abs($fpsRows['fps'].Value.ActualWidth-$slotWidth) -lt 1 -and $fpsRows['fps'].Value.TextAlignment -eq 'Right') 'FPS digit-count changes move the numeric slot'}
 $sample.Ready=$false;$sample.Status='Waiting for frames';$setFps.Invoke($shell,@($sample));InvokeShell UpdateDesktop
 $desktop.UpdateLayout();Assert ([Math]::Abs($fpsRows['fps'].Border.ActualHeight-$slotHeight) -lt .1) 'FPS ready/waiting transition changes row height'
 foreach($key in @('fps')){Assert ($fpsRows[$key].Value.Text -eq '—') 'Waiting FPS leaks status into reading'}
 $settings.Map('desktopVisible')['fps']=$false;InvokeShell StartOverlay;InvokeShell UpdateDesktop
 $shell.Window.Hide();InvokeShell ToggleDesktopFromShortcut;Assert (-not $settings.Flag('desktopEnabled') -and -not $shell.Window.IsVisible) 'Shortcut hide opens App'
 InvokeShell ToggleDesktopFromShortcut;Assert ($settings.Flag('desktopEnabled') -and $settings.Flag('desktopLocked') -and -not $shell.Window.IsVisible) 'Shortcut show steals App focus or leaves Desktop unlocked'
 InvokeShell EditDesktop;$desktop=$shell.GetType().GetField('desktop',$flags).GetValue($shell)
 $controls=$desktop.Content.Child.Children[0].Children[1].Children
 Assert ($controls[0].IsVisible -and $controls[0].Content -eq '锁定桌面') 'Editor has no visible Lock button'
 $controls[0].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent));Pump
 Assert ($desktop.Locked -and -not $controls[0].IsVisible) 'Lock action leaves editing controls visible'
 InvokeShell EditDesktop;Assert (-not $desktop.Locked -and -not $shell.Window.IsVisible) 'Edit Desktop still requires Settings'
 $point=$desktop.PointToScreen([Windows.Point]::new(1,50));$handle=[Windows.Interop.WindowInteropHelper]::new($desktop).Handle
 Assert ([SettingsQuotaFixture]::Hit($handle,[int]$point.X,[int]$point.Y) -eq 10) 'Native window hook does not expose left resize edge'
 $controls[0].RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent));Pump
 Assert ([SettingsQuotaFixture]::Hit($handle,[int]$point.X,[int]$point.Y) -ne 10) 'Locked Desktop still permits edge resizing'
 foreach($edge in @(@(0,50,10),@(400,50,11),@(50,0,12),@(0,0,13),@(400,0,14),@(50,200,15),@(0,200,16),@(400,200,17),@(50,50,0))){Assert ([HardwarePulse.DesktopView]::ResizeEdge([Windows.Point]::new($edge[0],$edge[1]),[Windows.Size]::new(400,200),8) -eq $edge[2]) 'Desktop resize edge mapping wrong'}
 Assert (-not $settings.Flag('desktopAlwaysOnTop')) 'Topmost must be opt-in'
 $nativeWindow=[Windows.Window]::new();$nativeWindow.Width=200;$nativeWindow.Height=100;$nativeWindow.WindowStyle="None";$nativeWindow.AllowsTransparency=$true;$nativeWindow.ShowActivated=$false;$nativeWindow.ShowInTaskbar=$false;$nativeWindow.Show();$nativeLayer=[HardwarePulse.DesktopLayer]::new($nativeWindow)

 try {
  Toggle 'DesktopAlwaysOnTop' $true;Toggle 'DesktopLocked' $true;Settle;$nativeLayer.SetAlwaysOnTop($true);$nativeLayer.SetLocked($true);$shell.Save()
  $reloaded=[HardwarePulse.Settings]::new((Join-Path $state 'widget-settings.json'));Assert ($reloaded.Flag('desktopAlwaysOnTop')) 'Topmost preference not persisted'
  $handle=[Windows.Interop.WindowInteropHelper]::new($nativeWindow).Handle;$style=[SettingsQuotaFixture]::Style($handle,-20).ToInt64()
  Assert (($style -band 8) -ne 0 -and ($style -band 32) -ne 0 -and ($style -band 0x08000000) -ne 0 -and ($style -band 0x80000) -ne 0) ('Topmost style=0x{0:X}, layer={1}, current={2}, attached={3}, flag={4}' -f $style,$nativeLayer.GetType().GetField('hwnd',$flags).GetValue($nativeLayer),$handle,$nativeLayer.Attached,$settings.Flag('desktopAlwaysOnTop'))
  $surface=$desktop.Content;$stack=$desktop.GetType().GetField('stack',$flags).GetValue($desktop)
  Assert ($surface.Background.Color.R -eq 245 -and $surface.Background.Color.A -eq 140 -and $stack.Opacity -eq 1 -and $null -eq $stack.Effect) 'Topmost smoke background or sharp opaque text incorrect'
  Assert ($surface.VerticalAlignment -eq 'Top') 'Locked topmost background stretches into empty space'
  foreach($alpha in @(0,15,55,100)){
   $shell.Window.FindName('DesktopOverlayOpacity').Value=$alpha
   $shell.Window.FindName('DesktopTextOpacity').Value=$alpha
   Assert ($surface.Background.Color.A -eq [Math]::Round(255*$alpha/100) -and [Math]::Abs($stack.Opacity-$alpha/100) -lt .001) 'Topmost opacity does not match slider'
   foreach($auto in @($false,$true)){Toggle 'DesktopAutoContrast' $auto;Assert ($shell.Window.FindName('DesktopTextOpacity').Value -eq $alpha -and [Math]::Abs($stack.Opacity-$alpha/100) -lt .001) 'Auto Contrast overrides user opacity'}
  }
  $shell.Window.FindName('DesktopTextOpacity').Value=15;$shell.Window.FindName('DesktopOverlayOpacity').Value=0;$shell.Save()
  Assert ($surface.BorderBrush.Color.A -eq 0) 'Zero-opacity locked panel leaves an outer frame'
  Assert ($shell.Window.FindName('DesktopColor').IsEnabled) 'Auto Contrast prevents choosing a custom text color'
  $opacitySaved=[HardwarePulse.Settings]::new((Join-Path $state 'widget-settings.json'));Assert ($opacitySaved.Number('desktopTextOpacity',100,0,100) -eq 15 -and $opacitySaved.Number('desktopOverlayOpacity',55,0,100) -eq 0) 'Low opacity does not survive reload'
  $shell.Window.FindName('DesktopTextOpacity').Value=100
  $shell.Window.FindName('DesktopOverlayOpacity').Value=65
  Assert ($surface.Background.Color.A -eq 166) 'Topmost opacity slider not applied'
  Settle
  foreach($background in @('White','Black')){
   $visual=[Windows.Media.DrawingVisual]::new();$dc=$visual.RenderOpen();$rect=[Windows.Rect]::new(0,0,$desktop.ActualWidth,$desktop.ActualHeight)
   $dc.DrawRectangle([Windows.Media.Brushes]::$background,$null,$rect);$dc.DrawRectangle([Windows.Media.VisualBrush]::new($desktop.Content),$null,$rect);$dc.Close()
   $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new([int]$desktop.ActualWidth,[int]$desktop.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($visual)
   $png=[Windows.Media.Imaging.PngBitmapEncoder]::new();$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$stream=[IO.File]::Create((Join-Path $state ('topmost-'+$background+'.png')));try{$png.Save($stream)}finally{$stream.Dispose()}
  }
  Toggle 'DesktopLocked' $false;$nativeLayer.SetLocked($false);$handle=[Windows.Interop.WindowInteropHelper]::new($nativeWindow).Handle;$style=[SettingsQuotaFixture]::Style($handle,-20).ToInt64()
  Assert (($style -band 8) -ne 0 -and ($style -band 0x08000020) -eq 0) 'Editing must retain topmost and allow activation and mouse input'
  Toggle 'DesktopAlwaysOnTop' $false;Toggle 'DesktopLocked' $true;$nativeLayer.SetAlwaysOnTop($false);$nativeLayer.SetLocked($true)
  $shell.Window.FindName('DesktopTextOpacity').Value=15;Toggle 'DesktopAutoContrast' $false;Toggle 'DesktopAutoContrast' $true;Assert ([Math]::Abs($stack.Opacity-.15) -lt .001) 'Desktop Auto Contrast changes text opacity'
  Assert ($surface.VerticalAlignment -eq 'Stretch') 'Desktop geometry not restored'
  $shell.Window.FindName('DesktopBackgroundOpacity').Value=40;Assert ($surface.Background.Color.A -eq 102) 'Desktop background opacity did not apply'
  Assert (([SettingsQuotaFixture]::Style($handle,-20).ToInt64() -band 8) -eq 0) 'Returning to Desktop retained topmost'
 } finally {$nativeLayer.Dispose();$nativeWindow.Close()}
 Toggle 'DesktopAppIconColors' $false;$shell.Save();Assert (-not $settings.Flag('desktopAppIconColors',$true)) 'Explicit icon override not retained'
 $saved=[HardwarePulse.Settings]::new((Join-Path $state 'widget-settings.json'));Assert ($saved.Flag('quotaFull') -and -not $saved.Flag('desktopAppIconColors',$true) -and $saved.Data['quotaCardOrder'][1] -eq 'Codex') 'Quota mode/order or icon override did not persist'
 'PASS Settings/quota: full vs compact, handles/order, 1/2/3 columns, languages, contrast, sticky Back, editor Lock and eight resize edges'
 'Screenshots: '+$state
} finally {$shell.Exit()}
