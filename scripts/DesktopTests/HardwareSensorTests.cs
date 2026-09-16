using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class HardwareSensorTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output) {
        var panel=new HardwareSensorPanel();
        var window=new Window{Width=360,Height=500,FontSize=15,Content=panel};window.Show();
        HardwareSensorSnapshot[] data=[new("a","fixture_chip · Long kernel label for an exposed package temperature sensor","54.0 °C"),new("b","fixture_chip · fan1","0 RPM")];
        panel.Present(data,true);
        foreach(int width in new[]{360,800}) {
            window.Width=width;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var text=panel.GetVisualDescendants().OfType<TextBlock>().ToArray();
            Check(text.Any(x=>x.Text=="54.0 °C")&&text.Any(x=>x.Text=="0 RPM"),"Temperature and stopped fan remain visible");
            foreach(var row in panel.GetVisualDescendants().OfType<Grid>()) {
                var label=(TextBlock)row.Children[0];var value=(TextBlock)row.Children[1];
                Check(label.Bounds.Right<=value.Bounds.Left&&value.Bounds.Right<=row.Bounds.Width+.1,"Label and value do not overlap or overflow");
            }
            if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(System.IO.Path.Combine(output,$"sensors-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        }
        var original=panel.GetVisualDescendants().OfType<TextBlock>().Single(x=>x.Text=="54.0 °C");
        panel.Present([data[0] with{Value="—"},data[1]],true);
        Check(original.Text=="—"&&panel.GetVisualDescendants().Contains(original),"Missing value clears in reused control");
        panel.Present([data[1]],true);
        Check(!panel.GetVisualDescendants().Contains(original),"Removed sensor leaves no stale row");
        panel.Present([],true);
        Check(panel.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text!.Contains("No temperature")),"Empty hardware capability explained");
        panel.Present([],false);
        Check(panel.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text!.Contains("Not available")),"Unsupported platform distinguished from empty hardware");
        window.Close();Console.WriteLine("PASS sensor panel: responsive labels, unavailable, stable controls and topology changes");
    }
}
