using System;

namespace HardwarePulse {
    // Logical units supplied by the host; child measurement and animation stay in the view.
    public struct ColumnLayout {
        public const double Gap=10;
        public readonly double Width,CellWidth;
        public readonly int Columns;
        public ColumnLayout(double availableWidth,double minimumColumnWidth,int requestedColumns,int previousColumns){
            Width=double.IsInfinity(availableWidth)?minimumColumnWidth*Math.Max(1,requestedColumns)+Gap*Math.Max(0,requestedColumns-1):availableWidth;
            int fit=Math.Max(1,(int)Math.Floor((Width+Gap)/(minimumColumnWidth+Gap)));
            int count=Math.Max(1,Math.Min(requestedColumns>0?Math.Min(3,requestedColumns):3,fit));
            if(requestedColumns==0&&previousColumns>0&&count>previousColumns&&Width<count*minimumColumnWidth+(count-1)*Gap+16)count=previousColumns;
            Columns=count;CellWidth=Math.Max(1,(Width-Gap*(Columns-1))/Columns);
        }
    }
}
