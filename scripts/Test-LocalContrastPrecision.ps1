param([string]$AppPath='build/native/app')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
[void][Reflection.Assembly]::LoadFrom((Join-Path ([IO.Path]::GetFullPath($AppPath)) 'HardwarePulse.exe'))
function Settle { $watch=[Diagnostics.Stopwatch]::StartNew();while($watch.ElapsedMilliseconds -lt 500){[Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::Background);Start-Sleep -Milliseconds 10} }
function Assert($condition,$message){if(!$condition){throw $message}}
$behind=[Windows.Window]::new();$view=$null
try {
 $behind.WindowStyle='None';$behind.ResizeMode='NoResize';$behind.ShowInTaskbar=$false;$behind.WindowStartupLocation='Manual';$behind.Left=150;$behind.Top=150;$behind.Width=400;$behind.Height=200;$behind.Topmost=$true
 $canvas=[Windows.Controls.Canvas]::new();$canvas.Background=[Windows.Media.Brushes]::White;$behind.Content=$canvas;$behind.Show()
 $factory=[Func[string,double,string,Windows.FrameworkElement]]{param($name,$size,$color);$shape=[Windows.Controls.Border]::new();$shape.Width=$shape.Height=$size;return $shape}
 $view=[HardwarePulse.DesktopView]::new($factory,$true)
 $view.Left=150;$view.Top=150;$view.Width=400;$view.Height=200;$view.SizeToContent='Manual';$view.Topmost=$true
 $metrics=[Collections.Generic.List[HardwarePulse.DesktopMetric]]::new();$metrics.Add([HardwarePulse.DesktopMetric]::new('fps','FPS','--','fps'))
 $view.Render($metrics,24,10,'#FFFFFF',$true,1,$null);$view.SetTextOpacity(100,$true,$true,0,0);$view.Show();$view.SetLocalContrast($true);Settle
 $flags=[Reflection.BindingFlags]'Instance,NonPublic';$rows=$view.GetType().GetField('rows',$flags).GetValue($view);$value=$rows['fps'].Value
 Assert ($view.LocalContrastAvailable) 'Capture exclusion unavailable'
 Assert ($value.Foreground.Color.R -eq 20) 'Uniform white background must use dark text'
 # The FPS cell reserves room for three readings. Empty space to the left of a
 # short right-aligned reading must not vote on the background behind its glyphs.
 $ink=[Windows.Media.VisualTreeHelper]::GetContentBounds($value);$point=$value.TranslatePoint($ink.TopLeft,$view)
 Assert ($ink.Width -lt $value.ActualWidth/2) 'FPS fixture did not reserve empty space'
 $patch=[Windows.Controls.Border]::new();$patch.Background=[Windows.Media.Brushes]::Black;$patch.Width=$ink.Width+24;$patch.Height=$ink.Height+24
 [Windows.Controls.Canvas]::SetLeft($patch,$point.X-12);[Windows.Controls.Canvas]::SetTop($patch,$point.Y-12);$null=$canvas.Children.Add($patch);Settle;Settle
 Assert ($value.Foreground.Color.R -eq 245) 'Blank FPS cell space outweighed the dark background beneath its actual glyphs'
 # Re-check after a fractional-DIP resize changes the capture downsampling grid.
 $canvas.Children.Clear();$view.Width=650.5;$behind.Width=650.5;$view.UpdateLayout();Settle
 $ink=[Windows.Media.VisualTreeHelper]::GetContentBounds($value);$point=$value.TranslatePoint($ink.TopLeft,$view)
 $patch.Width=$ink.Width+24;$patch.Height=$ink.Height+24;[Windows.Controls.Canvas]::SetLeft($patch,$point.X-12);[Windows.Controls.Canvas]::SetTop($patch,$point.Y-12);$null=$canvas.Children.Add($patch);Settle;Settle
 Assert ($value.Foreground.Color.R -eq 245) 'Glyph sampling drifted after resize/downsampling'
 $canvas.Children.Clear();$canvas.Background=[Windows.Media.Brushes]::Black;Settle;Settle
 Assert ($value.Foreground.Color.R -eq 245) 'Dark fixture did not establish a light prior'
 $canvas.Background=[Windows.Media.Brushes]::Gray;Settle;Settle
 Assert ($value.Foreground.Color.R -eq 20 -and $rows['fps'].Name.Foreground.Color.R -eq 20) 'Light text remained stuck on mid-gray despite the stronger dark-text contrast'
 Assert ($null -eq $value.Effect) 'Uniform mid-gray acquired an unnecessary outline'
 # Gaming HUD: a transparent surface and translucent text must be evaluated
 # after compositing, without raising the user's opacity or adding a backplate.
 $canvas.Background=[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromRgb(223,12,8));Settle;Settle
 Assert ($value.Foreground.Color.R -eq 245) 'Opaque ink fixture did not prefer the light shade on red'
 $view.SetTextOpacity(40,$true,$true,0,0);Settle;Settle
 Assert ($value.Foreground.Color.R -eq 20) 'Translucent ink ignored the changed contrast after compositing over a colored background'
 $stack=$view.GetType().GetField('stack',$flags).GetValue($view);$surface=$view.GetType().GetField('surface',$flags).GetValue($view)
 Assert ($stack.Opacity -eq .4 -and $surface.Background.Color.A -eq 0) 'Local Contrast overrode the translucent gaming HUD preferences'
 $view.SetTextOpacity(0,$true,$true,0,0);Settle;Assert ($stack.Opacity -eq 0) 'Invisible text was forced visible'
 $view.SetTextOpacity(100,$true,$true,0,0);Settle;Settle
 Assert ($value.Foreground.Color.R -eq 245 -and $stack.Opacity -eq 1 -and $surface.Background.Color.A -eq 0) 'Opaque contrast did not recover after changing text opacity'
 'PASS native contrast precision: glyph bounds, FPS whitespace, fractional resize, mid-gray and translucent gaming HUD without opacity/backplate overrides'
}finally{if($view){$view.Close()};$behind.Close()}
