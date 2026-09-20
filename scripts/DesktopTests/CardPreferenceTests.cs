using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class CardPreferenceTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output) {
        string directory=Directory.CreateTempSubdirectory("pulse-card-preferences-").FullName;
        try {
            string path=Path.Combine(directory,"settings.json");
            File.WriteAllText(path,"{\"cardOrder\":[\"Network\",\"Network\",17,\"unknown\"],\"hiddenCards\":[\"GPU\",null,\"unknown\"],\"future\":19}");
            var store=new PreviewSettingsStore(path);var initial=store.Load();
            Check(initial.CardOrder.SequenceEqual(new[]{"Network","CPU","GPU","Memory","NVMe","Airflow"}),"Normalize saved order without duplicates or losing newly supported cards");
            Check(initial.HiddenCards.SetEquals(new[]{"GPU"}),"Ignore invalid visibility keys");
            var source=new MonitorSource(true);var window=new MonitorWindow(source,start:false,store:store){Width=360,Height=700};window.Show();window.Present(source.Poll(null));Dispatcher.UIThread.RunJobs();
            try {
                T Find<T>(string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(x=>x.Name==name);
                void Click(string name){Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();}
                var cards=Find<Grid>("ReadingCards");var original=cards.Children.ToArray();
                Check(original[0].Name=="CardNetwork","Saved order is used before first reading");
                Click("OpenSettings");Find<TabControl>("SettingsTabs").SelectedIndex=2;Dispatcher.UIThread.RunJobs();
                if(output!=null){using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"app-card-settings.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
                Check(!Find<Button>("MoveCardUpNetwork").IsEnabled,"First card cannot move before start");
                Click("MoveCardDownNetwork");
                foreach(string key in PreviewSettings.CardKeys)Find<CheckBox>("ShowCard"+key).IsChecked=false;
                Click("Back");window.Present(source.Poll(null));
                Check(cards.Children[0].Name=="CardCPU"&&cards.Children[1].Name=="CardNetwork","Move changes actual card order");
                Check(cards.Children.All(x=>!x.IsVisible)&&Find<TextBlock>("CardsEmpty").IsVisible,"Polling preserves hidden cards and shows recovery guidance");
                Click("OpenSettings");Dispatcher.UIThread.RunJobs();Find<CheckBox>("ShowCardCPU").IsChecked=true;Click("Back");
                Check(cards.Children.Single(x=>x.Name=="CardCPU").IsVisible&&!Find<TextBlock>("CardsEmpty").IsVisible,"Re-enabling renders current snapshot without another poll");
                Check(original.All(cards.Children.Contains),"Reorder and visibility reuse existing card controls");
            } finally {window.Close();}
            var saved=new PreviewSettingsStore(path).Load();
            Check(saved.CardOrder[0]=="CPU"&&saved.CardOrder[1]=="Network"&&!saved.HiddenCards.Contains("CPU")&&saved.HiddenCards.Contains("Network"),"Order and visibility persist independently");
            using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));Check(json.RootElement.GetProperty("future").GetInt32()==19,"Card save preserves unknown profile fields");
        } finally {Directory.Delete(directory,true);}
        Console.WriteLine("PASS card preferences: normalization, order, visibility across polling, reuse and persistence");
    }
}
