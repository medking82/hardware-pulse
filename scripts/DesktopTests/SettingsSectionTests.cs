using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HardwarePulse.Desktop;

static class SettingsSectionTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run(string? output) {
        var owner=new MonitorWindow(new MonitorSource(true),start:false);owner.Show();
        try {
            owner.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="OpenSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            owner.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex=2;
            Dispatcher.UIThread.RunJobs();
            var panel=owner.GetVisualDescendants().OfType<SettingsSections>().Single();
            var sections=panel.Children.OfType<Expander>().ToArray();
            Check(sections.Select(x=>x.Header as string).SequenceEqual(new[]{"Desktop Layout","Desktop Appearance","Desktop Readings"}),"Original Desktop section order");
            foreach(int width in new[]{240,840,1200}) {
                owner.Width=width;owner.Height=750;Dispatcher.UIThread.RunJobs();
                Check(sections.All(x=>x.Bounds.Width>0&&x.Bounds.Right<=panel.Bounds.Width+1),"Sections remain within viewport");
                int columns=sections.Select(x=>x.Bounds.X).Distinct().Count();Check(columns==(width==1200?3:width==840?2:1),$"Original 350 DIP responsive section columns: requested={width}, actual={owner.ClientSize.Width}, panel={panel.Bounds.Width}, columns={columns}, slots={string.Join(";",sections.Select(x=>x.Bounds))}");
                if(output!=null){using var frame=owner.CaptureRenderedFrame();frame!.Save(Path.Combine(output,"desktop-sections-"+width+".png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            }
            var appearance=sections[1];var slider=appearance.GetVisualDescendants().OfType<Slider>().Single(x=>x.Name=="DesktopFontSize");
            slider.Value=19;
            var header=appearance.GetVisualDescendants().OfType<ToggleButton>().Single(x=>x.Name=="SectionHeader");header.Focus();
            owner.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");owner.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");Dispatcher.UIThread.RunJobs();
            Check(!appearance.IsExpanded&&!slider.IsEffectivelyVisible,"Keyboard collapses section content");
            header.Focus();owner.KeyPress(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");owner.KeyRelease(Key.Space,RawInputModifiers.None,PhysicalKey.Space," ");Dispatcher.UIThread.RunJobs();
            Check(appearance.IsExpanded&&slider.IsEffectivelyVisible&&slider.Value==19,"Reopening retains control state");
            owner.Language.Select("zh-CN");Check((string?)appearance.Header=="桌面外观","Section header localizes immediately");
            owner.Language.Select("en");
            string[][] titles=[["General"],["App Appearance","Window"],["Desktop Layout","Desktop Appearance","Desktop Readings"],["App Cards","Hardware Names"],["AI Quota"],["FPS","Game Overlay"]];
            var tabs=owner.GetVisualDescendants().OfType<TabControl>().Single();
            foreach(int width in new[]{360,840})for(int index=0;index<titles.Length;index++) {
                owner.Width=width;tabs.SelectedIndex=index;Dispatcher.UIThread.RunJobs();
                var page=owner.GetVisualDescendants().OfType<SettingsSections>().Single();
                var groups=page.Children.OfType<Expander>().ToArray();
                Check(groups.Select(x=>x.Header as string).SequenceEqual(titles[index]),"Category retains original section ownership");
                Check(groups.All(x=>x.Bounds.Width>0&&x.Bounds.Right<=page.Bounds.Width+1),"Category sections fit narrow/wide view");
                if(output!=null){using var frame=owner.CaptureRenderedFrame();frame!.Save(Path.Combine(output,$"settings-sections-{index}-{width}.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
            }
            Console.WriteLine("PASS Desktop Settings sections: original order, independent columns, keyboard collapse and retained state");
        } finally {owner.Close();}
    }
}
