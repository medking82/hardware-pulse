using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace HardwarePulse.Desktop;

// Preview uses transforms; only a completed gesture mutates order/persistence.
internal static class ReorderHandle {
    public static Control Create(StackPanel panel,Control row,string name,Action changed,Func<bool>? allowed=null) {
        var handle=new Border{Name=name,Width=30,MinHeight=34,Focusable=true,Background=Brushes.Transparent,BorderThickness=new Thickness(1),Cursor=new Cursor(StandardCursorType.SizeAll),Child=new TextBlock{Text="⠿",FontSize=20,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        bool active=false;double grab=0;int target=0;IPointer? pointer=null;
        bool Allowed()=>allowed?.Invoke()!=false;
        void Reset(){foreach(var item in panel.Children){item.RenderTransform=null;item.ZIndex=0;}handle.BorderBrush=null;}
        void Complete(bool cancel) {
            if(!active)return;active=false;pointer?.Capture(null);pointer=null;Reset();
            int from=panel.Children.IndexOf(row);
            if(!cancel&&Allowed()&&from>=0&&target!=from){panel.Children.RemoveAt(from);panel.Children.Insert(target,row);changed();}
            handle.Focus();
        }
        handle.PointerPressed+=(_,e)=>{
            if(!Allowed()||!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)return;
            handle.Focus();grab=e.GetPosition(panel).Y-row.Bounds.Y;target=panel.Children.IndexOf(row);active=true;pointer=e.Pointer;pointer.Capture(handle);row.ZIndex=10;handle.BorderBrush=Brushes.Aquamarine;e.Handled=true;
        };
        handle.PointerMoved+=(_,e)=>{
            if(!active)return;if(!Allowed()){Complete(true);return;}
            var scroll=panel.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            if(scroll!=null){double y=e.GetPosition(scroll).Y;double delta=y<32?-16:y>scroll.Bounds.Height-32?16:0;if(delta!=0)scroll.Offset=new Vector(scroll.Offset.X,Math.Clamp(scroll.Offset.Y+delta,0,Math.Max(0,scroll.Extent.Height-scroll.Viewport.Height)));}
            double top=e.GetPosition(panel).Y-grab;row.RenderTransform=new TranslateTransform(0,top-row.Bounds.Y);
            var others=panel.Children.Where(item=>item!=row).ToList();target=others.Count(item=>top+row.Bounds.Height/2>item.Bounds.Center.Y);others.Insert(target,row);
            double next=0;foreach(var item in others){if(item!=row)item.RenderTransform=new TranslateTransform(0,next-item.Bounds.Y);next+=item.Bounds.Height+panel.Spacing;}
            e.Handled=true;
        };
        handle.PointerReleased+=(_,e)=>{if(active){Complete(false);e.Handled=true;}};
        handle.PointerCaptureLost+=(_,_)=>Complete(true);
        handle.DetachedFromVisualTree+=(_,_)=>Complete(true);
        handle.KeyDown+=(_,e)=>{
            if(e.Key==Key.Escape&&active){Complete(true);e.Handled=true;return;}
            if(active||!Allowed()||e.Key is not (Key.Up or Key.Down))return;
            int from=panel.Children.IndexOf(row),to=from+(e.Key==Key.Up?-1:1);
            if(to>=0&&to<panel.Children.Count){panel.Children.RemoveAt(from);panel.Children.Insert(to,row);changed();handle.Focus();row.BringIntoView();}e.Handled=true;
        };
        return handle;
    }
}
