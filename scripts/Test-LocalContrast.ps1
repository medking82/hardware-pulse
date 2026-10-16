param([string]$AppPath='build/native/app')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
Add-Type 'public static class ContrastTestComposition { [System.Runtime.InteropServices.DllImport("dwmapi.dll")] public static extern int DwmFlush(); }'
[void][Reflection.Assembly]::LoadFrom((Join-Path ([IO.Path]::GetFullPath($AppPath)) 'HardwarePulse.exe'))
function Settle { $watch=[Diagnostics.Stopwatch]::StartNew();while($watch.ElapsedMilliseconds -lt 200){[Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background);Start-Sleep -Milliseconds 10} }
function Assert($condition,$message){if(!$condition){throw $message}}
$behind=[Windows.Window]::new();$front=[Windows.Window]::new();$capture=$null
try {
 foreach($window in @($behind,$front)){$window.WindowStyle='None';$window.ResizeMode='NoResize';$window.ShowInTaskbar=$false;$window.WindowStartupLocation='Manual';$window.Left=150;$window.Top=150;$window.Width=300;$window.Height=150;$window.Topmost=$true}
 $grid=[Windows.Controls.Grid]::new();$grid.ColumnDefinitions.Add([Windows.Controls.ColumnDefinition]::new());$grid.ColumnDefinitions.Add([Windows.Controls.ColumnDefinition]::new())
 $dark=[Windows.Controls.Border]::new();$dark.Background=[Windows.Media.Brushes]::Black;$light=[Windows.Controls.Border]::new();$light.Background=[Windows.Media.Brushes]::White;[Windows.Controls.Grid]::SetColumn($light,1);$grid.Children.Add($dark)|Out-Null;$grid.Children.Add($light)|Out-Null;$behind.Content=$grid
 $behind.Show();$front.AllowsTransparency=$true;$front.Background=[Windows.Media.Brushes]::Magenta;$front.Show();Settle
 $capture=[HardwarePulse.LocalContrast]::new($front);Assert ($capture.Enable()) 'Capture exclusion unavailable';Settle
 $watch=[Diagnostics.Stopwatch]::StartNew();$mask=$capture.Capture([Windows.Media.Colors]::Transparent);$watch.Stop()
 Assert ($null -ne $mask) 'Missing local contrast mask'
 $pixels=New-Object byte[] ($mask.PixelWidth*$mask.PixelHeight*4);$mask.CopyPixels($pixels,$mask.PixelWidth*4,0)
 $left=([int]($mask.PixelHeight/2)*$mask.PixelWidth+[int]($mask.PixelWidth/4))*4;$right=([int]($mask.PixelHeight/2)*$mask.PixelWidth+[int]($mask.PixelWidth*3/4))*4
 Assert ($pixels[$left] -eq 245 -and $pixels[$right] -eq 20) 'Did not sample dark/light background through excluded overlay'
 $referenceMinorityDark=0.0;$referenceMinorityLight=0.0
 $referenceDark=$capture.RegionColor([Windows.Int32Rect]::new(0,0,[int]($mask.PixelWidth/2),$mask.PixelHeight),245,[ref]$referenceMinorityDark)
 $referenceLight=$capture.RegionColor([Windows.Int32Rect]::new([int]($mask.PixelWidth/2),0,[int]($mask.PixelWidth/2),$mask.PixelHeight),20,[ref]$referenceMinorityLight)
 $analysisSize=$capture.CaptureAnalysis($capture.Bounds(),[Windows.Media.Colors]::White,6)
 Assert ($analysisSize.Width -eq $mask.PixelWidth -and $analysisSize.Height -eq $mask.PixelHeight) 'Analysis-only capture returned a different sampling grid'
 $freshPixels=New-Object byte[] ($mask.PixelWidth*$mask.PixelHeight*4);$mask.CopyPixels($freshPixels,$mask.PixelWidth*4,0)
 $freshLeft=([int]($mask.PixelHeight/2)*$mask.PixelWidth+[int]($mask.PixelWidth/4))*4;$freshRight=([int]($mask.PixelHeight/2)*$mask.PixelWidth+[int]($mask.PixelWidth*3/4))*4
 Assert ($freshPixels[$freshLeft] -eq 245 -and $freshPixels[$freshRight] -eq 20) 'Analysis-only capture changed the frozen BitmapSource'
 $analysisSize=$capture.CaptureAnalysis($capture.Bounds(),[Windows.Media.Colors]::Transparent,6)
 Assert ($analysisSize.Width -eq $mask.PixelWidth -and $analysisSize.Height -eq $mask.PixelHeight) 'Transparent analysis restore returned a different sampling grid'
 $analysisMinorityDark=0.0;$analysisMinorityLight=0.0
 $analysisDark=$capture.RegionColor([Windows.Int32Rect]::new(0,0,[int]($analysisSize.Width/2),$analysisSize.Height),245,[ref]$analysisMinorityDark)
 $analysisLight=$capture.RegionColor([Windows.Int32Rect]::new([int]($analysisSize.Width/2),0,[int]($analysisSize.Width/2),$analysisSize.Height),20,[ref]$analysisMinorityLight)
 Assert ($analysisDark -eq $referenceDark -and $analysisLight -eq $referenceLight -and [Math]::Abs($analysisMinorityDark-$referenceMinorityDark) -lt .000001 -and [Math]::Abs($analysisMinorityLight-$referenceMinorityLight) -lt .000001) 'Analysis-only contrast differed from the BitmapSource reference'
 $invalid=[Windows.Rect]::new(0,0,0,1);Assert ($capture.CaptureAnalysis($invalid,[Windows.Media.Colors]::Transparent,6).IsEmpty) 'Invalid analysis bounds were not rejected'
 Assert ([HardwarePulse.LocalContrast]::Select(.19,20) -eq 20 -and [HardwarePulse.LocalContrast]::Select(.19,245) -eq 245) 'Hysteresis loses prior color'
 Assert ([HardwarePulse.LocalContrast]::Select(.8,245) -eq 20 -and [HardwarePulse.LocalContrast]::Select(.02,20) -eq 245) 'Strong contrast change delayed'
 $noise=New-Object single[] 1024;$scratch=New-Object single[] 1024
 for($y=0;$y -lt 32;$y++){for($x=0;$x -lt 32;$x++){$noise[$y*32+$x]=if(($x+$y)%2){.29}else{.09}}}
 [HardwarePulse.LocalContrast]::Smooth($noise,32,32,6,$scratch)
 Assert (@($noise|Where-Object {$_ -lt .16 -or $_ -gt .22}).Count -eq 0) 'Fine texture still creates black/white speckles'
 Assert ([Math]::Abs([HardwarePulse.LocalContrast]::Stabilize(.8,.1)-.8) -lt .001) 'Large background change delayed'
 # Exercise the bounded grid and native buffer lifecycle at a larger physical size.
 foreach($window in @($behind,$front)){$window.Width=600;$window.Height=500;$window.UpdateLayout()};Settle
 Assert ([ContrastTestComposition]::DwmFlush() -eq 0) 'Desktop composition did not synchronize after resize'
 $large=$capture.Capture([Windows.Media.Colors]::Transparent)
 Assert ($large.PixelWidth*$large.PixelHeight -le 160000 -and $large.PixelWidth -lt $capture.Bounds().Width) 'Large capture did not use a bounded grid'
 $largeAnalysis=$capture.CaptureAnalysis($capture.Bounds(),[Windows.Media.Colors]::Transparent,6)
 Assert ($largeAnalysis.Width -eq $large.PixelWidth -and $largeAnalysis.Height -eq $large.PixelHeight -and $largeAnalysis.Width*$largeAnalysis.Height -le 160000) 'Large analysis-only capture did not use the bounded grid'
 $minority=0.0;$region=[Windows.Int32Rect]::new(10,10,[int]($large.PixelWidth/4),[int]($large.PixelHeight/2))
 $shade=$capture.RegionColor($region,20,[ref]$minority)
 if($shade -ne 245 -or $minority -ne 0){
  $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($large))
  $stream=[IO.File]::Create((Join-Path $PWD 'vendor/contrast-failed-mask.png'));try{$encoder.Save($stream)}finally{$stream.Dispose()}
  throw "Reduced grid lost dark-background contrast: shade=$shade minority=$minority region=$region bounds=$($capture.Bounds())"
 }
 $capture.Dispose();Assert ($capture.CaptureAnalysis($capture.Bounds(),[Windows.Media.Colors]::Transparent,6).IsEmpty) 'Disposed analysis-only capture did not return an empty size';Assert ($capture.Enable()) 'Capture could not resume after disposal';Settle
 $resumed=$capture.Capture([Windows.Media.Colors]::Transparent);Assert ($null -ne $resumed) 'Capture buffers did not recover'
 $resumedAnalysis=$capture.CaptureAnalysis($capture.Bounds(),[Windows.Media.Colors]::Transparent,6);Assert ($resumedAnalysis.Width -eq $resumed.PixelWidth -and $resumedAnalysis.Height -eq $resumed.PixelHeight) 'Analysis-only capture did not recover after disposal'
 foreach($window in @($behind,$front)){$window.Width=300;$window.Height=150};Settle
 $small=$capture.Capture([Windows.Media.Colors]::Transparent)
 Assert ($small.PixelWidth -eq $mask.PixelWidth -and $small.PixelHeight -eq $mask.PixelHeight) 'Capture buffers did not resize back'
 $smallAnalysis=$capture.CaptureAnalysis($capture.Bounds(),[Windows.Media.Colors]::Transparent,6)
 Assert ($smallAnalysis.Width -eq $small.PixelWidth -and $smallAnalysis.Height -eq $small.PixelHeight -and $smallAnalysis.Width -eq $mask.PixelWidth -and $smallAnalysis.Height -eq $mask.PixelHeight) 'Analysis-only capture did not resize back'
 $capture.Dispose();Settle
 $probe=[HardwarePulse.LocalContrast]::new($behind)
 try{Assert ($probe.Enable()) 'Probe exclusion unavailable';Settle;$visible=$probe.Capture([Windows.Media.Colors]::Transparent);$visible.CopyPixels($pixels,$visible.PixelWidth*4,0);Assert ($pixels[$left] -eq $pixels[$right]) 'Disabling local contrast did not restore overlay capture'}finally{$probe.Dispose()}
 'PASS local contrast: actual capture exclusion, split dark/light background, hysteresis; capture '+$watch.ElapsedMilliseconds+' ms'
 $front.Close()
 $factory=[Func[string,double,string,Windows.FrameworkElement]]{param($name,$size,$color);$shape=[Windows.Controls.Border]::new();$shape.Width=$shape.Height=$size;$shape.Background=[Windows.Media.BrushConverter]::new().ConvertFromString($color);return $shape}
 $view=[HardwarePulse.DesktopView]::new($factory,$true)
 try{
  $view.Left=150;$view.Top=150;$view.Width=300;$view.Height=150;$view.SizeToContent='Manual';$view.Topmost=$true
  $metrics=[Collections.Generic.List[HardwarePulse.DesktopMetric]]::new();$metrics.Add([HardwarePulse.DesktopMetric]::new('demo','MMMMMMMMMMMM','60','cpu'))
  $view.Render($metrics,24,10,'#FFFFFF',$true,1,$null);$view.SetTextOpacity(100,$true,$true,0,0);$view.Show();$view.SetLocalContrast($true);Settle;Settle
  $flags=[Reflection.BindingFlags]'Instance,NonPublic';$rows=$view.GetType().GetField('rows',$flags).GetValue($view);Assert ($rows['demo'].Name.Foreground -is [Windows.Media.SolidColorBrush] -and $rows['demo'].Value.Foreground.Color.R -eq 20) 'Local reading must use one coherent dark color over white'
  Assert ($rows['demo'].Name.Effect -is [Windows.Media.Effects.DropShadowEffect] -and $null -eq $rows['demo'].Value.Effect) 'Mixed background needs a thin edge; uniform background must remain plain'
  $frozen=$rows['demo'].Name.Foreground;Settle;Assert ([object]::ReferenceEquals($frozen,$rows['demo'].Name.Foreground)) 'Unchanged contrast recreated its foreground brush'
  $view.BeginScreenshot();Assert ($view.ScreenshotActive) 'Screenshot mode did not start';Settle;Assert ($rows['demo'].Name.Foreground -eq $frozen) 'Screenshot mode changed the sampled colors'
  $timer=$view.GetType().GetField('screenshotTimer',$flags).GetValue($view);$timer.Interval=[TimeSpan]::FromMilliseconds(50);$timer.Stop();$timer.Start();Settle;Assert (-not $view.ScreenshotActive -and $view.LocalContrastAvailable) 'Screenshot mode did not automatically resume contrast'
  $visual=[Windows.Media.DrawingVisual]::new();$context=$visual.RenderOpen();$rect=[Windows.Rect]::new(0,0,300,150);$context.DrawRectangle([Windows.Media.VisualBrush]::new($grid),$null,$rect);$context.DrawRectangle([Windows.Media.VisualBrush]::new($view.Content),$null,$rect);$context.Close()
  $image=[Windows.Media.Imaging.RenderTargetBitmap]::new(300,150,96,96,[Windows.Media.PixelFormats]::Pbgra32);$image.Render($visual);$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image));$stream=[IO.File]::Create((Join-Path $PWD 'vendor/local-contrast-preview.png'));try{$encoder.Save($stream)}finally{$stream.Dispose()}
  $view.SetLocalContrast($false);Assert ($rows['demo'].Name.Foreground -is [Windows.Media.SolidColorBrush] -and $null -eq $rows['demo'].Name.Effect) 'Manual foreground/edge not restored'
  # Give the icon a different background from its adjacent label.
  $grid.ColumnDefinitions[0].Width=[Windows.GridLength]::new(52);$dark.Background=[Windows.Media.Brushes]::White;$light.Background=[Windows.Media.Brushes]::Black;Settle
  $metrics[0].Title='CPU';$view.Render($metrics,24,10,'#FFFFFF',$true,1,$null);$view.SetLocalContrast($true);Settle;Settle
  Assert ($rows['demo'].IconColor -eq '#141414' -and $rows['demo'].Name.Foreground.Color.R -eq 245) 'Icon sampled the label background instead of its own'
  $dark.Background=[Windows.Media.Brushes]::Black;Settle;Settle
  Assert ($rows['demo'].IconColor -eq '#F5F5F5') 'Icon did not brighten over a dark background'
  $stableIcon=$rows['demo'].Icon;Settle;Assert ([object]::ReferenceEquals($stableIcon,$rows['demo'].Icon)) 'Stable icon was recreated every capture'
  $customPalette=[Func[string,string]]{param($name);return '#112233'};$view.Render($metrics,24,10,'#FFFFFF',$true,1,$customPalette);Settle;Settle
  Assert ($rows['demo'].IconColor -eq '#112233' -and $rows['demo'].IconHost.Effect.Color -eq [Windows.Media.Colors]::White) 'Dark App palette was replaced instead of outlined'
  $samePalette=[Func[string,string]]{param($name);return '#FFFFFF'};$view.Render($metrics,24,10,'#FFFFFF',$true,1,$samePalette);Settle;Settle
  Assert ($rows['demo'].IconColor -eq '#FFFFFF' -and $rows['demo'].IconHost.Effect.Color -eq [Windows.Media.Colors]::Black) 'App palette equal to text was incorrectly treated as monochrome'
  $palette=[Func[string,string]]{param($name);return '#A5E7D5'};$view.Render($metrics,24,10,'#FFFFFF',$true,1,$palette);Settle;Settle
  Assert ($rows['demo'].IconColor -eq '#A5E7D5' -and $rows['demo'].IconHost.Effect.Color -eq [Windows.Media.Colors]::Black) 'Light App palette was replaced instead of outlined'
  $timer.Interval=[TimeSpan]::FromSeconds(15);$view.BeginScreenshot();$frozenIcon=$rows['demo'].Icon;$dark.Background=[Windows.Media.Brushes]::White;Settle
  Assert ([object]::ReferenceEquals($frozenIcon,$rows['demo'].Icon)) 'Screenshot mode did not freeze icon appearance'
  $view.SetLocalContrast($false);Assert ($rows['demo'].IconColor -eq '#A5E7D5' -and $null -eq $rows['demo'].IconHost.Effect) 'Disabling contrast did not restore base icon style'
 }finally{$view.Close()}
}finally{if($capture){$capture.Dispose()};$front.Close();$behind.Close()}
