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
    $panel=$view.Content.Child.Content
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