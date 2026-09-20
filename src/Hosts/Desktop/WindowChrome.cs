using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace HardwarePulse.Desktop;

// Shared hit regions; the host window retains move/resize and lock ownership.
internal static class WindowChrome {
    public static void AddResizeEdges(Window window,Grid frame) {
        void Edge(WindowEdge edge,HorizontalAlignment horizontal,VerticalAlignment vertical,StandardCursorType cursor,bool corner=false) {
            var grip=new Border{Name="Resize"+edge,Background=Brushes.Transparent,HorizontalAlignment=horizontal,VerticalAlignment=vertical,Cursor=new Cursor(cursor)};
            if(horizontal!=HorizontalAlignment.Stretch)grip.Width=corner?10:5;
            if(vertical!=VerticalAlignment.Stretch)grip.Height=corner?10:5;
            grip.PointerPressed+=(_,e)=>{if(window.CanResize&&window.WindowState==WindowState.Normal&&e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed){window.BeginResizeDrag(edge,e);e.Handled=true;}};
            void State(){grip.IsVisible=window.CanResize&&window.WindowState==WindowState.Normal;}
            window.PropertyChanged+=(_,e)=>{if(e.Property==Window.WindowStateProperty||e.Property==Window.CanResizeProperty)State();};State();frame.Children.Add(grip);
        }
        Edge(WindowEdge.North,HorizontalAlignment.Stretch,VerticalAlignment.Top,StandardCursorType.SizeNorthSouth);
        Edge(WindowEdge.South,HorizontalAlignment.Stretch,VerticalAlignment.Bottom,StandardCursorType.SizeNorthSouth);
        Edge(WindowEdge.West,HorizontalAlignment.Left,VerticalAlignment.Stretch,StandardCursorType.SizeWestEast);
        Edge(WindowEdge.East,HorizontalAlignment.Right,VerticalAlignment.Stretch,StandardCursorType.SizeWestEast);
        Edge(WindowEdge.NorthWest,HorizontalAlignment.Left,VerticalAlignment.Top,StandardCursorType.TopLeftCorner,true);
        Edge(WindowEdge.NorthEast,HorizontalAlignment.Right,VerticalAlignment.Top,StandardCursorType.TopRightCorner,true);
        Edge(WindowEdge.SouthWest,HorizontalAlignment.Left,VerticalAlignment.Bottom,StandardCursorType.BottomLeftCorner,true);
        Edge(WindowEdge.SouthEast,HorizontalAlignment.Right,VerticalAlignment.Bottom,StandardCursorType.BottomRightCorner,true);
    }
}
