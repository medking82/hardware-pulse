param([string]$AppPath='build/native/app')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
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
 Assert ([HardwarePulse.LocalContrast]::Select(.19,20) -eq 20 -and [HardwarePulse.LocalContrast]::Select(.19,245) -eq 245) 'Hysteresis loses prior color'
 Assert ([HardwarePulse.LocalContrast]::Select(.8,245) -eq 20 -and [HardwarePulse.LocalContrast]::Select(.02,20) -eq 245) 'Strong contrast change delayed'
 $capture.Dispose();Settle
 $probe=[HardwarePulse.LocalContrast]::new($behind)
 try{Assert ($probe.Enable()) 'Probe exclusion unavailable';Settle;$visible=$probe.Capture([Windows.Media.Colors]::Transparent);$visible.CopyPixels($pixels,$visible.PixelWidth*4,0);Assert ($pixels[$left] -eq $pixels[$right]) 'Disabling local contrast did not restore overlay capture'}finally{$probe.Dispose()}
 'PASS local contrast: actual capture exclusion, split dark/light background, hysteresis; capture '+$watch.ElapsedMilliseconds+' ms'
 $front.Close()
 $factory=[Func[string,double,string,Windows.FrameworkElement]]{param($name,$size,$color);return [Windows.Controls.Border]::new()}
 $view=[HardwarePulse.DesktopView]::new($factory,$true)
 try{
  $view.Left=150;$view.Top=150;$view.Width=300;$view.Height=150;$view.SizeToContent='Manual';$view.Topmost=$true
  $metrics=[Collections.Generic.List[HardwarePulse.DesktopMetric]]::new();$metrics.Add([HardwarePulse.DesktopMetric]::new('demo','MMMMMMMMMMMM','60','fps'))
  $view.Render($metrics,24,10,'#FFFFFF',$true,1,$null);$view.SetTextOpacity(100,$true,$true,0,0);$view.Show();$view.SetLocalContrast($true);Settle;Settle
  $flags=[Reflection.BindingFlags]'Instance,NonPublic';$rows=$view.GetType().GetField('rows',$flags).GetValue($view);Assert ($rows['demo'].Name.Foreground -is [Windows.Media.ImageBrush]) 'Runtime did not apply per-pixel brush to text'
  $visual=[Windows.Media.DrawingVisual]::new();$context=$visual.RenderOpen();$rect=[Windows.Rect]::new(0,0,300,150);$context.DrawRectangle([Windows.Media.VisualBrush]::new($grid),$null,$rect);$context.DrawRectangle([Windows.Media.VisualBrush]::new($view.Content),$null,$rect);$context.Close()
  $image=[Windows.Media.Imaging.RenderTargetBitmap]::new(300,150,96,96,[Windows.Media.PixelFormats]::Pbgra32);$image.Render($visual);$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image));$stream=[IO.File]::Create((Join-Path $PWD 'vendor/local-contrast-preview.png'));try{$encoder.Save($stream)}finally{$stream.Dispose()}
  $view.SetLocalContrast($false);Assert ($rows['demo'].Name.Foreground -is [Windows.Media.SolidColorBrush]) 'Manual foreground not restored'
 }finally{$view.Close()}
}finally{if($capture){$capture.Dispose()};$front.Close();$behind.Close()}
