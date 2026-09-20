param([string]$AppPath,[string]$ScreenshotDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml,System.Windows.Forms
[void][Reflection.Assembly]::LoadFrom((Join-Path $AppPath 'HardwarePulse.exe'))
$icon=[Func[string,double,string,System.Windows.FrameworkElement]]{param($key,$size,$color) $b=New-Object System.Windows.Controls.Border; $b.Width=$size; $b.Height=$size; return $b}
$view=New-Object HardwarePulse.DesktopView($icon,$true)
function Assert($ok,$message){if(-not $ok){throw $message}}
try {
    $view.Width=700
    $items=[Collections.Generic.List[HardwarePulse.DesktopMetric]]::new()
    $items.Add([HardwarePulse.DesktopMetric]::new('fan','Bottom Intake · SYS1 — Chassis Fan','1046 RPM','airflow'))
    $view.Render($items,20,10,'#FFFFFF',$true,1,$null)
    $view.Show();$view.UpdateLayout()
    $panel=$view.Content.Child.Children[1].Content
    $row=$panel.Children[0].Child;$name=$row.Children[1];$value=$row.Children[2]
    $natural=New-Object System.Windows.Controls.TextBlock
    $natural.Text=$name.Text;$natural.FontSize=$name.FontSize;$natural.FontFamily=$name.FontFamily
    $natural.Measure([Windows.Size]::new([double]::PositiveInfinity,[double]::PositiveInfinity))
    Assert ($name.ActualWidth -ge $natural.DesiredSize.Width-1) 'Wide Desktop truncates a name despite spare row width'
    foreach($spec in @(@(280,1),@(430,1),@(960,2),@(1400,3))){
        $view.Width=$spec[0]
        $items.Clear()
        foreach($i in 1..6){$items.Add([HardwarePulse.DesktopMetric]::new("metric$i",'Bottom Intake · SYS1 — 系统进风风扇','27.6 / 61.4 GB · 45%','airflow'))}
        $view.Render($items,20,10,'#FFFFFF',$true,$spec[1],$null)
        $view.UpdateLayout()
        foreach($entry in $panel.Children){
            $grid=$entry.Child;$label=$grid.Children[1];$reading=$grid.Children[2]
            Assert ($label.ActualWidth -gt 0) 'Name lost all available width'
            Assert ($label.TextWrapping -eq 'Wrap') 'Narrow name must wrap instead of hiding content'
            Assert ($label.TextTrimming -eq 'None') 'Desktop name still silently truncates'
            $left=$reading.TranslatePoint([Windows.Point]::new(0,0),$grid).X
            Assert ($left+$reading.ActualWidth -le $grid.ActualWidth+1) 'Reading exceeds row width'
            Assert ($label.TranslatePoint([Windows.Point]::new($label.ActualWidth,0),$grid).X -le $left) 'Name overlaps reading'
        }
    }
    $view.Width=430;$view.Height=200;$view.SizeToContent='Manual';$items.Clear()
    foreach($i in 1..24){$items.Add([HardwarePulse.DesktopMetric]::new("metric$i",'Reading','100','airflow'))}
    $view.Render($items,16,10,'#FFFFFF',$true,0,$null);$view.UpdateLayout()
    $scroll=$view.Content.Child.Children[1]
    Assert ($scroll.ExtentHeight -le $scroll.ViewportHeight+1) 'Locked Desktop leaves avoidable overflow at a saved short height'
    $items.Clear();$items.Add([HardwarePulse.DesktopMetric]::new('fps','FPS',"144 / 128 /  60",'fps'))
    $view.Width=280;$view.Render($items,24,10,'#FFFFFF',$true,1,$null);$view.UpdateLayout()
    $grid=$panel.Children[0].Child;$reading=$grid.Children[2]
    Assert ($reading.Inlines.Count -eq 8 -and $reading.TextWrapping -eq 'NoWrap') 'FPS badges must stay in one reading'
    Assert ($reading.TranslatePoint([Windows.Point]::new($reading.ActualWidth,0),$grid).X -le $grid.ActualWidth+1) 'FPS badges exceed a narrow panel'
    $fpsRuns=@($reading.Inlines)
    $view.UpdateFpsReading('144 / 128 /  60','Live')
    Assert ([object]::ReferenceEquals($fpsRuns[0],$reading.Inlines.FirstInline)) 'Unchanged FPS rebuilt its text runs'
    $view.UpdateFpsReading('240 / 160 /  90','Live updated');$view.UpdateLayout()
    Assert ($reading.Inlines.FirstInline.Text -eq '240' -and $reading.Inlines.Count -eq 8 -and $reading.ToolTip -eq 'Live updated') 'Incremental FPS lost values, badges or tooltip'
    $view.UpdateFpsReading('—','Waiting for frames');$view.UpdateLayout()
    Assert ($reading.Text -eq '—' -and $reading.ToolTip -eq 'Waiting for frames') 'Incremental FPS retained stale readings'
    $view.UpdateFpsReading('144 / 128 /  60','Recovered');$view.UpdateLayout()
    Assert ($reading.Inlines.Count -eq 8) 'FPS recovery did not restore badges'
    $items[0].Value='144 / 128 /  60';$view.Render($items,16,10,'#FFFFFF',$true,1,$null);$view.UpdateLayout()
    Assert ($reading.Inlines.LastInline.FontSize -eq 8.8) 'FPS size change retained old badge font size'
    $items.Clear();$items.Add([HardwarePulse.DesktopMetric]::new('cpu','CPU','55 °C','cpu'))
    $view.Render($items,16,10,'#FFFFFF',$true,1,$null);$view.UpdateFpsReading('999 / 999 / 999','Hidden')
    Assert ($panel.Children.Count -eq 1 -and $panel.Children[0].Child.Children[1].Text -eq 'CPU') 'FPS update restored a hidden row'
    $view.SetEditorLabels('Drag to move','Lock Desktop','Return to App')
    $view.Width=430;$view.Height=260;$items.Clear()
    foreach($i in 1..24){$items.Add([HardwarePulse.DesktopMetric]::new("drag$i",'Reading','100','airflow'))}
    $view.Render($items,16,10,'#FFFFFF',$false,1,$null);$view.UpdateLayout()
    $label=$panel.Children[0].Child.Children[1]
    $point=$label.TranslatePoint([Windows.Point]::new(5,5),$view)
    Assert ($view.DesktopHitTest($point) -eq 2) 'Unlocked ScrollViewer content cannot move the Desktop'
    $editor=$view.Content.Child.Children[0];$button=$editor.Children[1].Children[0]
    Assert ($view.DesktopHitTest($button.TranslatePoint([Windows.Point]::new(5,5),$view)) -eq 0) 'Editor button starts a window drag'
    $bar=$scroll.Template.FindName('PART_VerticalScrollBar',$scroll)
    Assert ($bar.IsVisible -and $view.DesktopHitTest($bar.TranslatePoint([Windows.Point]::new(5,15),$view)) -eq 0) 'Scrollbar starts a window drag'
    Assert ($view.DesktopHitTest([Windows.Point]::new(1,100)) -eq 10) 'Left edge no longer resizes'
    $view.Render($items,16,10,'#FFFFFF',$true,1,$null)
    Assert ($view.DesktopHitTest($point) -eq 0) 'Locked Desktop accepts a window drag'
    if($ScreenshotDirectory){
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($ScreenshotDirectory))
        $view.Width=700;$view.Render($items,20,10,'#FFFFFF',$true,1,$null);$view.SetTextOpacity(100,$true);$view.UpdateLayout()
        $wait=[Diagnostics.Stopwatch]::StartNew();while($wait.ElapsedMilliseconds -lt 300){$view.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background);Start-Sleep -Milliseconds 10}
        $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new([int][Math]::Ceiling($view.ActualWidth),[int][Math]::Ceiling($view.ActualHeight),96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($view);$encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder;$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $file=[IO.File]::Create((Join-Path ([IO.Path]::GetFullPath($ScreenshotDirectory)) 'desktop-content-width.png'))
        try{$encoder.Save($file)}finally{$file.Dispose()}
    }
    'PASS Desktop content widths: spare space, narrow wrapping, no overlap, one/two/three columns'
} finally {$view.Close()}
Add-Type @'
using System;using System.Runtime.InteropServices;
public static class DesktopStyleProbe {
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
 [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y; public Point(int x,int y){X=x;Y=y;}}
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
}
'@
$native=[HardwarePulse.DesktopView]::new($icon,$false)
try{
    $native.Left=60;$native.Top=60;$native.Width=430;$native.Height=350;$native.SizeToContent='Manual'
    $native.Render($items,16,10,'#FFFFFF',$false,1,$null);$native.Show();$native.SetAlwaysOnTop($true)
    $handle=[Windows.Interop.WindowInteropHelper]::new($native).Handle
    Assert (([DesktopStyleProbe]::GetWindowLongPtr($handle,-20).ToInt64() -band 0x08000020) -eq 0) 'New unlocked native editor blocks activation or mouse input'
    $native.Render($items,16,10,'#FFFFFF',$true,1,$null)
    Assert (([DesktopStyleProbe]::GetWindowLongPtr($handle,-20).ToInt64() -band 0x08000020) -eq 0x08000020) 'Locked native panel lost no-activate/click-through'
    $native.Render($items,16,10,'#FFFFFF',$false,1,$null)
    Assert (([DesktopStyleProbe]::GetWindowLongPtr($handle,-20).ToInt64() -band 0x08000020) -eq 0) 'Unlock fails to restore native interaction'
    foreach($opacity in @(0,30)){
        $native.SetTextOpacity(100,$false,$true,$opacity,0);$native.UpdateLayout()
        $wait=[Diagnostics.Stopwatch]::StartNew();while($wait.ElapsedMilliseconds -lt 300){$native.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background);Start-Sleep -Milliseconds 10}
        foreach($point in @([Windows.Point]::new(10,120),[Windows.Point]::new(3,3))){
            $screen=$native.PointToScreen($point)
            $target=[DesktopStyleProbe]::WindowFromPoint([DesktopStyleProbe+Point]::new([int]$screen.X,[int]$screen.Y))
            Assert ($target -eq $handle) "Unlocked Desktop at $opacity percent passes native input through at $point"
        }
    }
    $native.Render($items,16,10,'#FFFFFF',$true,1,$null);$native.SetTextOpacity(100,$false,$true,0,0)
    Assert ($native.Background.Color.A -eq 0 -and $native.Content.Background.Color.A -eq 0) 'Locked zero-opacity Desktop retains an input backdrop'
    'PASS native zero-opacity Desktop: blank drag area, resize corner, locked transparency'
    'PASS native Desktop editor styles: initial unlock, lock, unlock'
}finally{$native.Close()}
