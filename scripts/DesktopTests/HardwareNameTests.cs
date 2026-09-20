using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class HardwareNameTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run() {
        string root=Directory.CreateTempSubdirectory("pulse-names-").FullName;
        MonitorWindow? owner=null;
        try {
            string path=Path.Combine(root,"settings.json");var source=new MonitorSource(true);
            var snapshot=source.Poll(null) with {Hardware=new HardwarePulse.Reading{state="LIVE",values={{"cpu",50},{"diskC",40}},names={{"CPU","Automatic processor"},{"diskC","Automatic drive"}}}};
            string? original=snapshot.Hardware?.names.GetValueOrDefault("CPU");
            owner=new MonitorWindow(source,start:false,store:new PreviewSettingsStore(path));owner.Show();owner.Present(snapshot);
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            owner.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex=3;Dispatcher.UIThread.RunJobs();
            var editors=owner.GetVisualDescendants().OfType<TextBox>().Where(x=>x.Name?.StartsWith("HardwareName")==true).ToArray();
            Check(editors.Length==12&&editors.All(x=>x.MaxLength==160),"Original Hardware Names controls missing");
            editors.Single(x=>x.Name=="HardwareNameCPU").Text="  Desk processor  ";
            editors.Single(x=>x.Name=="HardwareNamediskC").Text="Work drive";
            owner.OpenFloatingMonitor();owner.Present(snapshot);Dispatcher.UIThread.RunJobs();
            var desktop=owner.FloatingMonitor!;
            Check(desktop.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="CPU")&&
                !desktop.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="Desk processor"),"Desktop category labels must retain original 0.6.27 behavior");
            Check(desktop.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="Work drive"),"Desktop ignores custom drive name");
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="Back").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
            Check(owner.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="Desk processor"),"App card ignores custom CPU name");
            Check(snapshot.Hardware?.names.GetValueOrDefault("CPU")==original,"Presentation changed sampler metadata");
            owner.Present(source.Poll(null));Check(owner.GetVisualDescendants().OfType<TextBlock>().Any(x=>x.Text=="Desk processor"),"Refresh discards custom name");
            owner.Close();owner=null;var saved=new PreviewSettingsStore(path).Load();
            Check(saved.Names["CPU"]=="Desk processor"&&saved.Names["diskC"]=="Work drive","Names failed to persist");
            File.WriteAllText(path,"{\"names\":{\"CPU\":\"   \",\"GPU\":\"bad\\nname\",\"unknown\":\"drop\"}}");
            Check(new PreviewSettingsStore(path).Load().Names.Count==0,"Invalid or unsupported names survived normalization");
            Console.WriteLine("PASS Hardware Names: original controls, App/Desktop reuse, persistence, refresh and immutable sampler metadata");
        }finally{owner?.Close();Directory.Delete(root,true);}
    }
}
