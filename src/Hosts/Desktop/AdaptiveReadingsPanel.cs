using Avalonia;
using Avalonia.Controls;

namespace HardwarePulse.Desktop;

// Avalonia measurement of the existing Core column policy. Readings keep their
// controls and order; only the available width and explicit preferences set columns.
public sealed class AdaptiveReadingsPanel : Panel {
    double minimumColumnWidth=280,spacing=10;
    int requestedColumns;
    public double MinimumColumnWidth {get=>minimumColumnWidth;set{minimumColumnWidth=Math.Max(1,value);InvalidateMeasure();}}
    public double Spacing {get=>spacing;set{spacing=Math.Max(0,value);InvalidateMeasure();}}
    public int RequestedColumns {get=>requestedColumns;set{requestedColumns=Math.Clamp(value,0,3);InvalidateMeasure();}}
    public int Columns {get;private set;}
    double cellWidth;
    protected override Size MeasureOverride(Size available) {
        var layout=new ColumnLayout(available.Width,minimumColumnWidth,requestedColumns,Columns);
        Columns=layout.Columns;cellWidth=layout.CellWidth;
        double height=0,rowHeight=0;int count=0;
        foreach(var child in Children) {
            if(!child.IsVisible)continue;
            child.Measure(new Size(cellWidth,double.PositiveInfinity));rowHeight=Math.Max(rowHeight,child.DesiredSize.Height);
            if(++count%Columns==0){height+=rowHeight+spacing;rowHeight=0;}
        }
        if(count%Columns!=0)height+=rowHeight+spacing;
        return new Size(layout.Width,Math.Max(0,height-spacing));
    }
    protected override Size ArrangeOverride(Size finalSize) {
        var active=Children.Where(x=>x.IsVisible).ToArray();double top=0;
        for(int start=0;start<active.Length;start+=Columns) {
            double height=0;
            for(int i=start;i<Math.Min(start+Columns,active.Length);i++)height=Math.Max(height,active[i].DesiredSize.Height);
            for(int i=start;i<Math.Min(start+Columns,active.Length);i++)active[i].Arrange(new Rect((i-start)*(cellWidth+ColumnLayout.Gap),top,cellWidth,active[i].DesiredSize.Height));
            top+=height+spacing;
        }
        return finalSize;
    }
}
