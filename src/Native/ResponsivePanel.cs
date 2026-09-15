using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HardwarePulse {
    // StackPanel-compatible owner so existing card order and visibility consumers stay unchanged.
    public sealed class ResponsivePanel : StackPanel {
        public double MinimumColumnWidth=270;
        public int RequestedColumns;
        public double RowGap=10;
        public int Columns {get;private set;}
        public double CellWidth {get;private set;}
        public bool Dragging;
        public bool IndependentColumns;
        readonly Dictionary<UIElement,Rect> slots=new Dictionary<UIElement,Rect>();
        const double Gap=ColumnLayout.Gap;
        double arrangedWidth;
        int arrangedColumns;
        bool animateLayout;
        protected override Size MeasureOverride(Size available){
            var layout=new ColumnLayout(available.Width,MinimumColumnWidth,RequestedColumns,Columns);
            double width=layout.Width;Columns=layout.Columns;CellWidth=layout.CellWidth;
            if(IndependentColumns){var heights=new double[Columns];int at=0;foreach(UIElement child in Children){if(child.Visibility==Visibility.Collapsed)continue;child.Measure(new Size(CellWidth,double.PositiveInfinity));heights[at++%Columns]+=child.DesiredSize.Height+RowGap;}return new Size(width,Math.Max(0,heights.Max()-RowGap));}
            double height=0,row=0;int index=0;
            foreach(UIElement child in Children){if(child.Visibility==Visibility.Collapsed)continue;child.Measure(new Size(CellWidth,double.PositiveInfinity));row=Math.Max(row,child.DesiredSize.Height);if(++index%Columns==0){height+=row+RowGap;row=0;}}
            if(index%Columns!=0)height+=row+RowGap;
            return new Size(width,Math.Max(0,height-RowGap));
        }
        protected override Size ArrangeOverride(Size size){
            animateLayout=arrangedColumns!=Columns||Math.Abs(arrangedWidth-size.Width)<.5;
            arrangedWidth=size.Width;arrangedColumns=Columns;
            foreach(var key in slots.Keys.Where(c=>!Children.Contains(c)).ToArray())slots.Remove(key);
            var active=Children.Cast<UIElement>().Where(c=>c.Visibility!=Visibility.Collapsed).ToArray();
            if(IndependentColumns){var heights=new double[Columns];for(int i=0;i<active.Length;i++){var child=active[i];int col=i%Columns;var rect=new Rect(col*(CellWidth+Gap),heights[col],CellWidth,child.DesiredSize.Height);Rect old;bool moved=slots.TryGetValue(child,out old)&&old.Location!=rect.Location;child.Arrange(rect);slots[child]=rect;if(moved&&!Dragging&&IsLoaded&&animateLayout)Offset(child,old.X-rect.X,old.Y-rect.Y);heights[col]+=child.DesiredSize.Height+RowGap;}return size;}
            double top=0;for(int start=0;start<active.Length;start+=Columns){double height=active.Skip(start).Take(Columns).Max(c=>c.DesiredSize.Height);
                for(int col=0;col<Columns&&start+col<active.Length;col++){
                    var child=active[start+col];var rect=new Rect(col*(CellWidth+Gap),top,CellWidth,child.DesiredSize.Height);Rect old;
                    bool moved=slots.TryGetValue(child,out old)&&old.Location!=rect.Location;
                    child.Arrange(rect);slots[child]=rect;
                    if(moved&&!Dragging&&IsLoaded&&animateLayout)Offset(child,old.X-rect.X,old.Y-rect.Y);
                }top+=height+RowGap;
            }return size;
        }
        public static void Offset(UIElement child,double x,double y){
            var shift=child.RenderTransform as TranslateTransform;
            if(shift==null){shift=new TranslateTransform();child.RenderTransform=shift;}
            x+=shift.X;y+=shift.Y;shift.BeginAnimation(TranslateTransform.XProperty,null);shift.BeginAnimation(TranslateTransform.YProperty,null);shift.X=shift.Y=0;
            if(!SystemParameters.ClientAreaAnimation)return;
            var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
            shift.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(x,0,TimeSpan.FromMilliseconds(180)){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
            shift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(y,0,TimeSpan.FromMilliseconds(180)){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
        }
        public void Attach(Border card,Thumb handle,ScrollViewer scroll,Action changed){
            Point grab=new Point();UIElement[] original=null;Brush border=null;
            handle.DragStarted+=delegate{original=Children.Cast<UIElement>().ToArray();grab=Mouse.GetPosition(card);Dragging=true;border=card.BorderBrush;card.BorderBrush=Brushes.Aquamarine;Panel.SetZIndex(card,10);card.RenderTransform=new TranslateTransform();};
            handle.DragDelta+=delegate{
                if(original==null)return;Point p=Mouse.GetPosition(scroll);if(p.Y<32)scroll.ScrollToVerticalOffset(scroll.VerticalOffset-16);else if(p.Y>scroll.ActualHeight-32)scroll.ScrollToVerticalOffset(scroll.VerticalOffset+16);UpdateLayout();
                p=Mouse.GetPosition(this);var target=Children.Cast<UIElement>().Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>{var s=slots[c];return Math.Pow(p.X-(s.X+s.Width/2),2)+Math.Pow(p.Y-(s.Y+s.Height/2),2);}).FirstOrDefault();
                if(target!=null&&target!=card){int to=Children.IndexOf(target);var previous=slots.ToDictionary(e=>e.Key,e=>e.Value);Children.Remove(card);Children.Insert(to,card);UpdateLayout();foreach(var item in Children.Cast<UIElement>().Where(c=>c!=card&&slots.ContainsKey(c)&&previous.ContainsKey(c)))Offset(item,previous[item].X-slots[item].X,previous[item].Y-slots[item].Y);}
                var slot=slots[card];var move=(TranslateTransform)card.RenderTransform;move.X=p.X-grab.X-slot.X-card.Margin.Left;move.Y=p.Y-grab.Y-slot.Y-card.Margin.Top;
            };
            handle.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape&&original!=null){handle.CancelDrag();e.Handled=true;}else if(original==null&&(e.Key==Key.Up||e.Key==Key.Down||e.Key==Key.Left||e.Key==Key.Right)){int at=Children.IndexOf(card),delta=e.Key==Key.Up?-Columns:e.Key==Key.Down?Columns:e.Key==Key.Left?-1:1;int to=at+delta;if(to>=0&&to<Children.Count){Children.Remove(card);Children.Insert(to,card);changed();handle.Focus();}e.Handled=true;}};
            handle.DragCompleted+=delegate(object sender,DragCompletedEventArgs e){if(original==null)return;bool reordered=!Children.Cast<UIElement>().SequenceEqual(original);if(e.Canceled){Children.Clear();foreach(var child in original)Children.Add(child);}Dragging=false;original=null;card.BorderBrush=border;Panel.SetZIndex(card,0);Offset(card,0,0);if(reordered&&!e.Canceled)changed();handle.Focus();};
            handle.PreviewMouseLeftButtonDown+=delegate{handle.Focus();};
        }
    }
}
